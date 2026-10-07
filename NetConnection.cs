/* ----- ----- ----- ----- */
// NetConnection.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NetClient.Transport;

namespace NetClient
{
    /// <summary>
    /// One client connection's lifecycle: opens the transport, reads JSON line packets
    /// (<see cref="Envelope"/>), runs the application's <see cref="ConnectionHandshake"/>, then keeps
    /// a heartbeat going (sends one every interval, closes the connection when none arrives within
    /// the timeout), and reconnects on request. Thread-safe; a reconnect replaces the whole
    /// connection, and work belonging to an older one (its reader, its heartbeat, its handshake)
    /// can never close the newer one.
    /// </summary>
    public sealed class NetConnection
    {
        private readonly IStreamFactory _transport;
        private readonly NetConnectionOptions _options;
        private readonly INetLog _log;
        private readonly object _lock = new();

        // The current attempt or connection (null when disconnected); set from the start of
        // ConnectAsync, so a second call while one is still connecting returns instead of
        // opening a parallel connection.
        private Session _session;

        // The handshake of the last ConnectAsync, run again by Reconnect.
        private ConnectionHandshake _handshake;

        /// <summary>One opened (or opening) connection; replaced as a whole on reconnect.</summary>
        private sealed class Session
        {
            public readonly CancellationTokenSource Cts = new();
            public Stream Stream;
            public LineFraming Framing;
            public Timer HeartbeatTimer;
            public long LastHeartbeatTicks;  // UTC ticks of the last received heartbeat
        }

        /// <summary>Creates a disconnected connection.</summary>
        /// <param name="transport">Opens the stream for each connection attempt.</param>
        /// <param name="options">The heartbeat settings (null: no heartbeat).</param>
        /// <param name="log">Where diagnostics go (null: nowhere).</param>
        public NetConnection(IStreamFactory transport, NetConnectionOptions options = null, INetLog log = null)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _options = options ?? new NetConnectionOptions();
            _log = log ?? NullNetLog.Instance;

            if (_options.HeartbeatEnabled)
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.HeartbeatInterval, TimeSpan.Zero, nameof(options));
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(_options.HeartbeatTimeout, TimeSpan.Zero, nameof(options));
            }

            ClientId = Guid.NewGuid();
        }

        /// <summary>A random id for this client instance (the sender id of its heartbeats).</summary>
        public Guid ClientId { get; }

        /// <summary>The endpoint, for diagnostics.</summary>
        public string Endpoint => _transport.Describe();

        /// <summary>Whether the transport is open (from the moment it opens, so also during the handshake, until the disconnect).</summary>
        public bool IsConnected
        {
            get
            {
                lock (_lock)
                    return _session?.Framing != null;
            }
        }

        /// <summary>A packet arrived (heartbeats excluded). Raised on the reader's thread, one at a time.</summary>
        public event Action<Envelope> EnvelopeReceived;

        /// <summary>The connection (or the attempt to open it) ended. Raised once per connection, on whichever thread closed it.</summary>
        public event Action Disconnected;

        /// <summary>
        /// Opens the transport, starts reading, runs <paramref name="handshake"/> (if any) and then
        /// starts the heartbeat. Does nothing while a connection is already open or opening.
        /// </summary>
        /// <param name="handshake">The application's opening exchange, kept for <see cref="Reconnect"/>; null for none.</param>
        /// <returns>True when connected and the handshake (if any) succeeded; false otherwise (the connection is then closed).</returns>
        public async Task<bool> ConnectAsync(ConnectionHandshake handshake = null)
        {
            Session session;
            lock (_lock)
            {
                if (_session != null)
                    return false;
                session = _session = new Session();
                _handshake = handshake;
            }

            Stream stream;
            try
            {
                stream = await _transport.OpenAsync(session.Cts.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Unreachable, or Disconnect abandoned the attempt. Release it (if still current)
                // so a retry can start.
                if (!session.Cts.IsCancellationRequested)
                    _log.Write(NetLogLevel.Warning, $"Connect to {Endpoint} failed: {ex.Message}");
                Disconnect(session);
                return false;
            }

            lock (_lock)
            {
                if (_session != session)  // Disconnected meanwhile
                {
                    stream.Dispose();
                    return false;
                }
                session.Stream = stream;
                session.Framing = new LineFraming(stream);
            }

            _ = ListenAsync(session);

            if (handshake != null)
            {
                bool accepted;
                try
                {
                    accepted = await handshake(this, session.Cts.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _log.Write(NetLogLevel.Error, "Handshake error: " + ex.Message);
                    accepted = false;
                }

                if (!accepted)
                {
                    // Only this attempt is torn down: after a reconnect a newer one may be running.
                    if (IsCurrent(session))
                    {
                        _log.Write(NetLogLevel.Warning, "Handshake failed, disconnecting...");
                        Disconnect(session);
                    }
                    return false;
                }
            }

            lock (_lock)
            {
                if (_session != session)
                    return false;
                if (_options.HeartbeatEnabled)
                    StartHeartbeat(session);
            }

            _log.Write(NetLogLevel.Trace, $"Connected to {Endpoint}.");
            return true;
        }

        /// <summary>Closes the connection (or abandons the attempt to open it). Does nothing when disconnected.</summary>
        public void Disconnect()
        {
            Session session;
            lock (_lock)
                session = _session;
            Disconnect(session);
        }

        /// <summary>Closes the connection and opens a new one with the last handshake.</summary>
        public void Reconnect()
        {
            Disconnect();
            _log.Write(NetLogLevel.Trace, "Reconnecting...");

            ConnectionHandshake handshake;
            lock (_lock)
                handshake = _handshake;
            _ = ConnectAsync(handshake);
        }

        /// <summary>Sends one packet. A failed write closes the connection.</summary>
        /// <param name="envelope">The packet.</param>
        /// <returns>True when it was written; false when not connected or the write failed.</returns>
        public bool Send(Envelope envelope)
        {
            ArgumentNullException.ThrowIfNull(envelope);

            Session session;
            lock (_lock)
                session = _session;
            return session != null && SendOn(session, envelope);
        }

        // ----- Internals -----

        private bool IsCurrent(Session session)
        {
            lock (_lock)
                return _session == session;
        }

        /// <summary>Tears <paramref name="session"/> down if it is still the current one, then raises <see cref="Disconnected"/>.</summary>
        /// <returns>True when it was torn down here.</returns>
        private bool Disconnect(Session session)
        {
            lock (_lock)
            {
                // Several paths can end one connection (the reader, the heartbeat, a failed send,
                // the caller): only the first one tears it down and raises the event.
                if (session == null || _session != session)
                    return false;
                _session = null;
            }

            // The session is detached, so the rest needs no lock. Cancel first: the reader then
            // treats the closed stream as expected, and the handshake learns it was abandoned.
            session.Cts.Cancel();
            session.HeartbeatTimer?.Dispose();
            session.Stream?.Dispose();

            // Outside the lock, so a handler can call back into this class (e.g. Reconnect).
            try
            {
                Disconnected?.Invoke();
            }
            catch (Exception ex)
            {
                _log.Write(NetLogLevel.Error, "Disconnected handler error: " + ex.Message);
            }

            _log.Write(NetLogLevel.Trace, "Disconnected.");
            return true;
        }

        private bool SendOn(Session session, Envelope envelope)
        {
            LineFraming framing;
            lock (_lock)
                framing = session.Framing;
            if (framing == null || session.Cts.IsCancellationRequested)
                return false;

            try
            {
                framing.WriteLine(envelope.Serialize());
                return true;
            }
            catch (Exception ex)
            {
                // Also reached from the heartbeat timer, where an escaping exception would end the process.
                if (!session.Cts.IsCancellationRequested)
                    _log.Write(NetLogLevel.Warning, "Send failed: " + ex.Message);
                Disconnect(session);
                return false;
            }
        }

        /// <summary>Reads <paramref name="session"/>'s lines until it ends; never touches a newer connection.</summary>
        private async Task ListenAsync(Session session)
        {
            CancellationToken token = session.Cts.Token;
            LineFraming framing = session.Framing;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    string line = await framing.ReadLineAsync(token).ConfigureAwait(false);
                    if (line == null)  // The other side closed the connection
                    {
                        Disconnect(session);
                        return;
                    }

                    if (!Envelope.TryParse(line, out Envelope envelope, out string error))
                    {
                        // Blank lines and a bare "null" are harmless; anything else is worth a warning.
                        string trimmed = line.Trim();
                        NetLogLevel level = trimmed.Length == 0 || trimmed == "null" ? NetLogLevel.Trace : NetLogLevel.Warning;
                        _log.Write(level, $"{error}, ignored: {line}");
                        continue;
                    }

                    if (_options.HeartbeatEnabled && envelope.Type == _options.HeartbeatType)
                    {
                        Volatile.Write(ref session.LastHeartbeatTicks, DateTime.UtcNow.Ticks);
                        continue;
                    }

                    try
                    {
                        EnvelopeReceived?.Invoke(envelope);
                    }
                    catch (Exception ex)
                    {
                        // A failing handler must not end the connection.
                        _log.Write(NetLogLevel.Error, $"Packet handler error ({envelope.Type}): {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                // A cancelled token means Disconnect already closed this connection.
                if (token.IsCancellationRequested)
                    return;
                _log.Write(NetLogLevel.Warning, "Receive error: " + ex.Message);
                Disconnect(session);
            }
        }

        /// <summary>Starts <paramref name="session"/>'s heartbeat (called under the lock).</summary>
        private void StartHeartbeat(Session session)
        {
            // The timeout window starts now, not at a heartbeat from an earlier connection.
            Volatile.Write(ref session.LastHeartbeatTicks, DateTime.UtcNow.Ticks);
            session.HeartbeatTimer = new Timer(_ => HeartbeatTick(session), null, TimeSpan.Zero, _options.HeartbeatInterval);
        }

        private void HeartbeatTick(Session session)
        {
            if (session.Cts.IsCancellationRequested)
                return;

            var lastHeartbeat = new DateTime(Volatile.Read(ref session.LastHeartbeatTicks), DateTimeKind.Utc);
            if (DateTime.UtcNow - lastHeartbeat > _options.HeartbeatTimeout)
            {
                _log.Write(NetLogLevel.Warning, "Heartbeat timeout, disconnecting...");
                Disconnect(session);
                return;
            }

            SendOn(session, Envelope.Create(_options.HeartbeatType, senderId: ClientId.ToString()));
        }
    }
}

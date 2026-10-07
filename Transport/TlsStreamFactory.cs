/* ----- ----- ----- ----- */
// TlsStreamFactory.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;

namespace NetClient.Transport
{
    /// <summary>
    /// A TLS 1.3 connection over TCP to a fixed host and port (no older protocol version is
    /// offered). The server's certificate is checked by a <see cref="TlsCertificateValidation"/>;
    /// the host is also the TLS target name (SNI and the name the system validation checks).
    /// On macOS the runtime must use Network.framework (<c>System.Net.Security.UseNetworkFramework</c>
    /// set in the app's runtime configuration): the default SecureTransport backend has no TLS 1.3,
    /// and the handshake then fails.
    /// </summary>
    public sealed class TlsStreamFactory : IStreamFactory
    {
        private readonly string _host;
        private readonly int _port;
        private readonly TlsCertificateValidation _validation;

        /// <summary>Connects to <paramref name="host"/>:<paramref name="port"/> over TLS 1.3.</summary>
        /// <param name="host">A host name or IP address (also the TLS target name).</param>
        /// <param name="port">The TCP port (1-65535).</param>
        /// <param name="validation">How the server's certificate is checked.</param>
        public TlsStreamFactory(string host, int port, TlsCertificateValidation validation)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
            _host = host;
            _port = port;
            _validation = validation ?? throw new ArgumentNullException(nameof(validation));
        }

        /// <summary>How the server's certificate is checked.</summary>
        public TlsCertificateValidation Validation => _validation;

        /// <inheritdoc/>
        /// <exception cref="AuthenticationException">The TLS handshake failed (certificate refused, no TLS 1.3...).</exception>
        public async Task<Stream> OpenAsync(CancellationToken token)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            SslStream ssl = null;
            try
            {
                await socket.ConnectAsync(_host, _port, token).ConfigureAwait(false);
                ssl = new SslStream(new NetworkStream(socket, ownsSocket: true), leaveInnerStreamOpen: false);
                var options = new SslClientAuthenticationOptions
                {
                    TargetHost = _host,
                    EnabledSslProtocols = SslProtocols.Tls13,
                    RemoteCertificateValidationCallback = (_, certificate, _, errors) => _validation.Validate(certificate, errors),
                };
                await ssl.AuthenticateAsClientAsync(options, token).ConfigureAwait(false);
                return ssl;
            }
            catch
            {
                // The SslStream owns the network stream, which owns the socket.
                if (ssl != null)
                    ssl.Dispose();
                else
                    socket.Dispose();
                throw;
            }
        }

        /// <inheritdoc/>
        public string Describe() => $"{_host}:{_port} (TLS, {_validation})";
    }
}

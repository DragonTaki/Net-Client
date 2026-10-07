/* ----- ----- ----- ----- */
// TcpStreamFactory.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace NetClient.Transport
{
    /// <summary>A plain (unencrypted) TCP connection to a fixed host and port.</summary>
    public sealed class TcpStreamFactory : IStreamFactory
    {
        private readonly string _host;
        private readonly int _port;

        /// <summary>Connects to <paramref name="host"/>:<paramref name="port"/>.</summary>
        /// <param name="host">A host name or IP address.</param>
        /// <param name="port">The TCP port (1-65535).</param>
        public TcpStreamFactory(string host, int port)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(host);
            ArgumentOutOfRangeException.ThrowIfLessThan(port, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(port, 65535);
            _host = host;
            _port = port;
        }

        /// <inheritdoc/>
        public async Task<Stream> OpenAsync(CancellationToken token)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            try
            {
                await socket.ConnectAsync(_host, _port, token);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        /// <inheritdoc/>
        public string Describe() => $"{_host}:{_port}";
    }
}

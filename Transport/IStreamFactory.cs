/* ----- ----- ----- ----- */
// IStreamFactory.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace NetClient.Transport
{
    /// <summary>
    /// Opens the byte stream a <see cref="NetConnection"/> talks over: the replaceable transport
    /// (plain TCP to a server, TLS over TCP, a peer accepted by a local host's listener...).
    /// </summary>
    public interface IStreamFactory
    {
        /// <summary>
        /// Opens one connected stream. The returned stream owns its connection: disposing it closes
        /// the connection. Throws (any exception) when the other side cannot be reached.
        /// </summary>
        /// <param name="token">Cancelled when the attempt is abandoned (disconnect while connecting).</param>
        /// <returns>The connected, readable and writable stream.</returns>
        Task<Stream> OpenAsync(CancellationToken token);

        /// <summary>A short description of the endpoint for diagnostics (e.g. "127.0.0.1:8080").</summary>
        string Describe();
    }
}

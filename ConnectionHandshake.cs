/* ----- ----- ----- ----- */
// ConnectionHandshake.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

using System.Threading;
using System.Threading.Tasks;

namespace NetClient
{
    /// <summary>
    /// The application's opening exchange (e.g. version check and login), run by
    /// <see cref="NetConnection.ConnectAsync"/> once the transport is open and received lines are
    /// already being read (subscribe to <see cref="NetConnection.EnvelopeReceived"/> before
    /// sending). The heartbeat starts only after it succeeds; on false the connection is closed.
    /// Run again on every <see cref="NetConnection.Reconnect"/>.
    /// </summary>
    /// <param name="connection">The connection being opened.</param>
    /// <param name="abandoned">Cancelled when this connection is torn down (disconnect or reconnect)
    /// before the handshake finished: stop listening and complete with false.</param>
    /// <returns>True when the other side accepted the client.</returns>
    public delegate Task<bool> ConnectionHandshake(NetConnection connection, CancellationToken abandoned);
}

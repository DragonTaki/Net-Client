/* ----- ----- ----- ----- */
// NetConnectionOptions.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

using System;

namespace NetClient
{
    /// <summary>The heartbeat settings of a <see cref="NetConnection"/>.</summary>
    public sealed class NetConnectionOptions
    {
        /// <summary>
        /// The <see cref="Envelope.Type"/> of heartbeat packets, as the application names it.
        /// Received ones only reset the timeout (they are not raised as
        /// <see cref="NetConnection.EnvelopeReceived"/>). Null or empty: no heartbeat at all.
        /// </summary>
        public string HeartbeatType { get; init; }

        /// <summary>How often a heartbeat is sent (and the timeout checked). Default 1 second.</summary>
        public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(1);

        /// <summary>How long without a received heartbeat before the connection counts as lost and is closed. Default 8 seconds.</summary>
        public TimeSpan HeartbeatTimeout { get; init; } = TimeSpan.FromSeconds(8);

        /// <summary>Whether a heartbeat runs at all.</summary>
        public bool HeartbeatEnabled => !string.IsNullOrEmpty(HeartbeatType);
    }
}

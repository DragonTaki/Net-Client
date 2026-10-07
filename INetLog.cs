/* ----- ----- ----- ----- */
// INetLog.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

namespace NetClient
{
    /// <summary>
    /// Where the connection layer writes its diagnostics. The library has no logger of its own:
    /// the application adapts this to whatever it uses (or passes nothing to stay silent).
    /// Called from network threads, so an implementation must be thread-safe.
    /// </summary>
    public interface INetLog
    {
        /// <summary>Writes one message.</summary>
        /// <param name="level">How important the message is.</param>
        /// <param name="message">The message.</param>
        void Write(NetLogLevel level, string message);
    }

    /// <summary>An <see cref="INetLog"/> that drops everything.</summary>
    public sealed class NullNetLog : INetLog
    {
        /// <summary>The shared instance.</summary>
        public static readonly NullNetLog Instance = new();

        private NullNetLog() { }

        /// <inheritdoc/>
        public void Write(NetLogLevel level, string message) { }
    }
}

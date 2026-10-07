/* ----- ----- ----- ----- */
// NetLogLevel.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

namespace NetClient
{
    /// <summary>How important a <see cref="INetLog"/> message is.</summary>
    public enum NetLogLevel
    {
        Trace,    // Routine progress (connected, ignored blank line); usually shown only while debugging
        Info,     // Noteworthy but expected
        Warning,  // Something went wrong and was handled (bad packet, connect failed, timeout)
        Error,    // An unexpected failure
    }
}

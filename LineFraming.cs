/* ----- ----- ----- ----- */
// LineFraming.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetClient
{
    /// <summary>
    /// Newline-delimited UTF-8 framing over a stream: one message per line, ending in <c>\n</c>
    /// (a trailing <c>\r</c> is accepted on read). Reads come from one reader loop; writes may
    /// come from several threads and are serialized, so two messages' bytes never interleave.
    /// Does not own the stream.
    /// </summary>
    public sealed class LineFraming
    {
        private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

        private readonly Stream _stream;
        private readonly StreamReader _reader;
        private readonly object _writeLock = new();

        /// <summary>Frames <paramref name="stream"/>.</summary>
        /// <param name="stream">A connected, readable and writable stream.</param>
        public LineFraming(Stream stream)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
        }

        /// <summary>Reads the next line (single reader only).</summary>
        /// <param name="token">Cancels the read.</param>
        /// <returns>The line without its line break, or null when the other side closed the stream.</returns>
        public ValueTask<string> ReadLineAsync(CancellationToken token) => _reader.ReadLineAsync(token);

        /// <summary>Writes one line and its <c>\n</c> in a single write.</summary>
        /// <param name="line">The message; it must not contain a line break.</param>
        /// <exception cref="ArgumentException">The message contains a line break.</exception>
        public void WriteLine(string line)
        {
            ArgumentNullException.ThrowIfNull(line);
            if (line.AsSpan().IndexOfAny('\r', '\n') >= 0)
                throw new ArgumentException("A framed line must not contain a line break.", nameof(line));

            byte[] data = Utf8NoBom.GetBytes(line + "\n");
            lock (_writeLock)
            {
                _stream.Write(data, 0, data.Length);
                _stream.Flush();
            }
        }
    }
}

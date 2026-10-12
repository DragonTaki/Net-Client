/* ----- ----- ----- ----- */
// Envelope.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/12
// Version: v1.1
/* ----- ----- ----- ----- */

using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetClient
{
    /// <summary>
    /// One protocol message, sent as one JSON line:
    /// <c>{"type":…,"senderId":…,"roomId":…,"data":…,"token":…}</c>, plus <c>"refreshToken"</c>
    /// when one is set.
    /// <see cref="Type"/> is a plain string; the application maps it to its own packet kinds.
    /// </summary>
    public sealed class Envelope
    {
        /// <summary>The packet's kind, as the application names it (e.g. "Heartbeat").</summary>
        public string Type { get; set; } = "";

        /// <summary>The sender's id (a client never trusts the one another side puts here).</summary>
        public string SenderId { get; set; } = "";

        /// <summary>The room the packet belongs to, or empty.</summary>
        public string RoomId { get; set; } = "";

        /// <summary>The payload, usually a JSON document of its own.</summary>
        public string Data { get; set; } = "";

        /// <summary>The session token issued at login, or empty.</summary>
        public string Token { get; set; } = "";

        /// <summary>
        /// A long-lived login token (e.g. the one a remembered login is given, to log in with
        /// next time), or null. Left out of the JSON when null; null when a received packet has none.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string RefreshToken { get; set; }

        private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        /// <summary>Creates an envelope.</summary>
        /// <param name="type">The packet's kind.</param>
        /// <param name="senderId">The sender's id.</param>
        /// <param name="roomId">The room, or empty.</param>
        /// <param name="data">The payload.</param>
        /// <param name="token">The session token, or empty.</param>
        /// <param name="refreshToken">The login token, or null for none.</param>
        /// <returns>The envelope (null arguments other than <paramref name="refreshToken"/> become empty strings).</returns>
        public static Envelope Create(string type, string senderId = "", string roomId = "", string data = "", string token = "", string refreshToken = null)
        {
            return new Envelope
            {
                Type = type ?? "",
                SenderId = senderId ?? "",
                RoomId = roomId ?? "",
                Data = data ?? "",
                Token = token ?? "",
                RefreshToken = refreshToken,
            };
        }

        /// <summary>The envelope as one line of JSON (never contains a raw line break: JSON escapes them).</summary>
        /// <returns>The JSON text.</returns>
        public string Serialize() => JsonSerializer.Serialize(this, _options);

        /// <summary>
        /// Parses one received line. Never throws: a blank line, text that is not a JSON object,
        /// invalid JSON or the literal <c>null</c> give false and a short reason.
        /// </summary>
        /// <param name="line">The received line (without its line break).</param>
        /// <param name="envelope">The parsed envelope (missing or null fields are empty strings, except
        /// <see cref="RefreshToken"/>, which stays null), or null.</param>
        /// <param name="error">Why the line was rejected, or null.</param>
        /// <returns>True when the line held an envelope.</returns>
        public static bool TryParse(string line, out Envelope envelope, out string error)
        {
            envelope = null;
            if (string.IsNullOrWhiteSpace(line))
            {
                error = "Empty line";
                return false;
            }
            if (!line.TrimStart().StartsWith('{'))
            {
                error = "Not a JSON object";
                return false;
            }

            Envelope parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<Envelope>(line, _options);
            }
            catch (JsonException ex)
            {
                error = "Invalid JSON: " + ex.Message;
                return false;
            }

            if (parsed == null)
            {
                error = "Empty packet";
                return false;
            }

            // An explicit JSON null overrides the property defaults.
            parsed.Type ??= "";
            parsed.SenderId ??= "";
            parsed.RoomId ??= "";
            parsed.Data ??= "";
            parsed.Token ??= "";

            envelope = parsed;
            error = null;
            return true;
        }
    }
}

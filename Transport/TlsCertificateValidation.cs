/* ----- ----- ----- ----- */
// TlsCertificateValidation.cs
// Do not distribute or modify
// Author: DragonTaki (https://github.com/DragonTaki)
// Create Date: 2026/10/07
// Update Date: 2026/10/07
// Version: v1.0
/* ----- ----- ----- ----- */

using System;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NetClient.Transport
{
    /// <summary>
    /// How a <see cref="TlsStreamFactory"/> decides whether to trust the server's certificate:
    /// the operating system's certificate authorities (a production server with a real
    /// certificate), or one pinned certificate known by its SHA-256 fingerprint (a self-signed
    /// development or LAN server).
    /// </summary>
    public sealed class TlsCertificateValidation
    {
        private readonly byte[] _pinnedSha256;  // null: system validation

        private TlsCertificateValidation(byte[] pinnedSha256)
        {
            _pinnedSha256 = pinnedSha256;
        }

        /// <summary>
        /// Trust what the system trusts: the chain must reach a trusted root and the certificate
        /// must name the host (no policy errors at all).
        /// </summary>
        public static TlsCertificateValidation SystemTrust { get; } = new(null);

        /// <summary>Whether a certificate is pinned (false: system validation).</summary>
        public bool IsPinned => _pinnedSha256 != null;

        /// <summary>The pinned fingerprint as upper-case hex without separators; null for system validation.</summary>
        public string PinnedFingerprint => _pinnedSha256 == null ? null : Convert.ToHexString(_pinnedSha256);

        /// <summary>
        /// Trust exactly the certificate whose SHA-256 fingerprint (of the whole DER certificate,
        /// as <c>openssl x509 -fingerprint -sha256</c> prints it) is <paramref name="fingerprint"/>,
        /// whatever its chain, validity dates or names say; any other certificate is refused.
        /// </summary>
        /// <param name="fingerprint">64 hex digits; colons, spaces and case are ignored.</param>
        /// <returns>The policy.</returns>
        /// <exception cref="ArgumentException">Not a SHA-256 fingerprint.</exception>
        public static TlsCertificateValidation PinnedSha256(string fingerprint)
        {
            if (!TryParseFingerprint(fingerprint, out byte[] hash))
                throw new ArgumentException("Not a SHA-256 fingerprint (64 hex digits, colons allowed).", nameof(fingerprint));
            return new TlsCertificateValidation(hash);
        }

        /// <summary>Parses a SHA-256 fingerprint (64 hex digits; colons, spaces and case ignored). Never throws.</summary>
        /// <param name="fingerprint">The text.</param>
        /// <param name="hash">The 32 bytes, or null.</param>
        /// <returns>True when the text is a SHA-256 fingerprint.</returns>
        public static bool TryParseFingerprint(string fingerprint, out byte[] hash)
        {
            hash = null;
            if (fingerprint == null)
                return false;

            var digits = new StringBuilder(64);
            foreach (char c in fingerprint)
            {
                if (c == ':' || char.IsWhiteSpace(c))
                    continue;
                if (!Uri.IsHexDigit(c))
                    return false;
                digits.Append(c);
            }
            if (digits.Length != 64)
                return false;

            hash = Convert.FromHexString(digits.ToString());
            return true;
        }

        /// <summary>The verdict on the certificate the server presented (an SslStream validation callback).</summary>
        /// <param name="certificate">The server's certificate, or null when it sent none.</param>
        /// <param name="errors">What the system's validation found.</param>
        /// <returns>True to accept the connection.</returns>
        public bool Validate(X509Certificate certificate, SslPolicyErrors errors)
        {
            if (_pinnedSha256 == null)
                return errors == SslPolicyErrors.None;
            if (certificate == null)
                return false;

            byte[] actual = certificate.GetCertHash(HashAlgorithmName.SHA256);
            return CryptographicOperations.FixedTimeEquals(actual, _pinnedSha256);
        }

        /// <inheritdoc/>
        public override string ToString() => IsPinned ? "pinned SHA-256 " + PinnedFingerprint : "system CA";
    }
}

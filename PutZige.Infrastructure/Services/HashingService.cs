#nullable enable
using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PutZige.Application.Interfaces;
using PutZige.Application.Settings;
using PutZige.Application.DTOs;

namespace PutZige.Infrastructure.Services
{
    /// <summary>
    /// Provides hashing operations for passwords and tokens.
    /// </summary>
    public class HashingService : IHashingService
    {
        private readonly HashingSettings _settings;
        private readonly ILogger<HashingService>? _logger;

        public HashingService(IOptions<HashingSettings> options, ILogger<HashingService>? logger = null)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _logger = logger;
        }

        /// <summary>
        /// Hashes a plaintext value using PBKDF2 with HMAC-SHA algorithms.
        /// Returns the hash and salt as base64 strings.
        /// </summary>
        public async Task<HashedValue> HashAsync(string plainText, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(plainText)) throw new ArgumentException(PutZige.Application.Common.Messages.ErrorMessages.Validation.PlainTextRequired, nameof(plainText));

            ct.ThrowIfCancellationRequested();

            var salt = new byte[_settings.SaltSizeBytes];
            RandomNumberGenerator.Fill(salt);

            var hash = await Task.Run(() =>
            {
                var alg = _settings.Algorithm.ToUpperInvariant();
                HashAlgorithmName hashName = alg == "SHA256" ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512;

                var derived = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(plainText), salt, _settings.Iterations, hashName, 64);
                return Convert.ToBase64String(derived);
            }, ct).ConfigureAwait(false);

            var saltStr = Convert.ToBase64String(salt);
            return new HashedValue(hash, saltStr);
        }

        /// <summary>
        /// Verifies a plaintext against the provided hash and salt using constant-time comparison.
        /// </summary>
        public async Task<bool> VerifyAsync(string plainText, string hash, string salt, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(plainText)) throw new ArgumentException(PutZige.Application.Common.Messages.ErrorMessages.Validation.PlainTextRequired, nameof(plainText));
            if (string.IsNullOrEmpty(hash)) throw new ArgumentException(PutZige.Application.Common.Messages.ErrorMessages.Validation.HashRequired, nameof(hash));
            if (string.IsNullOrEmpty(salt)) throw new ArgumentException(PutZige.Application.Common.Messages.ErrorMessages.Validation.SaltRequired, nameof(salt));

            ct.ThrowIfCancellationRequested();

            return await Task.Run(() =>
            {
                var saltBytes = Convert.FromBase64String(salt);
                var alg = _settings.Algorithm.ToUpperInvariant();
                HashAlgorithmName hashName = alg == "SHA256" ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA512;

                var derived = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(plainText), saltBytes, _settings.Iterations, hashName, 64);
                var derivedStr = Convert.ToBase64String(derived);

                var a = Convert.FromBase64String(derivedStr);
                var b = Convert.FromBase64String(hash);

                return CryptographicOperations.FixedTimeEquals(a, b);
            }, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Generates a URL-safe base64 secure token of the specified byte length.
        /// </summary>
        public string GenerateSecureToken(int byteLength = 32)
        {
            var bytes = new byte[byteLength];
            RandomNumberGenerator.Fill(bytes);
            var token = Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
            return token;
        }

        public string GenerateEmailVerificationToken(string email, int randomByteLength = 32)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required", nameof(email));

            // Generate secure random token
            var randomToken = GenerateSecureToken(randomByteLength);

            // Base64URL encode the email
            var emailBytes = Encoding.UTF8.GetBytes(email);
            var encodedEmail = Convert.ToBase64String(emailBytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');

            // Composite format: {randomToken}.{encodedEmail}
            return $"{randomToken}.{encodedEmail}";
        }

        public string ExtractEmailFromVerificationToken(string compositeToken)
        {
            if (string.IsNullOrWhiteSpace(compositeToken))
                throw new ArgumentException("Token is required", nameof(compositeToken));

            var parts = compositeToken.Split('.');
            if (parts.Length != 2)
                throw new ArgumentException("Invalid token format", nameof(compositeToken));

            try
            {
                // Decode the email part (add padding if needed)
                var encodedEmail = parts[1];
                var padding = (4 - (encodedEmail.Length % 4)) % 4;
                var base64 = encodedEmail
                    .Replace("-", "+")
                    .Replace("_", "/") + new string('=', padding);

                var emailBytes = Convert.FromBase64String(base64);
                return Encoding.UTF8.GetString(emailBytes);
            }
            catch (Exception ex)
            {
                throw new ArgumentException("Failed to decode email from token", nameof(compositeToken), ex);
            }
        }
    }
}

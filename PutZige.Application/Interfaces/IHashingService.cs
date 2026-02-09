using System.Threading;
using System.Threading.Tasks;
using PutZige.Application.DTOs;

namespace PutZige.Application.Interfaces
{
    public interface IHashingService
    {
        Task<HashedValue> HashAsync(string plainText, CancellationToken ct = default);
        Task<bool> VerifyAsync(string plainText, string hash, string salt, CancellationToken ct = default);
        string GenerateSecureToken(int byteLength = 32);
        /// <summary>
        /// Generates a composite verification token containing an encoded email and secure random token.
        /// Format: {randomToken}.{base64UrlEncodedEmail}
        /// </summary>
        string GenerateEmailVerificationToken(string email, int randomByteLength = 32);

        /// <summary>
        /// Extracts and validates the email from a composite verification token.
        /// Returns the decoded email if valid, throws exception if invalid format.
        /// </summary>
        string ExtractEmailFromVerificationToken(string compositeToken);
    }
}
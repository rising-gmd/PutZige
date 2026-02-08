#nullable enable
using System.Threading;
using System.Threading.Tasks;
using PutZige.Application.DTOs.Auth;

namespace PutZige.Application.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(string identifier, string password, CancellationToken ct = default);
        Task<RefreshTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct = default);
        Task<bool> VerifyEmailAsync(string token, CancellationToken ct = default);
        Task ResendVerificationEmailAsync(string email, CancellationToken ct = default);
        Task ResendVerificationEmailByTokenAsync(string compositeToken, CancellationToken ct = default);
    }
}

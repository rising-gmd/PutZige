using PutZige.Domain.DTOs;

namespace PutZige.Domain.Interfaces
{
    public interface IDapperUserRepository
    {
        Task<int> VerifyEmailByTokenAsync(string token, CancellationToken ct = default);
        Task<UserProfileProjection?> GetProfileByIdAsync(Guid id, CancellationToken ct = default);
    }
}

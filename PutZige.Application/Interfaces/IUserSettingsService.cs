#nullable enable
using System.Threading;
using System.Threading.Tasks;
using PutZige.Application.DTOs.Users;

namespace PutZige.Application.Interfaces
{
    public interface IUserSettingsService
    {
        Task<UserPreferencesDto> UpdatePreferencesAsync(UpdatePreferencesRequest request, CancellationToken cancellationToken = default);
        Task<UserPreferencesDto> GetPreferencesAsync(CancellationToken cancellationToken = default);
    }
}

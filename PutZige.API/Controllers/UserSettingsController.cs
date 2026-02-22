#nullable enable
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PutZige.Application.Interfaces;
using PutZige.Application.DTOs.Users;

namespace PutZige.API.Controllers
{
    [ApiController]
    [Route("api/v1/users/me/settings")]
    public class UserSettingsController : BaseApiController
    {
        private readonly IUserSettingsService _userSettingsService;
        private readonly ILogger<UserSettingsController> _logger;

        public UserSettingsController(IUserSettingsService userSettingsService, ILogger<UserSettingsController> logger)
        {
            _userSettingsService = userSettingsService;
            _logger = logger;
        }

        [HttpPatch("preferences")]
        public Task<UserPreferencesDto> UpdatePreferences([FromBody] UpdatePreferencesRequest request, CancellationToken ct)
        {
            return _userSettingsService.UpdatePreferencesAsync(request, ct);
        }

        [HttpGet("preferences")]
        public Task<UserPreferencesDto> GetPreferences(CancellationToken ct)
        {
            return _userSettingsService.GetPreferencesAsync(ct);
        }
    }
}

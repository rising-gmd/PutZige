#nullable enable
using System;
using System.Security.Claims;

namespace PutZige.Application.Interfaces
{
    public interface IJwtTokenService
    {
        string GenerateAccessToken(Guid userId, string email, string username, double expiryMinutes, out DateTime expiresAt);
        string GenerateRefreshToken();
        ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true);
    }
}

using System;
using PutZige.Application.Interfaces;

namespace PutZige.Application.Tests
{
    internal class TestJwtTokenService : IJwtTokenService
    {
        public string GenerateAccessToken(Guid userId, string email, string username, double expiryMinutes, out DateTime expiresAt)
        {
            expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);
            return "access-token-test";
        }

        public string GenerateRefreshToken()
        {
            return "refresh-token-test";
        }

        public System.Security.Claims.ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
        {
            return null;
        }
    }
}

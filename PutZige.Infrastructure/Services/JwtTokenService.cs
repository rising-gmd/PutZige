#nullable enable
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PutZige.Application.Interfaces;
using PutZige.Application.Settings;

namespace PutZige.Infrastructure.Services
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _settings;
        private readonly byte[] _keyBytes;

        public JwtTokenService(IOptions<JwtSettings> options)
        {
            _settings = options?.Value ?? throw new ArgumentNullException(nameof(options));
            if (string.IsNullOrWhiteSpace(_settings.Secret)) throw new ArgumentException(PutZige.Application.Common.Messages.ErrorMessages.Security.JwtSecretNotConfigured, nameof(_settings.Secret));
            if (_settings.Secret.Length < 32) throw new ArgumentException(PutZige.Application.Common.Messages.ErrorMessages.Security.JwtSecretTooShort, nameof(_settings.Secret));
            _keyBytes = Encoding.UTF8.GetBytes(_settings.Secret);
        }

        public string GenerateAccessToken(Guid userId, string email, string username, double expiryMinutes, out DateTime expiresAt)
        {
            expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim("username", username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            var signingKey = new SymmetricSecurityKey(_keyBytes);
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: expiresAt,
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }

        public ClaimsPrincipal? ValidateToken(string token, bool validateLifetime = true)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(_keyBytes),
                ValidateIssuerSigningKey = true,
                ValidateLifetime = validateLifetime,
                ClockSkew = TimeSpan.FromSeconds(30)
            };

            try
            {
                var principal = tokenHandler.ValidateToken(token, parameters, out var validatedToken);
                return principal;
            }
            catch
            {
                // Intentionally return null for invalid tokens; caller may handle logging if needed.
                return null;
            }
        }
    }
}

#nullable enable
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using PutZige.Application.Common.Constants;
using PutZige.Application.Interfaces;
using PutZige.Application.Settings;

namespace PutZige.Infrastructure.Services
{
    /// <summary>
    /// Service for managing authentication-related HTTP cookies.
    /// </summary>
    public sealed class CookieService : ICookieService
    {
        private readonly JwtSettings _jwtSettings;

        public CookieService(IOptions<JwtSettings> jwtOptions)
        {
            ArgumentNullException.ThrowIfNull(jwtOptions);
            _jwtSettings = jwtOptions.Value;
        }

        /// <inheritdoc />
        public void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);

            var cookieOptions = CreateSecureCookieOptions(ApiConstants.API_PATH);

            // Set access token cookie (HttpOnly)
            response.Cookies.Append(
                CookieConstants.ACCESS_TOKEN,
                accessToken,
                CreateCookieOptions(
                    cookieOptions,
                    DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)));

            // Set refresh token cookie (HttpOnly)
            response.Cookies.Append(
                CookieConstants.REFRESH_TOKEN,
                refreshToken,
                CreateCookieOptions(
                    cookieOptions,
                    DateTimeOffset.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays)));

            // Set XSRF token cookie (readable by JavaScript)
            var xsrfOptions = CreateSecureCookieOptions(ApiConstants.API_PATH);
            xsrfOptions.HttpOnly = false;

            response.Cookies.Append(
                "XSRF-TOKEN",
                Guid.NewGuid().ToString(),
                CreateCookieOptions(
                    xsrfOptions,
                    DateTimeOffset.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes)));
        }

        /// <inheritdoc />
        public string? GetRefreshToken(HttpRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            request.Cookies.TryGetValue(CookieConstants.REFRESH_TOKEN, out var refreshToken);
            return refreshToken;
        }

        /// <inheritdoc />
        public void ClearAuthCookies(HttpResponse response)
        {
            ArgumentNullException.ThrowIfNull(response);

            var deletionOptions = new CookieOptions
            {
                Path = ApiConstants.API_PATH
            };

            response.Cookies.Delete(CookieConstants.ACCESS_TOKEN, deletionOptions);
            response.Cookies.Delete(CookieConstants.REFRESH_TOKEN, deletionOptions);
            response.Cookies.Delete("XSRF-TOKEN", deletionOptions);
        }

        private static CookieOptions CreateSecureCookieOptions(string path)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = path
            };
        }

        private static CookieOptions CreateCookieOptions(CookieOptions baseOptions, DateTimeOffset expires)
        {
            return new CookieOptions
            {
                HttpOnly = baseOptions.HttpOnly,
                Secure = baseOptions.Secure,
                SameSite = baseOptions.SameSite,
                Path = baseOptions.Path,
                Expires = expires
            };
        }
    }
}
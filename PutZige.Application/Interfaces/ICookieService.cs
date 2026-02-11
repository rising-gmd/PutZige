#nullable enable
using Microsoft.AspNetCore.Http;

namespace PutZige.Application.Interfaces
{
    /// <summary>
    /// Service contract for managing HTTP cookies for authentication.
    /// </summary>
    public interface ICookieService
    {
        /// <summary>
        /// Sets authentication cookies (access token, refresh token, XSRF token).
        /// </summary>
        /// <param name="response">HTTP response object</param>
        /// <param name="accessToken">JWT access token</param>
        /// <param name="refreshToken">Refresh token</param>
        void SetAuthCookies(HttpResponse response, string accessToken, string refreshToken);

        /// <summary>
        /// Retrieves the refresh token from HTTP request cookies.
        /// </summary>
        /// <param name="request">HTTP request object</param>
        /// <returns>Refresh token or null if not found</returns>
        string? GetRefreshToken(HttpRequest request);

        /// <summary>
        /// Clears all authentication-related cookies.
        /// </summary>
        /// <param name="response">HTTP response object</param>
        void ClearAuthCookies(HttpResponse response);
    }
}
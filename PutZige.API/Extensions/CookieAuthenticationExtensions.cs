using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PutZige.API.Extensions
{
    internal static class CookieAuthenticationExtensions
    {
        public static IServiceCollection AddCookieAuthenticationConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Read JWT settings to align cookie expiries with token expiries
            var jwt = configuration.GetSection(PutZige.Application.Settings.JwtSettings.SectionName).Get<PutZige.Application.Settings.JwtSettings>();
            var accessMinutes = jwt?.AccessTokenExpiryMinutes ?? 15;
            var refreshDays = jwt?.RefreshTokenExpiryDays ?? 7;
            var cookieDomain = configuration.GetValue<string>("Cookie:Domain");

            services.AddAuthentication()
                .AddCookie("AccessCookie", options =>
                {
                    options.Cookie.Name = PutZige.Application.Common.Constants.CookieConstants.ACCESS_TOKEN;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
                    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                    options.Cookie.Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH;
                    if (!string.IsNullOrWhiteSpace(cookieDomain)) options.Cookie.Domain = cookieDomain;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(accessMinutes);
                    options.SlidingExpiration = false;
                })
                .AddCookie("RefreshCookie", options =>
                {
                    options.Cookie.Name = PutZige.Application.Common.Constants.CookieConstants.REFRESH_TOKEN;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SecurePolicy = Microsoft.AspNetCore.Http.CookieSecurePolicy.Always;
                    options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
                    options.Cookie.Path = PutZige.Application.Common.Constants.ApiConstants.API_PATH;
                    if (!string.IsNullOrWhiteSpace(cookieDomain)) options.Cookie.Domain = cookieDomain;
                    options.ExpireTimeSpan = TimeSpan.FromDays(refreshDays);
                    options.SlidingExpiration = false;
                });

            return services;
        }
    }
}

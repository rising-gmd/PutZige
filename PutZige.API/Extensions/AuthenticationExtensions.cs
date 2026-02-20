using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using PutZige.Application.Common.Constants;
using PutZige.Application.Settings;
using System;
using System.Text;

namespace PutZige.API.Extensions
{
    /// <summary>
    /// Authentication setup extensions.
    /// </summary>
    public static class AuthenticationExtensions
    {
        /// <summary>
        /// Adds JWT bearer authentication configuration. If JWT settings are missing or invalid, logs a warning and does not add authentication.
        /// </summary>
        public static IServiceCollection AddAuthenticationConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            try
            {
                var jwtSection = configuration.GetSection(JwtSettings.SectionName);
                var jwtSettings = jwtSection.Get<JwtSettings>();
                var loggerFactory = services.BuildServiceProvider().GetService<ILoggerFactory>();
                var logger = loggerFactory?.CreateLogger("Authentication");

                if (jwtSettings == null || string.IsNullOrWhiteSpace(jwtSettings.Secret))
                {
                    logger?.LogWarning("JWT settings missing or incomplete. Authentication will not be configured.");
                    return services;
                }

                var key = Encoding.UTF8.GetBytes(jwtSettings.Secret);

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    options.RequireHttpsMetadata = false;
                    options.SaveToken = true;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtSettings.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                    // Allow JWT from query string for SignalR
                    options.Events = new JwtBearerEvents
                    {
                        OnMessageReceived = context =>
                        {
                            var path = context.HttpContext.Request.Path;

                            // SignalR: Check query string first (for fallback)
                            if (path.StartsWithSegments(SignalRConstants.HubRoute))
                            {
                                var accessToken = context.Request.Query["access_token"].ToString();
                                if (!string.IsNullOrEmpty(accessToken))
                                {
                                    context.Token = accessToken;
                                    return System.Threading.Tasks.Task.CompletedTask;
                                }

                                // SignalR with cookies: Read from cookie
                                if (context.Request.Cookies.TryGetValue(CookieConstants.ACCESS_TOKEN, out var cookieToken))
                                {
                                    context.Token = cookieToken;
                                    return System.Threading.Tasks.Task.CompletedTask;
                                }
                            }

                            // Regular API calls: Read from cookie
                            if (context.Request.Cookies.TryGetValue(CookieConstants.ACCESS_TOKEN, out var token))
                            {
                                context.Token = token;
                            }

                            return System.Threading.Tasks.Task.CompletedTask;
                        }
                    };
                });

                logger?.LogInformation("JWT authentication configured");
            }
            catch (Exception ex)
            {
                var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("Authentication");
                logger?.LogError(ex, "Exception while configuring JWT authentication; continuing without authentication");
            }

            return services;
        }
    }
}

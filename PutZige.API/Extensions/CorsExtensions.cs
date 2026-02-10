#nullable enable
using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using PutZige.Application.Settings;

namespace PutZige.API.Extensions
{
    /// <summary>
    /// Extensions to register and apply CORS policies in an environment-aware and configuration-driven manner.
    /// </summary>
    public static class CorsExtensions
    {
        private const string DefaultPolicyName = "Default";

        /// <summary>
        /// Adds CORS configuration reading settings from configuration section <see cref="CorsSettings.SectionName"/>.
        /// If configuration is missing or invalid, registers a fail-safe default that denies all cross-origin requests.
        /// </summary>
        public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration, IHostEnvironment? env = null)
        {
            try
            {
                var section = configuration.GetSection(CorsSettings.SectionName);
                var settings = section.Get<CorsSettings>();

                var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("CorsConfiguration");

                if (settings == null || settings.AllowedOrigins == null || !settings.AllowedOrigins.Any())
                {
                    logger?.LogWarning("CORS settings missing or empty. Registering a deny-all CORS policy as a fail-safe.");

                    services.AddCors(options =>
                    {
                        options.AddPolicy(DefaultPolicyName, builder =>
                        {
                            // No origins added -> deny all cross-origin requests by default
                        });
                    });

                    return services;
                }

                var policyName = string.IsNullOrWhiteSpace(settings.PolicyName) ? DefaultPolicyName : settings.PolicyName;

                services.AddCors(options =>
                {
                    options.AddPolicy(policyName, builder =>
                    {
                        // Environment-aware: allow wildcards in Development only
                        var isDev = string.Equals(env?.EnvironmentName, "Development", StringComparison.OrdinalIgnoreCase);

                        if (isDev && settings.AllowedOrigins.Contains("*"))
                        {
                            builder.SetIsOriginAllowed(_ => true);
                        }
                        else
                        {
                            // Use explicit origins for production/staging
                            var origins = settings.AllowedOrigins.Where(o => !string.IsNullOrWhiteSpace(o)).ToArray();
                            if (origins.Any())
                            {
                                builder.WithOrigins(origins);
                            }
                        }

                        // Methods and headers
                        if (settings.AllowedMethods != null && settings.AllowedMethods.Any())
                        {
                            builder.WithMethods(settings.AllowedMethods.ToArray());
                        }
                        else
                        {
                            builder.AllowAnyMethod();
                        }

                        if (settings.AllowedHeaders != null && settings.AllowedHeaders.Any())
                        {
                            if (settings.AllowedHeaders.Count == 1 && settings.AllowedHeaders[0] == "*")
                                builder.AllowAnyHeader();
                            else
                                builder.WithHeaders(settings.AllowedHeaders.ToArray());
                        }
                        else
                        {
                            builder.AllowAnyHeader();
                        }

                        if (settings.AllowCredentials)
                        {
                            // AllowCredentials requires explicit origins (cannot be used with AllowAnyOrigin)
                            builder.AllowCredentials();
                            // Expose Set-Cookie so browsers can access cookie headers when AllowCredentials is true
                            builder.WithExposedHeaders("Set-Cookie");
                        }

                        // Preflight caching for performance
                        builder.SetPreflightMaxAge(TimeSpan.FromSeconds(Math.Max(0, settings.PreflightMaxAgeSeconds)));
                    });
                });

                logger?.LogInformation("CORS configured with policy '{PolicyName}' and {Origins} origins", policyName, settings.AllowedOrigins.Count);
            }
            catch (Exception ex)
            {
                var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("CorsConfiguration");
                logger?.LogError(ex, "Exception while configuring CORS; registering a deny-all policy as fallback");

                services.AddCors(options => options.AddPolicy(DefaultPolicyName, builder => { }));
            }

            return services;
        }

        /// <summary>
        /// Applies the named CORS policy added via <see cref="AddCorsConfiguration"/>. If the policy is missing, logs a warning and continues.
        /// </summary>
        public static WebApplication UseCorsConfiguration(this WebApplication app)
        {
            try
            {
                var logger = app.Services.GetService<ILoggerFactory>()?.CreateLogger("CorsConfiguration");
                var settings = app.Configuration.GetSection(CorsSettings.SectionName).Get<CorsSettings>();
                var policyName = settings == null || string.IsNullOrWhiteSpace(settings.PolicyName) ? DefaultPolicyName : settings.PolicyName;

                // It's cheaper to call UseCors with a static policy name than to evaluate origins per-request
                app.UseCors(policyName);

                logger?.LogInformation("Applied CORS policy '{PolicyName}'", policyName);
            }
            catch (Exception ex)
            {
                var logger = app.Services.GetService<ILoggerFactory>()?.CreateLogger("CorsConfiguration");
                logger?.LogError(ex, "Exception while applying CORS policy; continuing without CORS middleware");
            }

            return app;
        }
    }
}

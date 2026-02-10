using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using FluentValidation.AspNetCore;
using PutZige.API.Filters;
using Microsoft.Extensions.Logging;
using PutZige.Application.Interfaces;
using PutZige.API.RealTime;

namespace PutZige.API.Extensions
{
    /// <summary>
    /// API configuration extensions (controllers, validation, API behavior).
    /// </summary>
    public static class ApiConfigurationExtensions
    {
        /// <summary>
        /// Adds API controllers, configures API behavior, and integrates FluentValidation.
        /// </summary>
        public static IServiceCollection AddApiConfiguration(this IServiceCollection services)
        {
            try
            {
                services.AddControllers(options =>
                {
                    options.Filters.Add<ValidationFilter>();
                })
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.SuppressModelStateInvalidFilter = true;
                });

                services.AddFluentValidationAutoValidation();
                services.AddFluentValidationClientsideAdapters();

                // Register real-time notifier (SignalR implementation)
                services.AddScoped<IRealTimeNotifier, SignalRNotifier>();

                var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("ApiConfiguration");
                logger?.LogInformation("Controllers and FluentValidation configured");
            }
            catch (Exception ex)
            {
                var logger = services.BuildServiceProvider().GetService<ILoggerFactory>()?.CreateLogger("ApiConfiguration");
                logger?.LogError(ex, "Exception while configuring API controllers/validation; continuing");
            }

            return services;
        }
    }
}

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using PutZige.Infrastructure.Settings;
using PutZige.API.Hubs;
using PutZige.Application.Common.Constants;

namespace PutZige.API.Extensions;

public static class SignalRExtensions
{
    public static IServiceCollection AddSignalRConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(SignalRSettings.SectionName).Get<SignalRSettings>();

        var signalRBuilder = services.AddSignalR(options =>
        {
            options.KeepAliveInterval = TimeSpan.FromSeconds(settings?.KeepAliveIntervalSeconds ?? 15);
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(settings?.ClientTimeoutSeconds ?? 30);
            options.HandshakeTimeout = TimeSpan.FromSeconds(settings?.HandshakeTimeoutSeconds ?? 15);
        });

        if (settings?.EnableRedis == true && !string.IsNullOrWhiteSpace(settings.RedisConnectionString))
        {
            signalRBuilder.AddStackExchangeRedis(settings.RedisConnectionString);
        }

        return services;
    }

    public static void MapSignalRHubs(this WebApplication app)
    {
        app.MapHub<ChatHub>(SignalRConstants.HubRoute);
    }
}

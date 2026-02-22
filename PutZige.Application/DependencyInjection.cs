// File: src/PutZige.Application/DependencyInjection.cs

#nullable enable
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using PutZige.Application.Services;
using PutZige.Application.Interfaces;
using AutoMapper;

namespace PutZige.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // AutoMapper - scan this assembly for Profile classes
        services.AddAutoMapper(assembly);

        // Register FluentValidation validators from this assembly
        services.AddValidatorsFromAssembly(assembly);

        // Register application services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();

        // Time abstraction for testability (registered in infrastructure where implementation lives)

        return services;
    }
}

#nullable enable
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PutZige.Domain.Interfaces;
using PutZige.Infrastructure.Data;
using PutZige.Infrastructure.Repositories;
using PutZige.Infrastructure.Settings;
using PutZige.Infrastructure.DependencyInjectionHelpers;
using System;
using System.Linq;
using PutZige.Application.Interfaces;
using PutZige.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PutZige.Application.Settings;
using PutZige.Application.Validators;
using PutZige.Infrastructure.Data.Dapper;
using PutZige.Infrastructure.Repositories.Dapper;

namespace PutZige.Infrastructure;

/// <summary>
/// Registers infrastructure services into the DI container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds infrastructure services such as DbContext, repositories and settings validators.
    /// </summary>
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, bool hangfireEnabled = false)
    {
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));
        services.AddSingleton<IValidator<DatabaseSettings>, DatabaseSettingsValidator>();
        services.AddSingleton<IValidateOptions<DatabaseSettings>, DatabaseSettingsOptionsValidator>();

        // Register DbContext with resilient SQL Server settings
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var dbSettings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

            options.UseSqlServer(
                dbSettings.ConnectionString,
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: dbSettings.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(30),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(dbSettings.CommandTimeout);
                });

            if (environment.IsDevelopment())
            {
                if (dbSettings.EnableSensitiveDataLogging)
                    options.EnableSensitiveDataLogging();

                if (dbSettings.EnableDetailedErrors)
                    options.EnableDetailedErrors();
            }
        });

        // Register repositories and unit of work
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // JWT settings and token service
        services.Configure<PutZige.Application.Settings.JwtSettings>(configuration.GetSection(PutZige.Application.Settings.JwtSettings.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        // Client info service (depends on IHttpContextAccessor which is provided by the host)
        services.AddScoped<IClientInfoService, ClientInfoService>();
        // Ensure IHttpContextAccessor is available in non-web TFMs
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        // Current user service (depends on IHttpContextAccessor)
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Hashing settings and service
        services.Configure<HashingSettings>(configuration.GetSection(HashingSettings.SectionName));
        services.AddSingleton<IValidator<HashingSettings>, HashingSettingsValidator>();
        services.AddSingleton<IValidateOptions<HashingSettings>, HashingSettingsOptionsValidator>();
        services.AddScoped<IHashingService, HashingService>();

        // Email settings and service
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.AddSingleton<IValidator<EmailSettings>, EmailSettingsValidator>();
        services.AddSingleton<IValidateOptions<EmailSettings>, EmailSettingsOptionsValidator>();
        services.AddScoped<IEmailService, EmailService>();

        // Connection mapping service used by SignalR hubs to avoid static state
        services.AddSingleton<IConnectionMappingService, ConnectionMappingService>();

        // Cookie service for authentication cookie management
        services.AddScoped<ICookieService, CookieService>();

        // Dapper context and repositories
        services.AddScoped<DapperContext>();
        services.AddScoped<IDapperUserRepository, DapperUserRepository>();
        services.AddScoped<PutZige.Domain.Interfaces.IDapperMessageRepository, PutZige.Infrastructure.Repositories.Dapper.DapperMessageRepository>();

        // Register Hangfire background service types
        services.AddScoped<EmailBackgroundService>();

        if (hangfireEnabled)
        {
            services.AddScoped<IBackgroundJobDispatcher, HangfireBackgroundJobDispatcher>();
        }
        else
        {
            services.AddScoped<IBackgroundJobDispatcher, NoOpBackgroundJobDispatcher>();
        }

            // Time abstraction for testability (Singleton - stateless, thread-safe)
            services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}


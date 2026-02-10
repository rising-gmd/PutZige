using FluentValidation.AspNetCore;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using PutZige.API.Configuration;
using PutZige.API.Filters;
using PutZige.API.Middleware;
using PutZige.Application;
using PutZige.Infrastructure;
using PutZige.Application.Settings;
using Serilog;
using PutZige.API.Extensions;

// Bootstrap logger for startup errors
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting PutZige API");
    var builder = WebApplication.CreateBuilder(args);

    // Configure logging
    builder.ConfigureLogging();

    // Services
    builder.Services.AddApiConfiguration();
    builder.Services.AddSwaggerConfiguration();
    builder.Services.AddAuthenticationConfiguration(builder.Configuration);
    builder.Services.AddCookieAuthenticationConfiguration(builder.Configuration);
    builder.Services.AddCorsConfiguration(builder.Configuration, builder.Environment);
    builder.Services.AddApplicationServices();
    builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment, hangfireEnabled: true);
    builder.Services.AddRateLimitingConfiguration(builder.Configuration);
    builder.Services.AddSignalRConfiguration(builder.Configuration);
    builder.Services.AddHangfireConfiguration(builder.Configuration);

    var app = builder.Build();

    app.ConfigureMiddlewarePipeline();

    app.ConfigureHangfireDashboard();

    app.MapSignalRHubs();

    Log.Information("Application starting");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
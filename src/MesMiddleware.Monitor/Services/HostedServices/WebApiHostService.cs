using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MesMiddleware.Monitor.Controllers;
using MesMiddleware.Monitor.Services;
using MesMiddleware.Monitor.Services.Converters;
using MesMiddleware.Monitor.Validation;
using MesMiddleware.Shared.Models;
using MesMiddleware.Shared.Models.LabView;
using FluentValidation;
using System.Net.Http;
using System.Threading.Channels;

namespace MesMiddleware.Monitor.Services.HostedServices;

/// <summary>
/// Background service that hosts ASP.NET Core Web API within WPF application.
/// Allows DeviceSimulator to connect via HTTP REST API (localhost:5100).
/// </summary>
public class WebApiHostService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WebApiHostService> _logger;
    private WebApplication? _webApp;

    public WebApiHostService(
        IServiceProvider serviceProvider,
        ILogger<WebApiHostService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Web API host service starting on http://localhost:5100...");

        try
        {
            var builder = WebApplication.CreateBuilder();

            // Configure Web API listening URL
            builder.WebHost.UseUrls("http://localhost:5100");

            // Add logging (use existing Serilog logger from WPF)
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
            });

            // Add ASP.NET Core services
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Register shared services from WPF DI container
            builder.Services.AddSingleton(_serviceProvider.GetRequiredService<ITokenService>());
            builder.Services.AddSingleton(_serviceProvider.GetRequiredService<IHttpClientFactory>());
            builder.Services.AddSingleton(_serviceProvider.GetRequiredService<Channel<LabViewInspectionRequest>>());

            // Register validator
            builder.Services.AddScoped<IValidator<InspectionRecord>, InspectionDataValidator>();

            // Register LabVIEW data converter
            builder.Services.AddSingleton<ILabViewDataConverter, LabViewDataConverter>();

            _webApp = builder.Build();

            // Configure HTTP request pipeline
            _webApp.UseSwagger();
            _webApp.UseSwaggerUI();
            _webApp.MapControllers();

            _logger.LogInformation("Web API host ready - DeviceSimulator can connect to http://localhost:5100");
            _logger.LogInformation("Swagger UI available at http://localhost:5100/swagger");

            // Run Web API (will block until cancellation)
            await _webApp.RunAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Web API host service is stopping (cancellation requested)");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in Web API host service");
            throw;
        }
        finally
        {
            _logger.LogInformation("Web API host service stopped");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Web API host...");

        if (_webApp != null)
        {
            await _webApp.StopAsync(cancellationToken);
            await _webApp.DisposeAsync();
        }

        await base.StopAsync(cancellationToken);
    }
}

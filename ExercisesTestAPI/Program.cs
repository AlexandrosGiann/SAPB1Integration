using System.Net;
using System.IO;
using ExercisesTestAPI.Options;
using ExercisesTestAPI.Services;
using Serilog;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// Ensure logs folder exists (file sink will fail if folder missing)
Directory.CreateDirectory("logs");

// Initialize Serilog early so startup logs (and any static Log usage) are captured.
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting up");

    var builder = WebApplication.CreateBuilder(args);

    // Make sure the host uses Serilog and reads from the same configuration
    builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddSwaggerGen();

    builder.Services.Configure<SapServiceLayerOptions>(
        builder.Configuration.GetSection("SapServiceLayer"));

    builder.Services.AddHttpClient<ISapServiceLayerClient, SapServiceLayerClient>()
        .ConfigureHttpClient((sp, client) =>
        {
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SapServiceLayerOptions>>().Value;
            client.BaseAddress = new Uri(opt.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opt.RequestTimeoutSeconds);
        })
        .ConfigurePrimaryHttpMessageHandler(sp =>
        {
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SapServiceLayerOptions>>().Value;

            var handler = new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            if (opt.IgnoreTlsErrors)
            {
                handler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            }

            return handler;
        })
        .SetHandlerLifetime(TimeSpan.FromMinutes(
            builder.Configuration.GetSection("SapServiceLayer").GetValue<int>("HandlerLifetimeMinutes", 30)));

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseSerilogRequestLogging(); // logs basic request information via Serilog
    app.MapControllers();
    app.Run();
}
catch (Exception ex)
{
    // Fatal startup error
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.Information("Shutting down");
    Log.CloseAndFlush();
}
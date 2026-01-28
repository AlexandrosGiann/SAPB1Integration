using System.Net;
using System.IO;
using ExercisesTestAPI.Options;
using ExercisesTestAPI.Services;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

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
    builder.Host.UseSerilog((ctx, cfg) =>
        cfg.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext());

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer(); 
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

    // HSTS 
    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
    }

    // Redirect HTTP -> HTTPS
    app.UseHttpsRedirection();

    // Serilog request logging
    app.UseSerilogRequestLogging();

    
    app.Use(async (context, next) =>
    {
        // ---------- General security headers ----------
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"] =
            "geolocation=(), microphone=(), camera=(), payment=(), usb=()";
        context.Response.Headers["X-DNS-Prefetch-Control"] = "off";
        context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";

        // ---------- Path-based tweaks (Swagger / OpenAPI) ----------
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
        var isSwagger = path.StartsWith("/swagger") || path.Contains("api-docs") || path.Contains("openapi");

        
        if (isSwagger)
        {
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "base-uri 'self'; " +
                "object-src 'none'; " +
                "frame-ancestors 'none'; " +
                "form-action 'self'; " +
                "img-src 'self' data:; " +
                "style-src 'self' 'unsafe-inline'; " +
                "script-src 'self' 'unsafe-inline'; " +
                "upgrade-insecure-requests";

            context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, nosnippet, noarchive";
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, max-age=0";
            context.Response.Headers["Pragma"] = "no-cache";
            context.Response.Headers["Expires"] = "0";
        }
        else
        {
            context.Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "base-uri 'self'; " +
                "object-src 'none'; " +
                "frame-ancestors 'none'; " +
                "form-action 'self'; " +
                "upgrade-insecure-requests";
        }

        await next();
    });

    // Swagger
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

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

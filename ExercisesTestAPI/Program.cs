using System.Net;
using ExercisesTestAPI.Options;
using ExercisesTestAPI.Services;

var builder = WebApplication.CreateBuilder(args);

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();

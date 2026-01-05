namespace ExercisesTestAPI.Options;

public sealed class SapServiceLayerOptions
{
    public string BaseUrl { get; set; } = default!;
    public string CompanyDB { get; set; } = default!;
    public string UserName { get; set; } = default!;
    public string Password { get; set; } = default!;

    public bool IgnoreTlsErrors { get; set; } = false;
    public int HandlerLifetimeMinutes { get; set; } = 30;
    public int RequestTimeoutSeconds { get; set; } = 100;
    public string? DefaultWarehouseCode { get; set; }

}

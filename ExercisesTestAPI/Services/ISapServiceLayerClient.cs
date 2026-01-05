using System.Net;
using System.Text.Json;

namespace ExercisesTestAPI.Services;

public interface ISapServiceLayerClient
{
    Task<(HttpStatusCode StatusCode, JsonElement? Body)> GetAsync(string relativeUrl, CancellationToken ct);
    Task<(HttpStatusCode StatusCode, JsonElement? Body)> PostAsync(string relativeUrl, object payload, CancellationToken ct);
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using ExercisesTestAPI.Options;

namespace ExercisesTestAPI.Services;

public sealed class SapServiceLayerClient : ISapServiceLayerClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = null,
        DictionaryKeyPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly SapServiceLayerOptions _opt;
    private readonly ILogger<SapServiceLayerClient> _logger;

    private bool _loggedIn;
    private string? _sessionId;
    private readonly SemaphoreSlim _loginLock = new(1, 1);

    public SapServiceLayerClient(
        HttpClient http,
        IOptions<SapServiceLayerOptions> opt,
        ILogger<SapServiceLayerClient> logger)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;
    }

    public Task<(HttpStatusCode StatusCode, JsonElement? Body)> GetAsync(string relativeUrl, CancellationToken ct)
        => SendWithAuthRetryAsync(
            buildRequest: () => new HttpRequestMessage(HttpMethod.Get, relativeUrl),
            ct);

    public Task<(HttpStatusCode StatusCode, JsonElement? Body)> PostAsync(string relativeUrl, object payload, CancellationToken ct)
        => SendWithAuthRetryAsync(
            buildRequest: () =>
            {
                var req = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
                {
                    Content = JsonContent.Create(payload, options: JsonOpts)
                };
                return req;
            },
            ct);

    private async Task<(HttpStatusCode StatusCode, JsonElement? Body)> SendWithAuthRetryAsync(
        Func<HttpRequestMessage> buildRequest,
        CancellationToken ct)
    {
        
        await EnsureLoggedInAsync(ct);

        using (var req = buildRequest())
        {
            AddSessionHeader(req);
            using var res = await _http.SendAsync(req, ct);

            if (res.StatusCode == HttpStatusCode.Unauthorized || res.StatusCode == HttpStatusCode.Forbidden)
            {
                _logger.LogWarning("Service Layer returned {Status}. Re-login and retry once.", (int)res.StatusCode);
            }
            else
            {
                return await ReadJsonOrThrowAsync(req.Method.Method, req.RequestUri?.ToString() ?? "", res, ct);
            }
        }

        ResetSession();
        await EnsureLoggedInAsync(ct);

        using (var req2 = buildRequest())
        {
            AddSessionHeader(req2);
            using var res2 = await _http.SendAsync(req2, ct);
            return await ReadJsonOrThrowAsync(req2.Method.Method, req2.RequestUri?.ToString() ?? "", res2, ct);
        }
    }

    private void AddSessionHeader(HttpRequestMessage req)
    {
        if (!string.IsNullOrWhiteSpace(_sessionId))
        {
            req.Headers.Remove("B1SESSION");
            req.Headers.TryAddWithoutValidation("B1SESSION", _sessionId);
        }

        req.Headers.Remove("Accept");
        req.Headers.TryAddWithoutValidation("Accept", "application/json");
    }

    private void ResetSession()
    {
        _loggedIn = false;
        _sessionId = null;
    }

    private async Task EnsureLoggedInAsync(CancellationToken ct)
    {
        if (_loggedIn) return;

        await _loginLock.WaitAsync(ct);
        try
        {
            if (_loggedIn) return;

            var loginPayload = new
            {
                CompanyDB = _opt.CompanyDB,
                UserName = _opt.UserName,
                Password = _opt.Password
            };

            using var res = await _http.PostAsJsonAsync("Login", loginPayload, JsonOpts, ct);
            var body = await res.Content.ReadAsStringAsync(ct);

            if (!res.IsSuccessStatusCode)
            {
                var (sapCode, sapMsg) = TryExtractSapError(body);
                throw new SapServiceLayerException(
                    $"Service Layer login failed. Status={(int)res.StatusCode} {res.StatusCode}. " +
                    $"{(sapCode is null ? "" : $"SAPCode={sapCode}. ")}" +
                    $"{(string.IsNullOrWhiteSpace(sapMsg) ? "" : $"SAP='{sapMsg}'. ")}" +
                    $"Body={Trim(body, 2000)}",
                    res.StatusCode,
                    body
                );
            }

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.TryGetProperty("SessionId", out var sid) &&
                        sid.ValueKind == JsonValueKind.String)
                    {
                        _sessionId = sid.GetString();
                    }
                }
                catch
                {
                    Console.WriteLine();
                }
            }

            _loggedIn = true;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task<(HttpStatusCode StatusCode, JsonElement? Body)> ReadJsonOrThrowAsync(
        string method,
        string url,
        HttpResponseMessage res,
        CancellationToken ct)
    {
        var text = await res.Content.ReadAsStringAsync(ct);

        if (res.IsSuccessStatusCode)
        {
            if (string.IsNullOrWhiteSpace(text))
                return (res.StatusCode, null);

            try
            {
                using var doc = JsonDocument.Parse(text);
                return (res.StatusCode, doc.RootElement.Clone());
            }
            catch
            {
                return (res.StatusCode, null);
            }
        }

        var (sapCode, sapMsg) = TryExtractSapError(text);

        throw new SapServiceLayerException(
            $"Service Layer request failed: {method} {url}. Status={(int)res.StatusCode} {res.StatusCode}. " +
            $"{(sapCode is null ? "" : $"SAPCode={sapCode}. ")}" +
            $"{(string.IsNullOrWhiteSpace(sapMsg) ? "" : $"SAP='{sapMsg}'. ")}" +
            $"Body={Trim(text, 4000)}",
            res.StatusCode,
            text
        );
    }

    private static (int? sapCode, string? sapMsg) TryExtractSapError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("error", out var err))
                return (null, null);

            int? code = null;
            if (err.TryGetProperty("code", out var c))
            {
                if (c.ValueKind == JsonValueKind.Number && c.TryGetInt32(out var ci))
                    code = ci;
                else if (c.ValueKind == JsonValueKind.String && int.TryParse(c.GetString(), out var cs))
                    code = cs;
            }

            string? msg = null;
            if (err.TryGetProperty("message", out var m) &&
                m.TryGetProperty("value", out var v) &&
                v.ValueKind == JsonValueKind.String)
            {
                msg = v.GetString();
            }

            return (code, msg);
        }
        catch
        {
            return (null, null);
        }
    }

    private static string Trim(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Length <= max ? s : s[..max];
    }
}

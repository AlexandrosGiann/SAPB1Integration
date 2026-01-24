using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ExercisesTestAPI.Dtos.SalesOrders;
using ExercisesTestAPI.Options;
using ExercisesTestAPI.Services;
using Serilog;

namespace ExercisesTestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class SalesOrdersController : ControllerBase
{
    private readonly ISapServiceLayerClient _sap;
    private readonly SapServiceLayerOptions _opt;

    public SalesOrdersController(ISapServiceLayerClient sap, IOptions<SapServiceLayerOptions> opt)
    {
        _sap = sap;
        _opt = opt.Value;
    }

    [HttpPost]
    public async Task<ActionResult<CreateSalesOrderResponse>> Create(
        [FromBody] CreateSalesOrderRequest req,
        CancellationToken ct)
    {
        // Log incoming request via Serilog (structured). Wrap in try/catch to avoid failing request on logging errors.
        try
        {
            Log.ForContext<SalesOrdersController>()
               .Information("CreateSalesOrder request {@Request}", req);
        }
        catch (Exception ex)
        {
            Log.ForContext<SalesOrdersController>()
               .Warning(ex, "Failed to log incoming CreateSalesOrder request.");
        }

        if (string.IsNullOrWhiteSpace(req.CardCode))
            return BadRequest("CardCode είναι υποχρεωτικό.");

        if (req.Lines is null || req.Lines.Count == 0)
            return BadRequest("Πρέπει να υπάρχει τουλάχιστον μία γραμμή (Lines).");

        foreach (var l in req.Lines)
        {
            if (string.IsNullOrWhiteSpace(l.ItemCode))
                return BadRequest("Κάθε γραμμή πρέπει να έχει ItemCode.");

            if (l.Quantity <= 0)
                return BadRequest("Κάθε γραμμή πρέπει να έχει Quantity > 0.");
        }

        if (!await BusinessPartnerExistsAsync(req.CardCode, ct))
            return BadRequest($"Το CardCode '{req.CardCode}' δεν υπάρχει στο SAP.");

        foreach (var line in req.Lines)
        {
            if (!await ItemExistsAsync(line.ItemCode, ct))
                return BadRequest($"Το ItemCode '{line.ItemCode}' δεν υπάρχει στο SAP.");
        }

        var whs = _opt.DefaultWarehouseCode;
        if (string.IsNullOrWhiteSpace(whs))
            return BadRequest("Λείπει SapServiceLayer:DefaultWarehouseCode στο appsettings.json.");

        var docDate = DateTime.Today;
        var docDateStr = docDate.ToString("yyyy-MM-dd");

        var payload = new
        {
            CardCode = req.CardCode,
            DocDate = docDateStr,
            DocDueDate = docDateStr,
            DocumentLines = req.Lines.Select(l => new
            {
                ItemCode = l.ItemCode,
                Quantity = l.Quantity,
                UnitPrice = l.Price,
                WarehouseCode = whs
            }).ToArray()
        };

        var (_, body) = await _sap.PostAsync("Orders", payload, ct);

        int docEntry = 0;
        int docNum = 0;

        if (body.HasValue)
        {
            if (body.Value.TryGetProperty("DocEntry", out var de) && de.ValueKind == System.Text.Json.JsonValueKind.Number)
                docEntry = de.GetInt32();

            if (body.Value.TryGetProperty("DocNum", out var dn) && dn.ValueKind == System.Text.Json.JsonValueKind.Number)
                docNum = dn.GetInt32();
        }

        return Ok(new CreateSalesOrderResponse
        {
            DocEntry = docEntry,
            DocNum = docNum
        });
    }

    private async Task<bool> BusinessPartnerExistsAsync(string cardCode, CancellationToken ct)
    {
        try
        {
            var url = $"BusinessPartners('{EscapeODataKey(cardCode)}')";
            await _sap.GetAsync(url, ct);
            return true;
        }
        catch (SapServiceLayerException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private async Task<bool> ItemExistsAsync(string itemCode, CancellationToken ct)
    {
        try
        {
            var url = $"Items('{EscapeODataKey(itemCode)}')";
            await _sap.GetAsync(url, ct);
            return true;
        }
        catch (SapServiceLayerException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    private static string EscapeODataKey(string key) => key.Replace("'", "''");
}
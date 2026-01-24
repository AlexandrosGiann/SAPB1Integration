using ExercisesTestAPI.Dtos.Items;
using ExercisesTestAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System.Net;
using System.Text.RegularExpressions;

namespace ExercisesTestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ItemsController : ControllerBase
{
    private readonly ISapServiceLayerClient _sap;

    public ItemsController(ISapServiceLayerClient sap)
    {
        _sap = sap;
    }

    [HttpPost]
    public async Task<ActionResult<CreateItemResponse>> Create(
        [FromBody] CreateItemRequest req,
        CancellationToken ct)
    {
        // Log incoming request via Serilog (structured). Wrap in try/catch to avoid failing request on logging errors.
        try
        {
            Log.ForContext<ItemsController>()
               .Information("CreateItem request {@Request}", req);
        }
        catch (Exception ex)
        {
            Log.ForContext<ItemsController>()
               .Warning(ex, "Failed to log incoming CreateItem request.");
        }

        req.ItemCode = Regex.Replace(req.ItemCode, @"[^a-zA-Z0-9]", "");
        if (string.IsNullOrWhiteSpace(req.ItemCode))
            return BadRequest("ItemCode είναι υποχρεωτικό.");

        req.ItemName = Regex.Replace(req.ItemName, @"[^a-zA-Z0-9\s]", "");
        if (string.IsNullOrWhiteSpace(req.ItemName))
            return BadRequest("ItemName είναι υποχρεωτικό.");

        if (await ItemExistsAsync(req.ItemCode, ct))
            return Conflict($"Item με ItemCode '{req.ItemCode}' υπάρχει ήδη.");

        var payload = new
        {
            ItemCode = req.ItemCode,
            ItemName = req.ItemName,
            ItemsGroupCode = req.ItemsGroupCode,
            InventoryItem = req.InventoryItem,
            SalesItem = "tYES"
        };


        await _sap.PostAsync("Items", payload, ct);

        return Ok(new CreateItemResponse
        {
            ItemCode = req.ItemCode,
            ItemName = req.ItemName
        });
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
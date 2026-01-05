using System.Net;
using Microsoft.AspNetCore.Mvc;
using ExercisesTestAPI.Dtos.Items;
using ExercisesTestAPI.Services;

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
        if (string.IsNullOrWhiteSpace(req.ItemCode))
            return BadRequest("ItemCode είναι υποχρεωτικό.");

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

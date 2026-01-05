using System.Net;
using Microsoft.AspNetCore.Mvc;
using ExercisesTestAPI.Dtos.BusinessPartners;
using ExercisesTestAPI.Services;

namespace ExercisesTestAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class BusinessPartnersController : ControllerBase
{
    private static readonly HashSet<string> AllowedCardTypes = new(StringComparer.Ordinal)
    {
        "cCustomer",
        "cSupplier",
        "cLid"
    };

    private readonly ISapServiceLayerClient _sap;

    public BusinessPartnersController(ISapServiceLayerClient sap)
    {
        _sap = sap;
    }

    [HttpPost]
    public async Task<ActionResult<CreateBusinessPartnerResponse>> Create(
        [FromBody] CreateBusinessPartnerRequest req,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.CardCode))
            return BadRequest("CardCode είναι υποχρεωτικό.");

        if (string.IsNullOrWhiteSpace(req.CardName))
            return BadRequest("CardName είναι υποχρεωτικό.");

        req.CardType ??= "cCustomer";

        if (!AllowedCardTypes.Contains(req.CardType))
            return BadRequest("CardType πρέπει να είναι ένα από: cCustomer, cSupplier, cLid.");

        if (await BusinessPartnerExistsAsync(req.CardCode, ct))
            return Conflict($"Business Partner με CardCode '{req.CardCode}' υπάρχει ήδη.");

        var payload = new
        {
            CardCode = req.CardCode,
            CardName = req.CardName,
            CardType = req.CardType,
            FederalTaxID = req.FederalTaxID,
            BilltoDefault = req.BilltoDefault,
            ShipToDefault = req.ShipToDefault
        };

        await _sap.PostAsync("BusinessPartners", payload, ct);

        return Ok(new CreateBusinessPartnerResponse
        {
            CardCode = req.CardCode,
            CardName = req.CardName
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

    private static string EscapeODataKey(string key) => key.Replace("'", "''");
}

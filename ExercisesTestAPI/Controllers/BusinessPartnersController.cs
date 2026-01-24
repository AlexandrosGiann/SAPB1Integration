using System.Net;
using Microsoft.AspNetCore.Mvc;
using ExercisesTestAPI.Dtos.BusinessPartners;
using ExercisesTestAPI.Services;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;

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

    private static readonly HashSet<string> AllowedAddressTypes = new(StringComparer.Ordinal)
    {
        "bo_ShipTo",
        "bo_BillTo"
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
        // Write incoming request to Serilog (structured). Wrap in try/catch to avoid failing the request if logging fails.
        try
        {
            Log.ForContext<BusinessPartnersController>()
               .Information("CreateBusinessPartner request {@Request}", req);
        }
        catch (Exception ex)
        {
            Log.ForContext<BusinessPartnersController>()
               .Warning(ex, "Failed to log incoming CreateBusinessPartner request.");
        }

        req.CardCode = Regex.Replace(req.CardCode, @"[^a-zA-Z0-9]", "");
        if (string.IsNullOrWhiteSpace(req.CardCode))
            return BadRequest("CardCode είναι υποχρεωτικό.");

        req.CardName = Regex.Replace(req.CardName, @"[^a-zA-Z0-9 ]", "");
        if (string.IsNullOrWhiteSpace(req.CardName))
            return BadRequest("CardName είναι υποχρεωτικό.");

        req.CardType ??= "cCustomer";

        if (!AllowedCardTypes.Contains(req.CardType))
            return BadRequest("CardType πρέπει να είναι ένα από: cCustomer, cSupplier, cLid.");

        foreach (var address in req.BPAddresses)
        {
            if (!AllowedAddressTypes.Contains(address.AddressType))
                return BadRequest("Κάθε AddressType πρέπει να είναι ένα από: bo_ShipTo, bo_BillTo.");
        }

        //if (req.ContactEmployees.Count == 0)
        //    return BadRequest("Πρέπει να υπάρχει τουλάχιστον ένα ContactEmployee.");

        if (await BusinessPartnerExistsAsync(req.CardCode, ct))
            return Conflict($"Business Partner με CardCode '{req.CardCode}' υπάρχει ήδη.");

        var payload = new
        {
            CardCode = req.CardCode,
            CardName = req.CardName,
            CardType = req.CardType,
            FederalTaxID = req.FederalTaxID,
            BilltoDefault = req.BilltoDefault,
            ShipToDefault = req.ShipToDefault,
            BPAddresses = req.BPAddresses,
            ContactEmployees = req.ContactEmployees
        };

        await _sap.PostAsync("BusinessPartners", payload, ct);
        if (req.BPAddresses.Count > 0)
        {
            bool billToFound = false;

            foreach (var address in req.BPAddresses)
            {
                if (address.AddressType == "bo_BillTo")
                {
                    billToFound = true;
                    break;
                }
            }
            if (!billToFound)
            {
                return Ok(new CreateBusinessPartnertResponse2
                {
                    CardCode = req.CardCode,
                    CardName = req.CardName,
                    BillNotFound = "Καλό είναι να υπάρχει τουλάχιστον ένα bo_billTo στα AdressType του BPAdressess."
                });
            }
        }
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
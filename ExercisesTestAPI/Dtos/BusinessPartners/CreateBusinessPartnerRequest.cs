namespace ExercisesTestAPI.Dtos.BusinessPartners;

public sealed class CreateBusinessPartnerRequest
{
    public string CardCode { get; set; } = default!;
    public string CardName { get; set; } = default!;
    public string CardType { get; set; } = "cCustomer";
    public string? FederalTaxID { get; set; } = default!;
    public string? BilltoDefault { get; set; } = default!;
    public string? ShipToDefault { get; set; } = default!;
}

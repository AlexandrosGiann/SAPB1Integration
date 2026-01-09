namespace ExercisesTestAPI.Dtos.BusinessPartners;

public sealed class BPAddressDto
{
    public string AddressName { get; set; } = default!;
    public string AddressType { get; set; } = "bo_BillTo";
    public string? Street { get; set; } = default!;
    public string? City { get; set; } = default!;
    public string? ZipCode { get; set; } = default!;
    public string Country { get; set; } = "GB";
}

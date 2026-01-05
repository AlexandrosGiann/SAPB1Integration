namespace ExercisesTestAPI.Dtos.BusinessPartners;

public sealed class AddressDto
{
    public string Street { get; set; } = default!;
    public string City { get; set; } = default!;
    public string ZipCode { get; set; } = default!;
    public string Country { get; set; } = default!;
    public string? State { get; set; }
}

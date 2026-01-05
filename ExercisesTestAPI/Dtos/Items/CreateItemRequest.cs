namespace ExercisesTestAPI.Dtos.Items;

public sealed class CreateItemRequest
{
    public string ItemCode { get; set; } = default!;
    public string ItemName { get; set; } = default!;
    public int ItemsGroupCode { get; set; } = 100;
    public string? InventoryItem { get; set; } = "tYES";
}

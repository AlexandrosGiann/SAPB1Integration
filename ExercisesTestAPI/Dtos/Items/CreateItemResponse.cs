namespace ExercisesTestAPI.Dtos.Items;

public sealed class CreateItemResponse
{
    public string ItemCode { get; set; } = default!;
    public string ItemName { get; set; } = default!;
}

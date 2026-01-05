namespace ExercisesTestAPI.Dtos.SalesOrders;

public sealed class SalesOrderLineDto
{
    public string ItemCode { get; set; } = default!;
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
}

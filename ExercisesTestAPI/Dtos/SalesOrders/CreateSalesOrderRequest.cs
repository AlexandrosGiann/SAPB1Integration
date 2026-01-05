namespace ExercisesTestAPI.Dtos.SalesOrders;

public sealed class CreateSalesOrderRequest
{
    public string CardCode { get; set; } = default!;
    public List<SalesOrderLineDto> Lines { get; set; } = new();
}

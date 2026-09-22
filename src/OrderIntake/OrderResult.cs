namespace OrderIntake;

public enum OrderStatus
{
    Accepted,
    Rejected
}

public class OrderResult
{
    public OrderStatus Status { get; init; }
    public Order? Order { get; init; }
    public List<ValidationError> Errors { get; init; } = new();
}

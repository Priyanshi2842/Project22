namespace OrderIntake;

public class Order
{
    public string OrderId { get; init; } = string.Empty;
    public string PatientId { get; init; } = string.Empty;
    public string SpecimenId { get; init; } = string.Empty;
    public string SpecimenType { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public DateOnly CollectionDate { get; init; }
    public List<string> RequestedTests { get; init; } = new();
}
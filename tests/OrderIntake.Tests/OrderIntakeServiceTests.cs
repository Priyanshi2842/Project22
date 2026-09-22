using OrderIntake;

namespace OrderIntake.Tests;

public class OrderIntakeServiceTests
{
    private readonly OrderIntakeService _service = new();

    [Fact]
    public void AcceptedOrder_NormalizesValuesAndIgnoresUnknownFields()
    {
        string json = """
        {
            "orderId": "ORD-1005",
            "patientId": "PAT-505",
            "specimenId": "SP-9005",
            "specimenType": "bLoOd",
            "priority": "URGENT",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose", "CompleteBloodCount"],
            "senderNote": "ignore me"
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Accepted, result.Status);
        Assert.NotNull(result.Order);
        Assert.Empty(result.Errors);

        Assert.Equal("ORD-1005", result.Order!.OrderId);
        Assert.Equal("PAT-505", result.Order.PatientId);
        Assert.Equal("SP-9005", result.Order.SpecimenId);
        Assert.Equal("Blood", result.Order.SpecimenType);
        Assert.Equal("Urgent", result.Order.Priority);
        Assert.Equal(new DateOnly(2026, 9, 18), result.Order.CollectionDate);

        Assert.Equal(
            new[] { "Glucose", "CompleteBloodCount" },
            result.Order.RequestedTests);
    }

    [Fact]
    public void AllErrors_AreReturnedTogether()
    {
        string json = """
        {
            "orderId": "   ",
            "patientId": "PAT-505",
            "specimenId": "SP-9005",
            "specimenType": "Plasma",
            "priority": "Emergency",
            "collectionDate": "2026-02-30",
            "requestedTests": []
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Null(result.Order);

        var actual = result.Errors
            .Select(error => (error.Field, error.Code))
            .ToHashSet();

        var expected = new HashSet<(string, string)>
        {
            ("orderId", "REQUIRED"),
            ("specimenType", "INVALID_VALUE"),
            ("priority", "INVALID_VALUE"),
            ("collectionDate", "INVALID_FORMAT"),
            ("requestedTests", "INVALID_VALUE")
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderId_Exactly20Characters_IsAccepted()
    {
        string orderId = new string('A', 20);

        string json = $$"""
        {
            "orderId": "{{orderId}}",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose"]
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Accepted, result.Status);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void OrderId_21Characters_ReturnsMaxLength()
    {
        string orderId = new string('A', 21);

        string json = $$"""
        {
            "orderId": "{{orderId}}",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose"]
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);

        Assert.Contains(
            result.Errors,
            e => e.Field == "orderId" &&
                 e.Code == "MAX_LENGTH");
    }

    [Fact]
    public void InvalidCollectionDate_IsRejected()
    {
        string json = ValidJsonWithDate("\"2026-02-30\"");

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);

        Assert.Contains(
            result.Errors,
            e => e.Field == "collectionDate" &&
                 e.Code == "INVALID_FORMAT");
    }

    [Fact]
    public void IncorrectCollectionDateFormat_IsRejected()
    {
        string json = ValidJsonWithDate("\"20-09-2026\"");

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);

        Assert.Contains(
            result.Errors,
            e => e.Field == "collectionDate" &&
                 e.Code == "INVALID_FORMAT");
    }

    [Fact]
    public void FutureCollectionDate_IsRejected()
    {
        DateOnly futureDate =
            DateOnly.FromDateTime(DateTime.Today).AddDays(1);

        string json =
            ValidJsonWithDate($"\"{futureDate:yyyy-MM-dd}\"");

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);

        Assert.Contains(
            result.Errors,
            e => e.Field == "collectionDate" &&
                 e.Code == "FUTURE_DATE");
    }

    [Fact]
    public void EmptyRequestedTests_IsRejected()
    {
        string json = """
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": "2026-09-18",
            "requestedTests": []
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);

        Assert.Contains(
            result.Errors,
            e => e.Field == "requestedTests" &&
                 e.Code == "INVALID_VALUE");
    }

    [Fact]
    public void RequestedTests_DuplicateIgnoringCase_IsRejected()
    {
        string json = """
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": "2026-09-18",
            "requestedTests": ["Glucose", "glucose"]
        }
        """;

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);

        Assert.Contains(
            result.Errors,
            e => e.Field == "requestedTests" &&
                 e.Code == "DUPLICATE");
    }

    [Fact]
    public void BrokenJson_ReturnsSingleMalformedInputError()
    {
        string json = """{"orderId":"ORD-1","patientId":""";

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    [Fact]
    public void TopLevelArray_ReturnsSingleMalformedInputError()
    {
        string json = """[]""";

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Single(result.Errors);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    [Fact]
    public void WrongRecognizedFieldType_ReturnsSingleMalformedInputError()
    {
        string json = """{"orderId":123}""";

        OrderResult result = _service.Process(json);

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Single(result.Errors);

        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    private static string ValidJsonWithDate(string collectionDate)
    {
        return $$"""
        {
            "orderId": "ORD-1",
            "patientId": "PAT-1",
            "specimenId": "SP-1",
            "specimenType": "Blood",
            "priority": "Routine",
            "collectionDate": {{collectionDate}},
            "requestedTests": ["Glucose"]
        }
        """;
    }
} 
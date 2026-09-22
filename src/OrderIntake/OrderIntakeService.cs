using System.Globalization;
using System.Text.Json;

namespace OrderIntake;

public class OrderIntakeService
{
    public OrderResult Process(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return MalformedResult();
        }

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return MalformedResult();
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return MalformedResult();
            }

            var root = document.RootElement;

            if (!HasCompatibleTypes(root))
            {
                return MalformedResult();
            }

            var errors = new List<ValidationError>();

            string? orderId = GetString(root, "orderId");
            string? patientId = GetString(root, "patientId");
            string? specimenId = GetString(root, "specimenId");
            string? specimenType = GetString(root, "specimenType");
            string? priority = GetString(root, "priority");
            string? collectionDate = GetString(root, "collectionDate");

            ValidateId(orderId, "orderId", errors);
            ValidateId(patientId, "patientId", errors);
            ValidateId(specimenId, "specimenId", errors);

            string? normalizedSpecimenType =
                ValidateSpecimenType(specimenType, errors);

            string? normalizedPriority =
                ValidatePriority(priority, errors);

            DateOnly? parsedDate =
                ValidateCollectionDate(collectionDate, errors);

            List<string>? requestedTests =
                ValidateRequestedTests(root, errors);

            if (errors.Count > 0)
            {
                return new OrderResult
                {
                    Status = OrderStatus.Rejected,
                    Order = null,
                    Errors = errors
                };
            }

            var order = new Order
            {
                OrderId = orderId!,
                PatientId = patientId!,
                SpecimenId = specimenId!,
                SpecimenType = normalizedSpecimenType!,
                Priority = normalizedPriority!,
                CollectionDate = parsedDate!.Value,
                RequestedTests = requestedTests!
            };

            return new OrderResult
            {
                Status = OrderStatus.Accepted,
                Order = order,
                Errors = new List<ValidationError>()
            };
        }
    }

    private static bool HasCompatibleTypes(JsonElement root)
    {
        string[] recognizedFields =
        {
            "orderId",
            "patientId",
            "specimenId",
            "specimenType",
            "priority",
            "collectionDate"
        };

        foreach (string field in recognizedFields)
        {
            if (!root.TryGetProperty(field, out JsonElement value))
            {
                continue;
            }

            if (value.ValueKind != JsonValueKind.String &&
                value.ValueKind != JsonValueKind.Null)
            {
                return false;
            }
        }

        if (root.TryGetProperty("requestedTests", out JsonElement tests))
        {
            if (tests.ValueKind != JsonValueKind.Array &&
                tests.ValueKind != JsonValueKind.Null)
            {
                return false;
            }

            if (tests.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in tests.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static string? GetString(JsonElement root, string property)
    {
        if (!root.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static void ValidateId(
        string? value,
        string field,
        List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Field = field,
                Code = "REQUIRED",
                Message = $"{field} is required."
            });

            return;
        }

        if (value.Length > 20)
        {
            errors.Add(new ValidationError
            {
                Field = field,
                Code = "MAX_LENGTH",
                Message = $"{field} must not exceed 20 characters."
            });
        }
    }

    private static string? ValidateSpecimenType(
        string? value,
        List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Field = "specimenType",
                Code = "REQUIRED",
                Message = "specimenType is required."
            });

            return null;
        }

        string? normalized = value.Trim().ToLowerInvariant() switch
        {
            "blood" => "Blood",
            "urine" => "Urine",
            "tissue" => "Tissue",
            "saliva" => "Saliva",
            _ => null
        };

        if (normalized is null)
        {
            errors.Add(new ValidationError
            {
                Field = "specimenType",
                Code = "INVALID_VALUE",
                Message = "specimenType must be Blood, Urine, Tissue or Saliva."
            });
        }

        return normalized;
    }

    private static string? ValidatePriority(
        string? value,
        List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Field = "priority",
                Code = "REQUIRED",
                Message = "priority is required."
            });

            return null;
        }

        string? normalized = value.Trim().ToLowerInvariant() switch
        {
            "routine" => "Routine",
            "urgent" => "Urgent",
            _ => null
        };

        if (normalized is null)
        {
            errors.Add(new ValidationError
            {
                Field = "priority",
                Code = "INVALID_VALUE",
                Message = "priority must be Routine or Urgent."
            });
        }

        return normalized;
    }

    private static DateOnly? ValidateCollectionDate(
        string? value,
        List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError
            {
                Field = "collectionDate",
                Code = "REQUIRED",
                Message = "collectionDate is required."
            });

            return null;
        }

        if (!DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateOnly date))
        {
            errors.Add(new ValidationError
            {
                Field = "collectionDate",
                Code = "INVALID_FORMAT",
                Message = "collectionDate must be a real date in yyyy-MM-dd format."
            });

            return null;
        }

        if (date > DateOnly.FromDateTime(DateTime.Today))
        {
            errors.Add(new ValidationError
            {
                Field = "collectionDate",
                Code = "FUTURE_DATE",
                Message = "collectionDate must not be in the future."
            });
        }

        return date;
    }

    private static List<string>? ValidateRequestedTests(
        JsonElement root,
        List<ValidationError> errors)
    {
        if (!root.TryGetProperty("requestedTests", out JsonElement value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            errors.Add(new ValidationError
            {
                Field = "requestedTests",
                Code = "REQUIRED",
                Message = "requestedTests is required."
            });

            return null;
        }

        var tests = value
            .EnumerateArray()
            .Select(x => x.GetString()!)
            .ToList();

        if (tests.Count == 0)
        {
            errors.Add(new ValidationError
            {
                Field = "requestedTests",
                Code = "INVALID_VALUE",
                Message = "requestedTests must contain at least one item."
            });

            return tests;
        }

        if (tests.Any(string.IsNullOrWhiteSpace))
        {
            errors.Add(new ValidationError
            {
                Field = "requestedTests",
                Code = "INVALID_VALUE",
                Message = "requestedTests must not contain empty items."
            });
        }

        bool duplicateExists = tests
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1);

        if (duplicateExists)
        {
            errors.Add(new ValidationError
            {
                Field = "requestedTests",
                Code = "DUPLICATE",
                Message = "requestedTests must not contain duplicate names ignoring case."
            });
        }

        return tests;
    }

    private static OrderResult MalformedResult()
    {
        return new OrderResult
        {
            Status = OrderStatus.Rejected,
            Order = null,
            Errors = new List<ValidationError>
            {
                new ValidationError
                {
                    Field = "$",
                    Code = "MALFORMED_INPUT",
                    Message = "Input must be a valid JSON object with compatible recognized field types."
                }
            }
        };
    }
}
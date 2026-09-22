# Project22
# Laboratory Order Intake and Input Validation Service

## Overview

This project implements a C# laboratory order intake and validation service.

The service accepts one laboratory order as a JSON string, validates the recognized fields, and returns an `OrderResult`.

The result contains:

- `Accepted` or `Rejected` status
- The validated `Order` when accepted
- A list of validation errors when rejected

The project does not use a console application, web API, database, UI, external API, or validation framework.

## Project Structure

```text
Project22/
├── src/
│   └── OrderIntake/
│       ├── Order.cs
│       ├── OrderResult.cs
│       ├── ValidationError.cs
│       └── OrderIntakeService.cs
│
├── tests/
│   └── OrderIntake.Tests/
│       └── OrderIntakeServiceTests.cs
│
├── Project22.sln
└── README.md
Requirements
.NET 10 SDK
C#
xUnit for automated tests
Build

From the repository root:

dotnet build
Run Tests

From the repository root:

dotnet test

The test suite covers:

Accepted orders with case-insensitive specimen type and priority
Collection of multiple validation errors
ID length validation
Collection date validation
Requested test validation and duplicate detection
Malformed JSON handling
Design Summary

The main public processing method is:

OrderResult Process(string json);

The processing flow is:

Check for null, empty, or whitespace input.
Parse the JSON.
Verify that the root is a JSON object.
Verify compatible JSON types for recognized fields.
Validate each order field.
Collect all validation errors rather than stopping at the first error.
Normalize specimenType and priority.
Convert collectionDate into DateOnly.
Build and return the strongly typed Order when validation succeeds.

Unknown fields are ignored.

Validation Rules
IDs

orderId, patientId, and specimenId:

Required
Must not be empty or whitespace
Maximum 20 characters
Specimen Type

Accepted values:

Blood
Urine
Tissue
Saliva

Comparison is case-insensitive and the stored value is normalized to fixed casing.

Priority

Accepted values:

Routine
Urgent

Comparison is case-insensitive and the stored value is normalized to fixed casing.

Collection Date

The date must:

Be present
Use exactly yyyy-MM-dd
Be a real calendar date
Not be after today's date

The date is stored as a DateOnly.

Requested Tests

The field must:

Be present
Contain at least one item
Not contain empty items
Not contain duplicate names when compared case-insensitively

The original casing of test names is preserved.

Malformed Input

Malformed input produces one error:

Field: $
Code: MALFORMED_INPUT

This applies to cases such as:

Invalid JSON
Top-level JSON array
Top-level JSON string
Incompatible types for recognized fields
Null, empty, or whitespace-only input

The service does not allow these cases to result in an unhandled exception.

Assumptions and Limitations
Input values are trimmed for specimenType and priority during normalization.
Unknown JSON fields are ignored as specified.
Error ordering is not relied upon by the tests.
No persistence or external communication is performed.
The service operates entirely on plain C# objects.
AI Use

GitHub Copilot was used as an assistance tool during development.

One suggestion was reviewed and adapted rather than copied blindly. The validation logic was checked against the assessment requirements, particularly the distinction between malformed JSON and valid JSON with field-level validation errors.

All submitted code was reviewed and tested before submission.

Test Verification

Before submission, the following commands were run from the repository root:

dotnet build
dotnet test

Both commands must complete successfully before submission.



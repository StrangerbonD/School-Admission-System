using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Models;

public record CreateHoldResponse(
    [property: JsonPropertyName("holdId")] Guid HoldId,
    [property: JsonPropertyName("classId")] Guid ClassId,
    [property: JsonPropertyName("studentId")] string StudentId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expiresAt")] DateTime ExpiresAt,
    [property: JsonPropertyName("serverTime")] DateTime ServerTime
);

public record HoldDetailsResponse(
    [property: JsonPropertyName("holdId")] Guid HoldId,
    [property: JsonPropertyName("classId")] Guid ClassId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expiresAt")] DateTime ExpiresAt,
    [property: JsonPropertyName("serverTime")] DateTime ServerTime
);

public record ConfirmHoldResponse(
    [property: JsonPropertyName("holdId")] Guid HoldId,
    [property: JsonPropertyName("status")] string Status
);

public record StudentHoldSummaryResponse(
    [property: JsonPropertyName("holdId")] Guid HoldId,
    [property: JsonPropertyName("classId")] Guid ClassId,
    [property: JsonPropertyName("className")] string ClassName,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expiresAt")] DateTime ExpiresAt,
    [property: JsonPropertyName("serverTime")] DateTime ServerTime
);

public class HoldTimingResult
{
    public DateTime ExpiresAt { get; set; }
    public DateTime ServerTime { get; set; }
}

public class HoldCheckResult
{
    public bool AlreadyApplied { get; set; }
    public int ActiveSeats { get; set; }
}

public class HoldDetailsQueryResult
{
    public Guid HoldId { get; set; }
    public Guid ClassId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime ServerTime { get; set; }
}

public class StudentHoldQueryResult
{
    public Guid HoldId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime ServerTime { get; set; }
}

public class HoldDiagnosticResult
{
    public Guid HoldId { get; set; }
    public Guid ClassId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string RawStatus { get; set; } = string.Empty;
    public string ComputedStatus { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime ServerTime { get; set; }
}

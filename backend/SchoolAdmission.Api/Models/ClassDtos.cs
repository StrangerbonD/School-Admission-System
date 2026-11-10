using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Models;

public record CreateClassRequest(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("totalSeats")] int? TotalSeats
);

public record ClassResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("totalSeats")] int TotalSeats
);

public record ClassSummaryResponse(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("totalSeats")] int TotalSeats,
    [property: JsonPropertyName("heldSeats")] int HeldSeats,
    [property: JsonPropertyName("confirmedSeats")] int ConfirmedSeats,
    [property: JsonPropertyName("availableSeats")] int AvailableSeats
);

public class ClassSummaryQueryResult
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public int Held { get; set; }
    public int Confirmed { get; set; }
}

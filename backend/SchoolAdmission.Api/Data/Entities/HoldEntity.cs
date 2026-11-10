namespace SchoolAdmission.Api.Data.Entities;

public class HoldEntity
{
    public Guid Id { get; set; }
    public Guid ClassId { get; set; }
    public string StudentId { get; set; } = string.Empty;
    public string Status { get; set; } = HoldStatus.Held;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public ClassEntity? Class { get; set; }
}

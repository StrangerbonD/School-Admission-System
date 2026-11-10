namespace SchoolAdmission.Api.Data.Entities;

public class ClassEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalSeats { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<HoldEntity> Holds { get; set; } = new();
}

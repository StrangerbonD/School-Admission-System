using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Data.Entities;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class ClassService : IClassService
{
    private readonly AppDbContext _db;

    public ClassService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ClassResponse> CreateClassAsync(CreateClassRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ValidationException("Class name is required and cannot be empty.");
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length > 200)
        {
            throw new ValidationException("Class name must not exceed 200 characters.");
        }

        if (!request.TotalSeats.HasValue)
        {
            throw new ValidationException("totalSeats is required.");
        }

        if (request.TotalSeats.Value < 1 || request.TotalSeats.Value > 100000)
        {
            throw new ValidationException("totalSeats must be an integer between 1 and 100000.");
        }

        var entity = new ClassEntity
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            TotalSeats = request.TotalSeats.Value,
            CreatedAt = DateTime.UtcNow
        };

        _db.Classes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return new ClassResponse(entity.Id, entity.Name, entity.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassSummaryResponse>> GetAllClassesAsync(CancellationToken cancellationToken = default)
    {
        var results = await _db.Database.SqlQueryRaw<ClassSummaryQueryResult>(@"
            SELECT c.id AS ""Id"",
                   c.name AS ""Name"",
                   c.total_seats AS ""TotalSeats"",
                   COALESCE(SUM(CASE WHEN h.status = 'HELD' AND h.expires_at > clock_timestamp() THEN 1 ELSE 0 END), 0)::int AS ""Held"",
                   COALESCE(SUM(CASE WHEN h.status = 'CONFIRMED' THEN 1 ELSE 0 END), 0)::int AS ""Confirmed""
            FROM classes c
            LEFT JOIN holds h ON h.class_id = c.id
            GROUP BY c.id, c.name, c.total_seats, c.created_at
            ORDER BY c.created_at ASC, c.id ASC
        ").ToListAsync(cancellationToken);

        return results.Select(r => new ClassSummaryResponse(
            r.Id,
            r.Name,
            r.TotalSeats,
            r.Held,
            r.Confirmed,
            Math.Max(r.TotalSeats - r.Held - r.Confirmed, 0)
        )).ToList();
    }

    public async Task<ClassSummaryResponse?> GetClassByIdAsync(Guid classId, CancellationToken cancellationToken = default)
    {
        var results = await _db.Database.SqlQueryRaw<ClassSummaryQueryResult>(@"
            SELECT c.id AS ""Id"",
                   c.name AS ""Name"",
                   c.total_seats AS ""TotalSeats"",
                   COALESCE(SUM(CASE WHEN h.status = 'HELD' AND h.expires_at > clock_timestamp() THEN 1 ELSE 0 END), 0)::int AS ""Held"",
                   COALESCE(SUM(CASE WHEN h.status = 'CONFIRMED' THEN 1 ELSE 0 END), 0)::int AS ""Confirmed""
            FROM classes c
            LEFT JOIN holds h ON h.class_id = c.id
            WHERE c.id = {0}
            GROUP BY c.id, c.name, c.total_seats, c.created_at
        ", classId).ToListAsync(cancellationToken);

        var result = results.FirstOrDefault();
        if (result == null)
        {
            return null;
        }

        return new ClassSummaryResponse(
            result.Id,
            result.Name,
            result.TotalSeats,
            result.Held,
            result.Confirmed,
            Math.Max(result.TotalSeats - result.Held - result.Confirmed, 0)
        );
    }
}

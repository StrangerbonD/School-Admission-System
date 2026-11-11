using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Npgsql;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Data.Entities;
using SchoolAdmission.Api.Models;

namespace SchoolAdmission.Api.Services;

public class HoldService : IHoldService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;

    public HoldService(AppDbContext db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
    }

    public async Task<CreateHoldResponse> CreateHoldAsync(Guid classId, string studentId, CancellationToken cancellationToken = default)
    {
        var holdDurationConfig = _configuration["HOLD_DURATION_SECONDS"] ?? "600";
        if (!int.TryParse(holdDurationConfig, out var holdDurationSeconds) || holdDurationSeconds <= 0)
        {
            holdDurationSeconds = 600;
        }

        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var classRows = await _db.Classes
                .FromSqlInterpolated($"SELECT id, name, total_seats, created_at FROM classes WHERE id = {classId} FOR UPDATE")
                .ToListAsync(cancellationToken);

            var classEntity = classRows.FirstOrDefault();
            if (classEntity == null)
            {
                throw new NotFoundException($"Class with id '{classId}' not found.");
            }

            await _db.Database.ExecuteSqlInterpolatedAsync($@"
                UPDATE holds SET status = 'EXPIRED'
                WHERE class_id = {classId} AND student_id = {studentId}
                  AND status = 'HELD' AND expires_at <= clock_timestamp()", cancellationToken);

            var check = await _db.Database.SqlQueryRaw<HoldCheckResult>(@"
                SELECT 
                    (SELECT EXISTS (
                        SELECT 1 FROM holds 
                        WHERE class_id = {0} AND student_id = {1} 
                          AND status IN ('HELD', 'CONFIRMED')
                    )) AS ""AlreadyApplied"",
                    (SELECT count(*)::int FROM holds 
                     WHERE class_id = {0} 
                       AND (status = 'CONFIRMED' OR (status = 'HELD' AND expires_at > clock_timestamp()))) AS ""ActiveSeats""",
                classId, studentId)
                .SingleAsync(cancellationToken);

            if (check.AlreadyApplied)
            {
                throw new ConflictException("already_applied", "An active hold or confirmed admission already exists for this student.");
            }

            if (check.ActiveSeats >= classEntity.TotalSeats)
            {
                throw new ConflictException("no_seats", "No seats available in this class.");
            }

            var holdId = Guid.NewGuid();
            var conn = (NpgsqlConnection)_db.Database.GetDbConnection();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO holds (id, class_id, student_id, status, expires_at)
                VALUES ($1, $2, $3, 'HELD', clock_timestamp() + make_interval(secs => $4))
                RETURNING expires_at, clock_timestamp()", conn, (NpgsqlTransaction)tx.GetDbTransaction());
            cmd.Parameters.AddWithValue(holdId);
            cmd.Parameters.AddWithValue(classId);
            cmd.Parameters.AddWithValue(studentId);
            cmd.Parameters.AddWithValue((double)holdDurationSeconds);

            DateTime expiresAt;
            DateTime serverTime;
            await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
            {
                await reader.ReadAsync(cancellationToken);
                expiresAt = reader.GetDateTime(0);
                serverTime = reader.GetDateTime(1);
            }

            await tx.CommitAsync(cancellationToken);

            return new CreateHoldResponse(
                holdId,
                classId,
                studentId,
                HoldStatus.Held,
                expiresAt,
                serverTime
            );
        }
        catch (PostgresException pgEx) when (pgEx.SqlState == "23505")
        {
            try { await tx.RollbackAsync(cancellationToken); } catch { }
            throw new ConflictException("already_applied", "An active hold or confirmed admission already exists for this student.");
        }
        catch (DbUpdateException dbEx) when (FindPostgresException(dbEx)?.SqlState == "23505")
        {
            try { await tx.RollbackAsync(cancellationToken); } catch { }
            throw new ConflictException("already_applied", "An active hold or confirmed admission already exists for this student.");
        }
        catch
        {
            try { await tx.RollbackAsync(cancellationToken); } catch { }
            throw;
        }
    }

    public async Task<HoldDetailsResponse> GetHoldByIdAsync(Guid holdId, CancellationToken cancellationToken = default)
    {
        var result = await _db.Database.SqlQueryRaw<HoldDetailsQueryResult>(@"
            SELECT id AS ""HoldId"",
                   class_id AS ""ClassId"",
                   CASE WHEN status = 'HELD' AND expires_at <= clock_timestamp() THEN 'EXPIRED' ELSE status END AS ""Status"",
                   expires_at AS ""ExpiresAt"",
                   clock_timestamp() AS ""ServerTime""
            FROM holds
            WHERE id = {0}", holdId)
            .FirstOrDefaultAsync(cancellationToken);

        if (result == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' not found.");
        }

        return new HoldDetailsResponse(
            result.HoldId,
            result.ClassId,
            result.Status,
            result.ExpiresAt,
            result.ServerTime
        );
    }

    public async Task<ConfirmHoldResponse> ConfirmHoldAsync(Guid holdId, string studentId, CancellationToken cancellationToken = default)
    {
        var affectedRows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE holds SET status = 'CONFIRMED'
            WHERE id = {holdId} AND student_id = {studentId}
              AND status = 'HELD' AND expires_at > clock_timestamp()", cancellationToken);

        if (affectedRows == 1)
        {
            return new ConfirmHoldResponse(holdId, HoldStatus.Confirmed);
        }

        var diagnostic = await _db.Database.SqlQueryRaw<HoldDiagnosticResult>(@"
            SELECT id AS ""HoldId"",
                   class_id AS ""ClassId"",
                   student_id AS ""StudentId"",
                   status AS ""RawStatus"",
                   CASE WHEN status = 'HELD' AND expires_at <= clock_timestamp() THEN 'EXPIRED' ELSE status END AS ""ComputedStatus"",
                   expires_at AS ""ExpiresAt"",
                   clock_timestamp() AS ""ServerTime""
            FROM holds
            WHERE id = {0}", holdId)
            .FirstOrDefaultAsync(cancellationToken);

        if (diagnostic == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' not found.");
        }

        if (diagnostic.StudentId != studentId)
        {
            throw new ForbiddenException("This hold belongs to another student.");
        }

        if (diagnostic.RawStatus == HoldStatus.Confirmed)
        {
            return new ConfirmHoldResponse(holdId, HoldStatus.Confirmed);
        }

        if (diagnostic.RawStatus == HoldStatus.Cancelled)
        {
            throw new ConflictException("hold_cancelled", "This hold has been cancelled.");
        }

        if (diagnostic.ComputedStatus == HoldStatus.Expired)
        {
            throw new ConflictException("hold_expired", "This hold has expired.");
        }

        throw new ConflictException("hold_expired", "This hold is no longer valid for confirmation.");
    }

    public async Task CancelHoldAsync(Guid holdId, string studentId, CancellationToken cancellationToken = default)
    {
        var affectedRows = await _db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE holds SET status = 'CANCELLED'
            WHERE id = {holdId} AND student_id = {studentId}
              AND status = 'HELD' AND expires_at > clock_timestamp()", cancellationToken);

        if (affectedRows == 1)
        {
            return;
        }

        var diagnostic = await _db.Database.SqlQueryRaw<HoldDiagnosticResult>(@"
            SELECT id AS ""HoldId"",
                   class_id AS ""ClassId"",
                   student_id AS ""StudentId"",
                   status AS ""RawStatus"",
                   CASE WHEN status = 'HELD' AND expires_at <= clock_timestamp() THEN 'EXPIRED' ELSE status END AS ""ComputedStatus"",
                   expires_at AS ""ExpiresAt"",
                   clock_timestamp() AS ""ServerTime""
            FROM holds
            WHERE id = {0}", holdId)
            .FirstOrDefaultAsync(cancellationToken);

        if (diagnostic == null)
        {
            throw new NotFoundException($"Hold with id '{holdId}' not found.");
        }

        if (diagnostic.StudentId != studentId)
        {
            throw new ForbiddenException("This hold belongs to another student.");
        }

        if (diagnostic.RawStatus == HoldStatus.Cancelled)
        {
            return;
        }

        if (diagnostic.RawStatus == HoldStatus.Confirmed)
        {
            throw new ConflictException("already_confirmed", "Confirmed admission cannot be cancelled.");
        }

        if (diagnostic.ComputedStatus == HoldStatus.Expired)
        {
            throw new ConflictException("hold_expired", "Expired hold cannot be cancelled.");
        }

        throw new ConflictException("hold_expired", "Expired hold cannot be cancelled.");
    }

    public async Task<IReadOnlyList<StudentHoldSummaryResponse>> GetMyHoldsAsync(string studentId, CancellationToken cancellationToken = default)
    {
        var items = await _db.Database.SqlQueryRaw<StudentHoldQueryResult>(@"
            SELECT h.id AS ""HoldId"",
                   h.class_id AS ""ClassId"",
                   c.name AS ""ClassName"",
                   CASE WHEN h.status = 'HELD' AND h.expires_at <= clock_timestamp() THEN 'EXPIRED' ELSE h.status END AS ""Status"",
                   h.expires_at AS ""ExpiresAt"",
                   clock_timestamp() AS ""ServerTime""
            FROM holds h
            JOIN classes c ON c.id = h.class_id
            WHERE h.student_id = {0}
            ORDER BY h.created_at DESC", studentId)
            .ToListAsync(cancellationToken);

        return items.Select(x => new StudentHoldSummaryResponse(
            x.HoldId,
            x.ClassId,
            x.ClassName,
            x.Status,
            x.ExpiresAt,
            x.ServerTime
        )).ToList();
    }

    private static PostgresException? FindPostgresException(Exception? ex)
    {
        while (ex != null)
        {
            if (ex is PostgresException pgEx) return pgEx;
            ex = ex.InnerException;
        }
        return null;
    }
}

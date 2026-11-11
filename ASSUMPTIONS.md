# Assumptions and Design Decisions

This document lists the assumptions made where the specification left room for interpretation, along with how multi-process concurrency is handled.

## 1. Student ID Validation (`X-Student-Id`)
- The `X-Student-Id` header is trimmed of leading/trailing whitespace and must match `^[0-9]{17}$` (exactly 17 ASCII digits).
- Missing, empty, duplicate, or invalid header values return `400 Bad Request` (`invalid_student_id`). Since the system does not use login credentials or tokens, invalid student identification is treated as a request validation error (`400`) rather than `401 Unauthorized`.

## 2. Class Validation and Per-Class Application Scope
- `POST /classes` requires a non-empty `name` (trimmed, up to 200 characters) and `totalSeats` as an integer between `1` and `100,000`.
- Because each shift is modeled as a separate class, the one-application-per-student restriction is scoped per class (`class_id, student_id`). A student cannot have more than one active (`HELD` or `CONFIRMED`) record in the same class at the same time, but may apply to other classes/shifts independently.

## 3. Re-Applying After Expiry or Cancellation
- A student whose previous hold in a class has become `EXPIRED` or `CANCELLED` is allowed to apply again for that same class if seats are available.
- At the database level, this is enforced with a partial unique index (`uq_active_hold`) on `(class_id, student_id) WHERE status IN ('HELD', 'CONFIRMED')`. During `POST /classes/{classId}/holds`, any past-due `HELD` record for that student in that class is first updated to `EXPIRED` inside the transaction before checking eligibility and inserting a new hold.

## 4. Hold Confirmation and Cancellation Rules
- **404 vs. 403:** When accessing, confirming, or cancelling a hold, if the `holdId` does not exist, the API returns `404 Not Found`. If the hold exists but belongs to a different `studentId`, the API returns `403 Forbidden`.
- **Idempotent Confirmation (`POST /holds/{holdId}/confirm`):** Calling confirm on an active `HELD` record transitions it to `CONFIRMED` (`200 OK`). Calling confirm again on an already `CONFIRMED` hold by the same student returns `200 OK` with `{ "holdId": "...", "status": "CONFIRMED" }` without incrementing `confirmedSeats` a second time. Attempting to confirm an `EXPIRED` or `CANCELLED` hold returns `409 Conflict`.
- **Cancellation (`DELETE /holds/{holdId}`):** Only an unexpired `HELD` record can be cancelled (`204 No Content`). Calling `DELETE` again on an already `CANCELLED` hold by the same student is idempotent and returns `204 No Content`. Attempting to cancel an already `CONFIRMED` admission or an `EXPIRED` hold returns `409 Conflict`.

## 5. Time Authority, Dynamic Expiry, and `serverTime`
- All hold expiration calculations and comparisons use PostgreSQL's `clock_timestamp()` instead of application server time or transaction-start `now()`. This avoids clock drift between containers and ensures transactions waiting on a lock always evaluate the actual current time after acquiring the lock.
- Read endpoints (`GET /classes`, `GET /classes/{classId}`, `GET /holds/{holdId}`, `GET /me/holds`) evaluate `status = 'HELD' AND expires_at <= clock_timestamp()` on the fly in SQL. As soon as `expires_at` passes, the seat is immediately reflected as available and the hold status is returned as `EXPIRED`, even before the background cleanup service (`ExpiredHoldCleaner`, which runs every 5 seconds) updates the stored status column.
- Hold responses include an extra `serverTime` field alongside `expiresAt`. The frontend uses `serverTime` to calculate the offset between the server clock and the browser clock so the countdown timer remains accurate even if the user's device clock is out of sync.

## 6. Multi-Process Concurrency Guarantee (Bonus 1.6)
Running two or more backend processes on different ports against the same PostgreSQL database will never allow `heldSeats + confirmedSeats` to exceed `totalSeats`:
- **No In-Memory State:** The backend stores no seat counters, caches, or locks in application memory.
- **Per-Class Row Locking:** In `HoldService.CreateHoldAsync`, every seat reservation runs inside an explicit `Read Committed` database transaction that first locks the target class row via `SELECT id, name, total_seats, created_at FROM classes WHERE id = @classId FOR UPDATE`.
- **Serialized Check-and-Insert:** Any concurrent requests for the same class—whether from threads in the same process or from separate backend processes—wait on PostgreSQL's row lock. Once a transaction acquires the lock, under `Read Committed` isolation its subsequent queries see all holds committed by prior transactions, expire any stale holds for the student, count active seats (`status = 'CONFIRMED'` or `status = 'HELD' AND expires_at > clock_timestamp()`), and insert the new hold within the same transaction.
- **Database Constraints and Migration Locking:** The partial unique index `uq_active_hold` prevents duplicate active holds per student per class across processes, and startup EF Core migrations use a PostgreSQL session advisory lock (`pg_advisory_lock`) so multiple backend instances starting simultaneously do not race during schema migration.

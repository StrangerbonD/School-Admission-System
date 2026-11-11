# School Admission System

First-come, first-served online school admission system with temporary seat holds, countdown expiration, and fee confirmation.

## Tech Stack

- **Backend:** C# (.NET 10 Minimal API), Entity Framework Core, Npgsql
- **Database:** PostgreSQL 16
- **Frontend:** React 18, JavaScript (Vite), Nginx
- **Containerization:** Docker & Docker Compose

## Running the Application

Ensure Docker Desktop (or Docker Engine with Docker Compose) is running on your machine. From the repository root, run:

```bash
docker compose up --build
```

Once the containers are up:

- **Frontend UI:** http://localhost:3000
- **Backend API:** http://localhost:8080

To stop the containers while keeping database data intact:

```bash
docker compose down
```

To stop the containers and reset the database volume:

```bash
docker compose down -v
```

## Environment Variables

All settings have defaults in `docker-compose.yml` and can be overridden when starting the containers:

- `HOLD_DURATION_SECONDS`: Seat hold duration in seconds (default: `600`).
- `DATABASE_URL`: PostgreSQL connection string for the backend.
- `API_URL`: Backend base URL used by the frontend (default: `http://localhost:8080`).

Example with a 10-second hold duration:

**Linux / macOS (Bash):**
```bash
HOLD_DURATION_SECONDS=10 docker compose up
```

**Windows (PowerShell):**
```powershell
$env:HOLD_DURATION_SECONDS=10; docker compose up
```

## Running Automated Tests

The backend test suite (unit, integration, and concurrency tests) runs inside Docker against an isolated test database (`school_admission_test`), so it does not affect data in the main application database.

```bash
docker compose --profile test build tests
docker compose --profile test run --rm tests
```

If you have the .NET 10 SDK installed locally and PostgreSQL running on `localhost:5432`, you can also run:

```bash
cd backend
dotnet test
```

## API Endpoints

All endpoints use `http://localhost:8080` as the base URL. Student-specific endpoints require the `X-Student-Id` header containing a 17-digit birth registration number.

| Method | Path | Header | Description |
| :--- | :--- | :--- | :--- |
| `POST` | `/classes` | - | Create a new class (`name`, `totalSeats`) |
| `GET` | `/classes` | - | List all classes with live seat counts |
| `GET` | `/classes/{classId}` | - | Get seat summary for a specific class |
| `POST` | `/classes/{classId}/holds` | `X-Student-Id` | Hold a seat in a class |
| `GET` | `/holds/{holdId}` | - | Get current status of a hold |
| `POST` | `/holds/{holdId}/confirm` | `X-Student-Id` | Confirm admission for an active hold |
| `DELETE` | `/holds/{holdId}` | `X-Student-Id` | Cancel an active hold |
| `GET` | `/me/holds` | `X-Student-Id` | List all holds for the student |

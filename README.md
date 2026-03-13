# Overview
This project is a .NET console application that ingests a mocked snapshot of retail transactions from the last 24 hours and reconciles them into a SQLite database using Entity Framework Core.

The ingestion process detects new transactions, applies updates when fields change, records audit history, and manages transaction lifecycle states such as revocation and finalization.

---

# Features
- Inserts new transactions by `TransactionId`
- Detects updates to tracked fields
- Records audit history for inserts, updates, revocations, and finalization
- Marks missing in-window transactions as revoked
- Finalizes transactions older than 24 hours
- Uses a single database transaction per run for consistency
- Reads configuration from `appsettings.json`
- Supports automated tests validating ingestion logic

---

## Tech Stack
- .NET 10 Console Application
- Entity Framework Core
- SQLite
- xUnit (for automated testing)

---

## Design Approach
The ingestion pipeline was implemented using a service-oriented structure to keep the application modular and maintainable.

The core logic resides in `TransactionIngestionService`, which reconciles snapshot data against the existing database records.

Key design considerations include:

- **Separation of concerns**  
  Snapshot retrieval, card masking, and ingestion logic are implemented in separate services.

- **Idempotent processing**  
  Incoming transaction fields are compared with stored values before updates are applied. This ensures that repeated ingestion runs with the same data do not generate duplicate updates or audit records.

- **Audit tracking**  
  Every meaningful change to a transaction is recorded in the `TransactionAudit` table to preserve historical traceability.

- **Transactional safety**  
  Each ingestion run executes inside a single database transaction to ensure that partial updates do not occur if an error happens during processing.

---

## Database Schema

## Transactions
Stores the latest state of each transaction.

Fields include:

- `Id` – Internal database primary key
- `TransactionId` – Unique identifier from the external snapshot
- `CardLast4` – Last four digits of the card number
- `LocationCode` – Store location code
- `ProductName` – Name of the purchased product
- `Amount` – Transaction amount
- `TransactionTimeUtc` – Timestamp of the transaction
- `Status` – Transaction lifecycle state (`Active`, `Revoked`, `Finalized`)
- `CreatedAtUtc` – Record creation timestamp
- `UpdatedAtUtc` – Last modification timestamp

## TransactionAudits
Stores historical changes made to transactions.

Fields include:

- `Id` – Audit record identifier
- `TransactionRecordId` – Foreign key to the transaction record
- `TransactionId` – External transaction identifier
- `ChangeType` – Type of change (`Insert`, `Update`, `Revoke`, `Finalize`)
- `FieldName` – Field that changed
- `OldValue` – Previous value
- `NewValue` – Updated value
- `ChangedAtUtc` – Timestamp of the change


---

## Project Structure
- **Models/** – Database entity models  
- **Data/** – Entity Framework Core DbContext  
- **Services/** – Snapshot loading and ingestion logic  
- **DTOs/** – Data transfer objects for snapshot input  
- **MockData/** – Local JSON snapshot used for testing  
- **TransactionIngest.Tests/** – Automated test project  
- **Program.cs** – Application entry point  
- **appsettings.json** – Configuration for database and snapshot path  
- **TransactionIngest.csproj** – Project configuration  
- **README.md** – Project documentation

---

## Assumptions
- `TransactionId` is treated as an integer based on the structure of the provided dataset.
- Only the last four digits of the card number are stored for privacy.
- Transaction status values used are:
  - `Active`
  - `Revoked`
  - `Finalized`
- The external transactions API described in the prompt is simulated using a local JSON snapshot.

---

## How to Run

Restore dependencies: dotnet restore

Run the application: dotnet run

The application will:
1. Load transactions from `MockData/transactions.json`
2. Process ingestion logic
3. Persist results to `transactions.db`

---

## How to Test
Automated tests validate ingestion logic.

Run tests using: dotnet test

Tests currently verify:
- transaction insertion
- update detection
- revocation behavior
- idempotent snapshot processing

---

## Included Deliverables
This repository includes:
- .NET console application source code
- EF Core code-first models and DbContext
- SQLite-backed local database configuration
- `appsettings.json` for configurable paths and connection string
- Mock JSON snapshot for local testing
- Automated tests for insert, update, revocation, and idempotency scenarios
- README with setup, run instructions, design approach, and assumptions

using Microsoft.EntityFrameworkCore;
using TransactionIngest.Data;
using TransactionIngest.DTOs;
using TransactionIngest.Models;

namespace TransactionIngest.Services;

public class TransactionIngestionService
{
    private readonly AppDbContext _dbContext;
    private readonly SnapshotService _snapshotService;
    private readonly CardMaskService _cardMaskService;

    public TransactionIngestionService(
        AppDbContext dbContext,
        SnapshotService snapshotService,
        CardMaskService cardMaskService)
    {
        _dbContext = dbContext;
        _snapshotService = snapshotService;
        _cardMaskService = cardMaskService;
    }

    public async Task RunAsync()
    {
        var snapshot = await _snapshotService.GetSnapshotAsync();
        await RunAsync(snapshot);

        Console.WriteLine("Ingestion complete.");
    }

    public async Task RunAsync(List<TransactionSnapshotDto> snapshot)
    {
        var utcNow = DateTime.UtcNow;
        var cutoff = utcNow.AddHours(-24);

        Console.WriteLine($"Processing {snapshot.Count} transactions...");

        await using var dbTransaction = await _dbContext.Database.BeginTransactionAsync();

        var snapshotIds = snapshot.Select(x => x.TransactionId).ToHashSet();

        foreach (var item in snapshot)
        {
            await UpsertTransactionAsync(item, utcNow);
        }

        await RevokeMissingTransactionsAsync(snapshotIds, cutoff, utcNow);
        await FinalizeOldTransactionsAsync(cutoff, utcNow);

        await _dbContext.SaveChangesAsync();
        await dbTransaction.CommitAsync();
    }

    private async Task UpsertTransactionAsync(TransactionSnapshotDto item, DateTime utcNow)
    {
        var existing = await _dbContext.Transactions
            .FirstOrDefaultAsync(x => x.TransactionId == item.TransactionId);

        var incomingTimestampUtc = item.Timestamp.ToUniversalTime();

        if (existing == null)
        {
            var newRecord = new TransactionRecord
            {
                TransactionId = item.TransactionId,
                CardLast4 = _cardMaskService.GetLast4(item.CardNumber),
                LocationCode = item.LocationCode,
                ProductName = item.ProductName,
                Amount = item.Amount,
                TransactionTimeUtc = incomingTimestampUtc,
                Status = "Active",
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow
            };

            _dbContext.Transactions.Add(newRecord);
            await _dbContext.SaveChangesAsync();

            _dbContext.TransactionAudits.Add(new TransactionAudit
            {
                TransactionRecordId = newRecord.Id,
                TransactionId = newRecord.TransactionId,
                ChangeType = "Insert",
                FieldName = "Record",
                OldValue = null,
                NewValue = "Created",
                ChangedAtUtc = utcNow
            });

            Console.WriteLine($"Inserted transaction {item.TransactionId}");
            return;
        }

        if (existing.Status == "Finalized")
        {
            Console.WriteLine($"Transaction {item.TransactionId} is finalized. Skipping.");
            return;
        }

        var changed = false;

        changed |= AddAuditIfChanged(
            existing,
            "CardLast4",
            existing.CardLast4,
            _cardMaskService.GetLast4(item.CardNumber),
            utcNow);

        changed |= AddAuditIfChanged(
            existing,
            "LocationCode",
            existing.LocationCode,
            item.LocationCode,
            utcNow);

        changed |= AddAuditIfChanged(
            existing,
            "ProductName",
            existing.ProductName,
            item.ProductName,
            utcNow);

        changed |= AddAuditIfChanged(
            existing,
            "Amount",
            existing.Amount.ToString("0.00"),
            item.Amount.ToString("0.00"),
            utcNow);

        var existingTimestampUtc = DateTime.SpecifyKind(existing.TransactionTimeUtc, DateTimeKind.Utc);

        if (existingTimestampUtc != incomingTimestampUtc)
        {
            _dbContext.TransactionAudits.Add(new TransactionAudit
            {
                TransactionRecordId = existing.Id,
                TransactionId = existing.TransactionId,
                ChangeType = "Update",
                FieldName = "TransactionTimeUtc",
                OldValue = existingTimestampUtc.ToString("O"),
                NewValue = incomingTimestampUtc.ToString("O"),
                ChangedAtUtc = utcNow
            });

            changed = true;
        }

        if (!changed)
        {
            if (existing.Status == "Revoked")
            {
                _dbContext.TransactionAudits.Add(new TransactionAudit
                {
                    TransactionRecordId = existing.Id,
                    TransactionId = existing.TransactionId,
                    ChangeType = "Update",
                    FieldName = "Status",
                    OldValue = "Revoked",
                    NewValue = "Active",
                    ChangedAtUtc = utcNow
                });

                existing.Status = "Active";
                existing.UpdatedAtUtc = utcNow;

                Console.WriteLine($"Reactivated transaction {item.TransactionId}");
                return;
            }

            Console.WriteLine($"Transaction {item.TransactionId} unchanged. Skipping.");
            return;
        }

        existing.CardLast4 = _cardMaskService.GetLast4(item.CardNumber);
        existing.LocationCode = item.LocationCode;
        existing.ProductName = item.ProductName;
        existing.Amount = item.Amount;
        existing.TransactionTimeUtc = incomingTimestampUtc;
        existing.Status = "Active";
        existing.UpdatedAtUtc = utcNow;

        Console.WriteLine($"Updated transaction {item.TransactionId}");
    }

    private async Task RevokeMissingTransactionsAsync(HashSet<int> snapshotIds, DateTime cutoff, DateTime utcNow)
    {
        var candidates = await _dbContext.Transactions
            .Where(x => x.TransactionTimeUtc >= cutoff && x.Status != "Finalized")
            .ToListAsync();

        foreach (var record in candidates)
        {
            if (!snapshotIds.Contains(record.TransactionId) && record.Status != "Revoked")
            {
                var oldStatus = record.Status;

                record.Status = "Revoked";
                record.UpdatedAtUtc = utcNow;

                _dbContext.TransactionAudits.Add(new TransactionAudit
                {
                    TransactionRecordId = record.Id,
                    TransactionId = record.TransactionId,
                    ChangeType = "Revoke",
                    FieldName = "Status",
                    OldValue = oldStatus,
                    NewValue = "Revoked",
                    ChangedAtUtc = utcNow
                });

                Console.WriteLine($"Revoked transaction {record.TransactionId}");
            }
        }
    }

    private async Task FinalizeOldTransactionsAsync(DateTime cutoff, DateTime utcNow)
    {
        var oldRecords = await _dbContext.Transactions
            .Where(x => x.TransactionTimeUtc < cutoff && x.Status != "Finalized")
            .ToListAsync();

        foreach (var record in oldRecords)
        {
            var oldStatus = record.Status;

            record.Status = "Finalized";
            record.UpdatedAtUtc = utcNow;

            _dbContext.TransactionAudits.Add(new TransactionAudit
            {
                TransactionRecordId = record.Id,
                TransactionId = record.TransactionId,
                ChangeType = "Finalize",
                FieldName = "Status",
                OldValue = oldStatus,
                NewValue = "Finalized",
                ChangedAtUtc = utcNow
            });

            Console.WriteLine($"Finalized transaction {record.TransactionId}");
        }
    }

    private bool AddAuditIfChanged(
        TransactionRecord existing,
        string fieldName,
        string oldValue,
        string newValue,
        DateTime utcNow)
    {
        if (oldValue == newValue)
        {
            return false;
        }

        _dbContext.TransactionAudits.Add(new TransactionAudit
        {
            TransactionRecordId = existing.Id,
            TransactionId = existing.TransactionId,
            ChangeType = "Update",
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedAtUtc = utcNow
        });

        return true;
    }
}
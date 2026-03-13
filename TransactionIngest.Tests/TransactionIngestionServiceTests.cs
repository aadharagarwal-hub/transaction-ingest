using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TransactionIngest.Data;
using TransactionIngest.DTOs;
using TransactionIngest.Services;
using Xunit;

namespace TransactionIngest.Tests;

public class TransactionIngestionServiceTests
{
    private static AppDbContext CreateDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static TransactionIngestionService CreateService(AppDbContext context)
    {
        var snapshotService = new SnapshotServiceStub();
        var cardMaskService = new CardMaskService();

        return new TransactionIngestionService(context, snapshotService, cardMaskService);
    }

    [Fact]
    public async Task RunAsync_Inserts_New_Transactions()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateDbContext(connection);
        var service = CreateService(context);

        var snapshot = new List<TransactionSnapshotDto>
        {
            new()
            {
                TransactionId = 1001,
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Mouse",
                Amount = 19.99m,
                Timestamp = DateTime.UtcNow
            }
        };

        await service.RunAsync(snapshot);

        Assert.Single(context.Transactions);
        Assert.Single(context.TransactionAudits);
    }

    [Fact]
    public async Task RunAsync_Updates_Changed_Transaction_And_Creates_Audit()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateDbContext(connection);
        var service = CreateService(context);

        var time = DateTime.UtcNow;

        await service.RunAsync(new List<TransactionSnapshotDto>
        {
            new()
            {
                TransactionId = 1001,
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Mouse",
                Amount = 19.99m,
                Timestamp = time
            }
        });

        await service.RunAsync(new List<TransactionSnapshotDto>
        {
            new()
            {
                TransactionId = 1001,
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Keyboard",
                Amount = 29.99m,
                Timestamp = time
            }
        });

        var transaction = await context.Transactions.FirstAsync();
        Assert.Equal("Keyboard", transaction.ProductName);
        Assert.Equal(29.99m, transaction.Amount);

        var audits = context.TransactionAudits.Where(x => x.ChangeType == "Update").ToList();
        Assert.True(audits.Count >= 2);
    }

    [Fact]
    public async Task RunAsync_Revokes_Missing_Transaction_Within_24_Hours()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateDbContext(connection);
        var service = CreateService(context);

        var time = DateTime.UtcNow;

        await service.RunAsync(new List<TransactionSnapshotDto>
        {
            new()
            {
                TransactionId = 1001,
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Mouse",
                Amount = 19.99m,
                Timestamp = time
            },
            new()
            {
                TransactionId = 1002,
                CardNumber = "4000000000000002",
                LocationCode = "STO-02",
                ProductName = "Cable",
                Amount = 25.00m,
                Timestamp = time
            }
        });

        await service.RunAsync(new List<TransactionSnapshotDto>
        {
            new()
            {
                TransactionId = 1001,
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Mouse",
                Amount = 19.99m,
                Timestamp = time
            }
        });

        var revoked = await context.Transactions.FirstAsync(x => x.TransactionId == 1002);
        Assert.Equal("Revoked", revoked.Status);
    }

    [Fact]
    public async Task RunAsync_Is_Idempotent_For_Unchanged_Snapshot()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var context = CreateDbContext(connection);
        var service = CreateService(context);

        var time = DateTime.UtcNow;

        var snapshot = new List<TransactionSnapshotDto>
        {
            new()
            {
                TransactionId = 1001,
                CardNumber = "4111111111111111",
                LocationCode = "STO-01",
                ProductName = "Mouse",
                Amount = 19.99m,
                Timestamp = time
            }
        };

        await service.RunAsync(snapshot);
        var transactionCountAfterFirstRun = context.Transactions.Count();
        var auditCountAfterFirstRun = context.TransactionAudits.Count();

        await service.RunAsync(snapshot);

        Assert.Equal(transactionCountAfterFirstRun, context.Transactions.Count());
        Assert.Equal(auditCountAfterFirstRun, context.TransactionAudits.Count());
    }
}

public class SnapshotServiceStub : SnapshotService
{
    public SnapshotServiceStub() : base(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build())
    {
    }
}
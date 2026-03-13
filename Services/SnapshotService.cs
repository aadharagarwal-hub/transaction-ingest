using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TransactionIngest.DTOs;

namespace TransactionIngest.Services;

public class SnapshotService
{
    private readonly IConfiguration _configuration;

    public SnapshotService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public virtual async Task<List<TransactionSnapshotDto>> GetSnapshotAsync()
    {
        var path = _configuration["MockApi:SnapshotPath"];

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("MockApi:SnapshotPath is missing.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Snapshot file not found: {path}");
        }

        var json = await File.ReadAllTextAsync(path);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var transactions = JsonSerializer.Deserialize<List<TransactionSnapshotDto>>(json, options);

        return transactions ?? new List<TransactionSnapshotDto>();
    }
}
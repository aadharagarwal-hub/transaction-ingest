namespace TransactionIngest.Models;

public class TransactionAudit
{
    public int Id { get; set; }
    public int TransactionRecordId { get; set; }
    public TransactionRecord? TransactionRecord { get; set; }

    public int TransactionId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime ChangedAtUtc { get; set; }
}
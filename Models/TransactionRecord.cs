namespace TransactionIngest.Models;

public class TransactionRecord
{
    public int Id { get; set; }
    public int TransactionId { get; set; }
    public string CardLast4 { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime TransactionTimeUtc { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<TransactionAudit> Audits { get; set; } = new();
}
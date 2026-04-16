using InstallmentSystem.Models.Enums;

namespace InstallmentSystem.Models;

public class JournalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ReceiptId { get; set; }               // ربط بسند القبض
    public string EntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public JournalEntryType Type { get; set; } = JournalEntryType.Manual;       // Manual, Payment, Cancel, ContractIssue
    public Guid? CurrencyId { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public bool IsReversed { get; set; } = false;

    public Currency? Currency { get; set; }

    public Receipt? Receipt { get; set; }
    public ICollection<JournalEntryDetail> Details { get; set; } = new List<JournalEntryDetail>();
}

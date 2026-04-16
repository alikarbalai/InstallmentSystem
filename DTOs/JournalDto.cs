using InstallmentSystem.Models.Enums;

namespace InstallmentSystem.DTOs;

public class CreateJournalEntryDto
{
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
    public string Description { get; set; } = string.Empty;
    public JournalEntryType Type { get; set; } = JournalEntryType.Manual;
    public Guid? CurrencyId { get; set; }
    public decimal? ExchangeRate { get; set; }
    public List<JournalDetailDto> Details { get; set; } = new();
}

public class JournalDetailDto
{
    public Guid AccountId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

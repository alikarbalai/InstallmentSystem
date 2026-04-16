using InstallmentSystem.Models.Enums;

namespace InstallmentSystem.Models;

public class Receipt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ReceiptNumber { get; set; } = string.Empty;  // رقم سند القبض
    public Guid PaymentId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid CurrencyId { get; set; }
    public decimal Amount { get; set; }
    public decimal AmountInBase { get; set; }          // المبلغ بالدينار العراقي
    public decimal ExchangeRate { get; set; } = 1;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public bool IsCancelled { get; set; } = false;
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public Payment Payment { get; set; } = null!;
    public Customer Customer { get; set; } = null!;
    public Currency Currency { get; set; } = null!;
    public JournalEntry? JournalEntry { get; set; }
}

using InstallmentSystem.Models.Enums;

namespace InstallmentSystem.Models;

public class Payment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid ContractId { get; set; }
    public Guid InstallmentId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public string? Notes { get; set; }
    public bool IsCancelled { get; set; } = false;
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public Customer Customer { get; set; } = null!;
    public InstallmentContract Contract { get; set; } = null!;
    public Installment Installment { get; set; } = null!;
    public Receipt? Receipt { get; set; }
}

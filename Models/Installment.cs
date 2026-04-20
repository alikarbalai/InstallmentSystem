using System.ComponentModel.DataAnnotations;
using InstallmentSystem.Models.Enums;

namespace InstallmentSystem.Models;

public class Installment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillId { get; set; }
    public int InstallmentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal Amount { get; set; }
    public decimal PaidAmount { get; set; } = 0;
    public decimal RemainingAmount { get; set; }
    public InstallmentStatus Status { get; set; } = InstallmentStatus.Pending; // Pending, Paid, Overdue, PartiallyPaid
    public DateTime? PaymentDate { get; set; }

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public InstallmentBill Bill { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

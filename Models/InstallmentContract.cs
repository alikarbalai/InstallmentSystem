using System.ComponentModel.DataAnnotations;
using InstallmentSystem.Models.Enums;

namespace InstallmentSystem.Models;

public class InstallmentContract
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CustomerId { get; set; }
    public Guid CurrencyId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public DateTime ContractDate { get; set; }
    public decimal TotalAmount { get; set; }           // بعملة العقد
    public decimal TotalAmountInBase { get; set; }     // بالدينار العراقي
    public decimal DownPayment { get; set; }
    public decimal RemainingAmount { get; set; }
    public int InstallmentCount { get; set; }
    public decimal InstallmentValue { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public ContractStatus Status { get; set; } = ContractStatus.Active;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Timestamp]
    public byte[]? RowVersion { get; set; }

    public Customer Customer { get; set; } = null!;
    public Currency Currency { get; set; } = null!;
    public ICollection<Installment> Installments { get; set; } = new List<Installment>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<ContractItem> ContractItems { get; set; } = new List<ContractItem>();
}

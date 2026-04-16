namespace InstallmentSystem.DTOs;

public class CreatePaymentDto
{
    public Guid CustomerId { get; set; }
    public Guid InstallmentId { get; set; }
    public Guid CurrencyId { get; set; }
    public decimal Amount { get; set; }
    public InstallmentSystem.Models.Enums.PaymentMethod PaymentMethod { get; set; } = InstallmentSystem.Models.Enums.PaymentMethod.Cash;
    public string? Notes { get; set; }
}

public class CancelPaymentDto
{
    public string? CancelReason { get; set; }
}

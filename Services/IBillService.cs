using InstallmentSystem.DTOs;
using InstallmentSystem.Models;

namespace InstallmentSystem.Services;

public interface IBillService
{
    Task<InstallmentBill> CreateBillAsync(CreateBillDto dto);
    Task<Payment> ProcessPaymentAsync(CreatePaymentDto dto);
    Task CancelPaymentAsync(Guid paymentId, string? reason);
    Task UpdateOverdueInstallmentsAsync();
    Task DeleteBillAsync(Guid id);
}

using InstallmentSystem.DTOs;
using InstallmentSystem.Models;

namespace InstallmentSystem.Services;

public interface IContractService
{
    Task<InstallmentContract> CreateContractAsync(CreateContractDto dto);
    Task<Payment> ProcessPaymentAsync(CreatePaymentDto dto);
    Task CancelPaymentAsync(Guid paymentId, string? reason);
    Task UpdateOverdueInstallmentsAsync();
    Task DeleteContractAsync(Guid id);
}

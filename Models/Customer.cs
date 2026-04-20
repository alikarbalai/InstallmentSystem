namespace InstallmentSystem.Models;

public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<InstallmentContract> Contracts { get; set; } = new List<InstallmentContract>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

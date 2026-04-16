namespace InstallmentSystem.Models;

public class ContractItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ContractId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }

    public InstallmentContract Contract { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

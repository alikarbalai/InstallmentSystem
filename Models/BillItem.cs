namespace InstallmentSystem.Models;

public class BillItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid BillId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }

    public InstallmentBill Bill { get; set; } = null!;
    public Product Product { get; set; } = null!;
}

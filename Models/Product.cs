namespace InstallmentSystem.Models;

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }

    public ICollection<BillItem> BillItems { get; set; } = new List<BillItem>();
}

namespace InstallmentSystem.Models;

public class Currency
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;       // دينار عراقي
    public string Code { get; set; } = string.Empty;       // IQD
    public string Symbol { get; set; } = string.Empty;     // د.ع
    public decimal ExchangeRate { get; set; } = 1;         // معدل التحويل مقابل الدينار
    public bool IsBase { get; set; } = false;              // العملة الأساسية
    public bool IsActive { get; set; } = true;

    public ICollection<InstallmentContract> Contracts { get; set; } = new List<InstallmentContract>();
    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}

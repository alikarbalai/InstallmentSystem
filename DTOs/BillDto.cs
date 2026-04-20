namespace InstallmentSystem.DTOs;

public class CreateBillDto
{
    public Guid CustomerId { get; set; }
    public Guid CurrencyId { get; set; }
    public DateTime BillDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; }
    public int InstallmentCount { get; set; }
    public List<BillItemDto> Items { get; set; } = new();
}

public class BillItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class UpdateBillDto
{
    public Guid CustomerId { get; set; }
    public Guid CurrencyId { get; set; }
    public DateTime BillDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal RemainingAmount { get; set; }
    public int InstallmentCount { get; set; }
    public decimal InstallmentValue { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public List<BillItemDto> Items { get; set; } = new();
}

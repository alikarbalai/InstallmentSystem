namespace InstallmentSystem.DTOs;

public class CreateContractDto
{
    public Guid CustomerId { get; set; }
    public Guid CurrencyId { get; set; }
    public DateTime ContractDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; }
    public int InstallmentCount { get; set; }
    public List<ContractItemDto> Items { get; set; } = new();
}

public class ContractItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
}

public class UpdateContractDto
{
    public Guid CustomerId { get; set; }
    public Guid CurrencyId { get; set; }
    public DateTime ContractDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal DownPayment { get; set; }
    public decimal RemainingAmount { get; set; }
    public int InstallmentCount { get; set; }
    public decimal InstallmentValue { get; set; }
    public decimal ExchangeRate { get; set; } = 1;
    public List<ContractItemDto> Items { get; set; } = new();
}

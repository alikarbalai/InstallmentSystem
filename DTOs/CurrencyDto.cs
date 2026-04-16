namespace InstallmentSystem.DTOs;

public class CreateCurrencyDto
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1;
    public bool IsBase { get; set; } = false;
}

public class UpdateExchangeRateDto
{
    public decimal ExchangeRate { get; set; }
}

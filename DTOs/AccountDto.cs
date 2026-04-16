using System.ComponentModel.DataAnnotations;

namespace InstallmentSystem.DTOs;

public class AccountDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid? ParentId { get; set; }
    public decimal Balance { get; set; }
}

public class AccountTreeDto : AccountDto
{
    public List<AccountTreeDto> Children { get; set; } = new();
}

public class CreateAccountDto
{
    [Required(ErrorMessage = "رمز الحساب مطلوب")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم الحساب مطلوب")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع الحساب مطلوب")]
    public string Type { get; set; } = string.Empty;
    
    public Guid? ParentId { get; set; }
}

public class UpdateAccountDto
{
    [Required(ErrorMessage = "رمز الحساب مطلوب")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم الحساب مطلوب")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع الحساب مطلوب")]
    public string Type { get; set; } = string.Empty;
    
    public Guid? ParentId { get; set; }
}

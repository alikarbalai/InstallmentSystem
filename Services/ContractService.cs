using InstallmentSystem.Data;
using InstallmentSystem.DTOs;
using InstallmentSystem.Models;
using InstallmentSystem.Models.Enums;
using InstallmentSystem.Exceptions;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace InstallmentSystem.Services;

public class ContractService : IContractService
{
    private readonly AppDbContext _db;

    public ContractService(AppDbContext db) => _db = db;

    // ─── Create Contract ──────────────────────────────────────────────────────
    public async Task<InstallmentContract> CreateContractAsync(CreateContractDto dto)
    {
        var currency = await _db.Currencies.FindAsync(dto.CurrencyId)
            ?? throw new NotFoundException("العملة غير موجودة");

        var customer = await _db.Customers.FindAsync(dto.CustomerId)
            ?? throw new NotFoundException("العميل غير موجود");

        var totalFromItems = dto.Items.Sum(i => i.Quantity * i.Price);
        var totalAmount    = totalFromItems > 0 ? totalFromItems : dto.TotalAmount;
        var remaining      = totalAmount - dto.DownPayment;
        var installmentVal = dto.InstallmentCount > 0 ? remaining / dto.InstallmentCount : 0;
        var amountInBase   = totalAmount * currency.ExchangeRate;

        var contract = new InstallmentContract
        {
            CustomerId       = dto.CustomerId,
            CurrencyId       = dto.CurrencyId,
            ExchangeRate     = currency.ExchangeRate,
            ContractNumber   = $"CNT-{DateTime.UtcNow:yyyyMMddHHmmss}",
            ContractDate     = dto.ContractDate,
            TotalAmount      = totalAmount,
            TotalAmountInBase= amountInBase,
            DownPayment      = dto.DownPayment,
            RemainingAmount  = remaining,
            InstallmentCount = dto.InstallmentCount,
            InstallmentValue = installmentVal,
            Status           = ContractStatus.Active,
            CreatedAt        = DateTime.UtcNow
        };

        for (int i = 1; i <= dto.InstallmentCount; i++)
        {
            contract.Installments.Add(new Installment
            {
                InstallmentNumber = i,
                DueDate           = dto.ContractDate.AddMonths(i),
                Amount            = installmentVal,
                RemainingAmount   = installmentVal,
                Status            = InstallmentStatus.Pending
            });
        }

        foreach (var item in dto.Items)
        {
            contract.ContractItems.Add(new ContractItem
            {
                ProductId = item.ProductId,
                Quantity  = item.Quantity,
                Price     = item.Price
            });
        }

        _db.InstallmentContracts.Add(contract);

        // ── Revenue Recognition (استحقاق العقد) ──
        // من حساب ذمم العملاء (102) إلى المبيعات / الإيرادات (401)
        var clientAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "102") 
            ?? throw new NotFoundException("حساب ذمم العملاء غير موجود بالنظام (102)");
            
        var revenueAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "401") 
            ?? throw new NotFoundException("حساب الإيرادات غير موجود بالنظام (401)");

        var contractJournal = new JournalEntry
        {
            EntryNumber = $"JNL-{DateTime.UtcNow:yyyyMMddHHmmss}",
            EntryDate   = DateTime.UtcNow,
            Description = $"اثبات ذمة مالية للعقد رقم {contract.ContractNumber} للعميل {customer.FullName}",
            Type        = JournalEntryType.ContractIssue,
            CurrencyId  = contract.CurrencyId,
            ExchangeRate= contract.ExchangeRate,
            Details     = new List<JournalEntryDetail>
            {
                new() { AccountId = clientAccount.Id, Debit = amountInBase, Credit = 0 },
                new() { AccountId = revenueAccount.Id, Debit = 0, Credit = amountInBase }
            }
        };
        _db.JournalEntries.Add(contractJournal);

        // حفظ واحد شامل للجميع كمعاملة واحدة
        await _db.SaveChangesAsync();
        return contract;
    }

    // ─── Process Payment + Create Receipt + Journal Entry ─────────────────────
    public async Task<Payment> ProcessPaymentAsync(CreatePaymentDto dto)
    {
        var installment = await _db.Installments
            .Include(i => i.Contract)
            .FirstOrDefaultAsync(i => i.Id == dto.InstallmentId)
            ?? throw new NotFoundException("القسط غير موجود");

        if (dto.Amount <= 0)
            throw new ValidationException("المبلغ يجب أن يكون أكبر من صفر");

        if (dto.Amount > installment.RemainingAmount)
            throw new ValidationException("المبلغ أكبر من المتبقي على هذا القسط");

        // Use Contract's Currency instead of dto.CurrencyId
        var contractCurrency = await _db.Currencies.FindAsync(installment.Contract.CurrencyId)
            ?? throw new NotFoundException("عملة العقد غير موجودة");

        var amountInBase = dto.Amount * contractCurrency.ExchangeRate;
        
        // ── Payment ──
        var payment = new Payment
        {
            CustomerId    = dto.CustomerId,
            ContractId    = installment.ContractId,
            InstallmentId = dto.InstallmentId,
            Amount        = dto.Amount,
            PaymentDate   = DateTime.UtcNow,
            PaymentMethod = dto.PaymentMethod,
            Notes         = dto.Notes
        };

        installment.PaidAmount      += dto.Amount;
        installment.RemainingAmount -= dto.Amount;
        installment.Status           = installment.RemainingAmount <= 0 ? InstallmentStatus.Paid : InstallmentStatus.PartiallyPaid;
        
        if (installment.Status == InstallmentStatus.Paid)
            installment.PaymentDate = DateTime.UtcNow;

        installment.Contract.RemainingAmount -= dto.Amount;
        if (installment.Contract.RemainingAmount <= 0)
            installment.Contract.Status = ContractStatus.Completed;

        _db.Payments.Add(payment);

        // ── Receipt (سند قبض) ──
        var receiptNumber = $"RCP-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var receipt = new Receipt
        {
            ReceiptNumber = receiptNumber,
            PaymentId     = payment.Id, 
            CustomerId    = dto.CustomerId,
            CurrencyId    = installment.Contract.CurrencyId, // Force contract currency
            Amount        = dto.Amount,
            AmountInBase  = amountInBase,
            ExchangeRate  = contractCurrency.ExchangeRate,
            PaymentMethod = dto.PaymentMethod,
            Notes         = dto.Notes,
            ReceiptDate   = DateTime.UtcNow
        };
        
        // ربط السند بالدفعة
        receipt.Payment = payment;
        _db.Receipts.Add(receipt);

        // ── Journal Entry (قيد محاسبي) ──
        var cashAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "101") ?? throw new NotFoundException("حساب النقدية غير موجود");
        var clientAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "102") ?? throw new NotFoundException("حساب ذمم العملاء غير موجود");
        var bankAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "103") ?? throw new NotFoundException("حساب البنك غير موجود");

        var debitAccountId = dto.PaymentMethod == PaymentMethod.Transfer ? bankAccount.Id : cashAccount.Id;

        var journalEntry = new JournalEntry
        {
            ReceiptId   = receipt.Id, // سيتم الربط تلقائيا
            EntryNumber = $"JNL-{DateTime.UtcNow:yyyyMMddHHmmss}",
            EntryDate   = DateTime.UtcNow,
            Description = $"سند قبض رقم {receiptNumber} - دفعة قسط",
            Type        = JournalEntryType.ReceiptVoucher,
            CurrencyId  = receipt.CurrencyId,
            ExchangeRate= receipt.ExchangeRate,
            Details     = new List<JournalEntryDetail>
            {
                new() { AccountId = debitAccountId, Debit  = amountInBase, Credit = 0 },
                new() { AccountId = clientAccount.Id, Debit  = 0, Credit = amountInBase }
            }
        };
        
        journalEntry.Receipt = receipt;
        _db.JournalEntries.Add(journalEntry);

        // Single SaveChanges ensuring atomicity via EF Core implicit transaction
        await _db.SaveChangesAsync();

        return payment;
    }

    // ─── Cancel Payment + Cancel Receipt + Reverse Journal ────────────────────
    public async Task CancelPaymentAsync(Guid paymentId, string? reason)
    {
        var payment = await _db.Payments
            .Include(p => p.Installment).ThenInclude(i => i.Contract)
            .Include(p => p.Receipt).ThenInclude(r => r!.JournalEntry)
            .FirstOrDefaultAsync(p => p.Id == paymentId)
            ?? throw new NotFoundException("الدفعة غير موجودة");

        if (payment.IsCancelled)
            throw new ValidationException("الدفعة ملغاة مسبقاً");

        // ── Cancel Payment ──
        payment.IsCancelled = true;
        payment.CancelledAt = DateTime.UtcNow;
        payment.CancelReason = reason;

        // ── Reverse Installment amounts ──
        payment.Installment.PaidAmount      -= payment.Amount;
        payment.Installment.RemainingAmount += payment.Amount;
        payment.Installment.Status           =
            payment.Installment.PaidAmount <= 0 ? InstallmentStatus.Pending : InstallmentStatus.PartiallyPaid;
        payment.Installment.PaymentDate      = null;

        payment.Installment.Contract.RemainingAmount += payment.Amount;
        if (payment.Installment.Contract.Status == ContractStatus.Completed)
            payment.Installment.Contract.Status = ContractStatus.Active;

        // ── Cancel Receipt ──
        if (payment.Receipt != null)
        {
            payment.Receipt.IsCancelled = true;
            payment.Receipt.CancelledAt = DateTime.UtcNow;
            payment.Receipt.CancelReason = reason;

            // ── Reverse Journal Entry ──
            if (payment.Receipt.JournalEntry != null)
            {
                payment.Receipt.JournalEntry.IsReversed = true;

                var cashAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "101") ?? throw new NotFoundException("حساب النقدية غير موجود");
                var clientAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "102") ?? throw new NotFoundException("حساب ذمم العملاء غير موجود");
                var bankAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "103") ?? throw new NotFoundException("حساب البنك غير موجود");

                var creditAccountId = payment.PaymentMethod == PaymentMethod.Transfer ? bankAccount.Id : cashAccount.Id;

                var reversal = new JournalEntry
                {
                    EntryNumber = $"REV-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    EntryDate   = DateTime.UtcNow,
                    Description = $"عكس قيد - إلغاء سند قبض {payment.Receipt.ReceiptNumber}",
                    Type        = JournalEntryType.Cancel,
                    CurrencyId  = payment.Receipt.CurrencyId,
                    ExchangeRate= payment.Receipt.ExchangeRate,
                    Details     = new List<JournalEntryDetail>
                    {
                        new() { AccountId = clientAccount.Id, Debit = payment.Receipt.AmountInBase, Credit = 0 },
                        new() { AccountId = creditAccountId, Debit = 0, Credit = payment.Receipt.AmountInBase }
                    }
                };
                _db.JournalEntries.Add(reversal);
            }
        }

        await _db.SaveChangesAsync();
    }

    // ─── Update Overdue Installments ──────────────────────────────────────────
    public async Task UpdateOverdueInstallmentsAsync()
    {
        var overdue = await _db.Installments
            .Where(i => i.Status == InstallmentStatus.Pending && i.DueDate < DateTime.UtcNow)
            .ToListAsync();

        foreach (var inst in overdue)
            inst.Status = InstallmentStatus.Overdue;

        await _db.SaveChangesAsync();
    }

    // ─── Delete Contract (Cascade) ────────────────────────────────────────────
    public async Task DeleteContractAsync(Guid id)
    {
        // استخدام Transaction لضمان سلامة البيانات
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var contract = await _db.InstallmentContracts
                .FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new NotFoundException("العقد غير موجود");

            // 1. مسح تفاصيل القيود الخاصة بدفعات العقد
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE jed
                FROM JournalEntryDetails jed
                INNER JOIN JournalEntries je ON jed.JournalEntryId = je.Id
                INNER JOIN Receipts r ON je.ReceiptId = r.Id
                INNER JOIN Payments p ON r.PaymentId = p.Id
                WHERE p.ContractId = {0};", id);

            // 2. مسح القيود الخاصة بدفعات العقد
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE je
                FROM JournalEntries je
                INNER JOIN Receipts r ON je.ReceiptId = r.Id
                INNER JOIN Payments p ON r.PaymentId = p.Id
                WHERE p.ContractId = {0};", id);

            // 3. مسح تفاصيل قيد إنشاء العقد
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE jed
                FROM JournalEntryDetails jed
                INNER JOIN JournalEntries je ON jed.JournalEntryId = je.Id
                WHERE je.Type = 'ContractIssue' AND je.Description LIKE '%' + {0} + '%';", contract.ContractNumber);

            // 4. مسح قيد إنشاء العقد
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE FROM JournalEntries 
                WHERE Type = 'ContractIssue' AND Description LIKE '%' + {0} + '%';", contract.ContractNumber);

            // 5. مسح وصولات العقد
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE r
                FROM Receipts r
                INNER JOIN Payments p ON r.PaymentId = p.Id
                WHERE p.ContractId = {0};", id);

            // 6. مسح الدفعات والأقساط والمواد والعقد
            await _db.Database.ExecuteSqlRawAsync(";DELETE FROM Payments WHERE ContractId = {0};", id);
            await _db.Database.ExecuteSqlRawAsync(";DELETE FROM Installments WHERE ContractId = {0};", id);
            await _db.Database.ExecuteSqlRawAsync(";DELETE FROM ContractItems WHERE ContractId = {0};", id);
            await _db.Database.ExecuteSqlRawAsync(";DELETE FROM InstallmentContracts WHERE Id = {0};", id);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

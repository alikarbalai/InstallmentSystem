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

public class BillService : IBillService
{
    private readonly AppDbContext _db;

    public BillService(AppDbContext db) => _db = db;

    // ─── Create Bill ──────────────────────────────────────────────────────
    public async Task<InstallmentBill> CreateBillAsync(CreateBillDto dto)
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

        var bill = new InstallmentBill
        {
            CustomerId       = dto.CustomerId,
            CurrencyId       = dto.CurrencyId,
            ExchangeRate     = currency.ExchangeRate,
            BillNumber       = $"BIL-{DateTime.UtcNow:yyyyMMddHHmmss}",
            BillDate         = dto.BillDate,
            TotalAmount      = totalAmount,
            TotalAmountInBase= amountInBase,
            DownPayment      = dto.DownPayment,
            RemainingAmount  = remaining,
            InstallmentCount = dto.InstallmentCount,
            InstallmentValue = installmentVal,
            Status           = BillStatus.Active,
            CreatedAt        = DateTime.UtcNow
        };

        for (int i = 1; i <= dto.InstallmentCount; i++)
        {
            bill.Installments.Add(new Installment
            {
                InstallmentNumber = i,
                DueDate           = dto.BillDate.AddMonths(i),
                Amount            = installmentVal,
                RemainingAmount   = installmentVal,
                Status            = InstallmentStatus.Pending
            });
        }

        foreach (var item in dto.Items)
        {
            bill.ContractItems.Add(new ContractItem
            {
                ProductId = item.ProductId,
                Quantity  = item.Quantity,
                Price     = item.Price
            });
        }

        _db.InstallmentBills.Add(bill);

        // ── Revenue Recognition (استحقاق الفاتورة) ──
        // من حساب ذمم العملاء (102) إلى المبيعات / الإيرادات (401)
        var clientAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "102") 
            ?? throw new NotFoundException("حساب ذمم العملاء غير موجود بالنظام (102)");
            
        var revenueAccount = await _db.Accounts.FirstOrDefaultAsync(a => a.Code == "401") 
            ?? throw new NotFoundException("حساب الإيرادات غير موجود بالنظام (401)");

        var billJournal = new JournalEntry
        {
            EntryNumber = $"JNL-{DateTime.UtcNow:yyyyMMddHHmmss}",
            EntryDate   = DateTime.UtcNow,
            Description = $"اثبات ذمة مالية للفاتورة رقم {bill.BillNumber} للعميل {customer.FullName}",
            Type        = JournalEntryType.ContractIssue,
            CurrencyId  = bill.CurrencyId,
            ExchangeRate= bill.ExchangeRate,
            Details     = new List<JournalEntryDetail>
            {
                new() { AccountId = clientAccount.Id, Debit = amountInBase, Credit = 0 },
                new() { AccountId = revenueAccount.Id, Debit = 0, Credit = amountInBase }
            }
        };
        _db.JournalEntries.Add(billJournal);

        // حفظ واحد شامل للجميع كمعاملة واحدة
        await _db.SaveChangesAsync();
        return bill;
    }

    // ─── Process Payment + Create Receipt + Journal Entry ─────────────────────
    public async Task<Payment> ProcessPaymentAsync(CreatePaymentDto dto)
    {
        var installment = await _db.Installments
            .Include(i => i.Bill)
            .FirstOrDefaultAsync(i => i.Id == dto.InstallmentId)
            ?? throw new NotFoundException("القسط غير موجود");

        if (dto.Amount <= 0)
            throw new ValidationException("المبلغ يجب أن يكون أكبر من صفر");

        if (dto.Amount > installment.RemainingAmount)
            throw new ValidationException("المبلغ أكبر من المتبقي على هذا القسط");

        // Use Bill's Currency instead of dto.CurrencyId
        var billCurrency = await _db.Currencies.FindAsync(installment.Bill.CurrencyId)
            ?? throw new NotFoundException("عملة الفاتورة غير موجودة");

        var amountInBase = dto.Amount * billCurrency.ExchangeRate;
        
        // ── Payment ──
        var payment = new Payment
        {
            CustomerId    = dto.CustomerId,
            ContractId    = installment.BillId,
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

        installment.Bill.RemainingAmount -= dto.Amount;
        if (installment.Bill.RemainingAmount <= 0)
            installment.Bill.Status = BillStatus.Paid;

        _db.Payments.Add(payment);

        // ── Receipt (سند قبض) ──
        var receiptNumber = $"RCP-{DateTime.UtcNow:yyyyMMddHHmmss}";
        var receipt = new Receipt
        {
            ReceiptNumber = receiptNumber,
            PaymentId     = payment.Id, 
            CustomerId    = dto.CustomerId,
            CurrencyId    = installment.Bill.CurrencyId, // Force bill currency
            Amount        = dto.Amount,
            AmountInBase  = amountInBase,
            ExchangeRate  = billCurrency.ExchangeRate,
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
            .Include(p => p.Installment).ThenInclude(i => i.Bill)
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

        payment.Installment.Bill.RemainingAmount += payment.Amount;
        if (payment.Installment.Bill.Status == BillStatus.Paid)
            payment.Installment.Bill.Status = BillStatus.Active;

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

    // ─── Delete Bill (Cascade) ────────────────────────────────────────────
    public async Task DeleteBillAsync(Guid id)
    {
        // استخدام Transaction لضمان سلامة البيانات
        using var transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var bill = await _db.InstallmentBills
                .FirstOrDefaultAsync(c => c.Id == id)
                ?? throw new NotFoundException("الفاتورة غير موجودة");

            // 1. مسح تفاصيل القيود الخاصة بدفعات الفاتورة
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE jed
                FROM JournalEntryDetails jed
                INNER JOIN JournalEntries je ON jed.JournalEntryId = je.Id
                INNER JOIN Receipts r ON je.ReceiptId = r.Id
                INNER JOIN Payments p ON r.PaymentId = p.Id
                WHERE p.BillId = {0};", id);

            // 2. مسح القيود الخاصة بدفعات الفاتورة
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE je
                FROM JournalEntries je
                INNER JOIN Receipts r ON je.ReceiptId = r.Id
                INNER JOIN Payments p ON r.PaymentId = p.Id
                WHERE p.BillId = {0};", id);

            // 3. مسح تفاصيل قيد إنشاء الفاتورة
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE jed
                FROM JournalEntryDetails jed
                INNER JOIN JournalEntries je ON jed.JournalEntryId = je.Id
                WHERE je.Type = 'ContractIssue' AND je.Description LIKE '%' + {0} + '%';", bill.BillNumber);

            // 4. مسح قيد إنشاء الفاتورة
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE FROM JournalEntries 
                WHERE Type = 'ContractIssue' AND Description LIKE '%' + {0} + '%';", bill.BillNumber);

            // 5. مسح وصولات الفاتورة
            await _db.Database.ExecuteSqlRawAsync(@"
                ;DELETE r
                FROM Receipts r
                INNER JOIN Payments p ON r.PaymentId = p.Id
                WHERE p.BillId = {0};", id);

            // 6. مسح الدفعات والأقساط والمواد والفاتورة
            await _db.Database.ExecuteSqlRawAsync(";DELETE FROM Payments WHERE BillId = {0};", id);
            await _db.Database.ExecuteSqlRawAsync(";DELETE FROM Installments WHERE BillId = {0};", id);
            await _db.Database.ExecuteSqlRawAsync(";DELETE FROM BillItems WHERE BillId = {0};", id);
            await _db.Database.ExecuteSqlRawAsync(";DELETE FROM InstallmentBills WHERE Id = {0};", id);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }
}

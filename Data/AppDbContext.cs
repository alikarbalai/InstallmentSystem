using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using InstallmentSystem.Models;
using InstallmentSystem.Models.Identity;

namespace InstallmentSystem.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Group> Groups { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<UserGroup> UserGroups { get; set; }
    public DbSet<GroupPermission> GroupPermissions { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<InstallmentContract> InstallmentContracts { get; set; }
    public DbSet<Installment> Installments { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Receipt> Receipts { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<ContractItem> ContractItems { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<JournalEntry> JournalEntries { get; set; }
    public DbSet<JournalEntryDetail> JournalEntryDetails { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Decimal precision
        foreach (var prop in modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetColumnType("decimal(18,4)");
        }

        // Guid generation
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var idProp = entityType.FindProperty("Id");
            if (idProp != null && idProp.ClrType == typeof(Guid))
                idProp.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd;
        }

        // ── Currency ──
        modelBuilder.Entity<Currency>()
            .HasIndex(c => c.Code).IsUnique();

        // ── Enums String Conversions ──
        modelBuilder.Entity<InstallmentContract>().Property(c => c.Status).HasConversion<string>();
        modelBuilder.Entity<Installment>().Property(i => i.Status).HasConversion<string>();
        modelBuilder.Entity<Payment>().Property(p => p.PaymentMethod).HasConversion<string>();
        modelBuilder.Entity<Receipt>().Property(r => r.PaymentMethod).HasConversion<string>();
        modelBuilder.Entity<JournalEntry>().Property(j => j.Type).HasConversion<string>();

        // ── Concurrency Tokens ──
        modelBuilder.Entity<InstallmentContract>().Property(c => c.RowVersion).IsRowVersion();
        modelBuilder.Entity<Installment>().Property(i => i.RowVersion).IsRowVersion();

        // ── Receipt → Payment (one-to-one) ──
        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Payment)
            .WithOne(p => p.Receipt)
            .HasForeignKey<Receipt>(r => r.PaymentId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Customer)
            .WithMany()
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Receipt>()
            .HasOne(r => r.Currency)
            .WithMany(c => c.Receipts)
            .HasForeignKey(r => r.CurrencyId)
            .OnDelete(DeleteBehavior.NoAction);

        // ── JournalEntry → Receipt (one-to-one) ──
        modelBuilder.Entity<JournalEntry>()
            .HasOne(j => j.Receipt)
            .WithOne(r => r.JournalEntry)
            .HasForeignKey<JournalEntry>(j => j.ReceiptId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        // ── Payment cascades ──
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Customer)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Contract)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.ContractId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Installment)
            .WithMany(i => i.Payments)
            .HasForeignKey(p => p.InstallmentId)
            .OnDelete(DeleteBehavior.NoAction);

        // ── ContractItem cascades ──
        modelBuilder.Entity<ContractItem>()
            .HasOne(ci => ci.Contract)
            .WithMany(c => c.ContractItems)
            .HasForeignKey(ci => ci.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContractItem>()
            .HasOne(ci => ci.Product)
            .WithMany(p => p.ContractItems)
            .HasForeignKey(ci => ci.ProductId)
            .OnDelete(DeleteBehavior.NoAction);

        // ── Installment cascades ──
        modelBuilder.Entity<Installment>()
            .HasOne(i => i.Contract)
            .WithMany(c => c.Installments)
            .HasForeignKey(i => i.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── InstallmentContract cascades ──
        modelBuilder.Entity<InstallmentContract>()
            .HasOne(c => c.Customer)
            .WithMany(cu => cu.Contracts)
            .HasForeignKey(c => c.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<InstallmentContract>()
            .HasOne(c => c.Currency)
            .WithMany(cu => cu.Contracts)
            .HasForeignKey(c => c.CurrencyId)
            .OnDelete(DeleteBehavior.NoAction);

        // ── Self-referencing Account ──
        modelBuilder.Entity<Account>()
            .HasOne(a => a.Parent)
            .WithMany(a => a.Children)
            .HasForeignKey(a => a.ParentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.NoAction);

        // ── Seed Currencies ──
        var iqd = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var usd = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var eur = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        modelBuilder.Entity<Currency>().HasData(
            new Currency { Id = iqd, Name = "دينار عراقي", Code = "IQD", Symbol = "د.ع", ExchangeRate = 1,    IsBase = true,  IsActive = true },
            new Currency { Id = usd, Name = "دولار أمريكي", Code = "USD", Symbol = "$",   ExchangeRate = 1320, IsBase = false, IsActive = true },
            new Currency { Id = eur, Name = "يورو",         Code = "EUR", Symbol = "€",   ExchangeRate = 1450, IsBase = false, IsActive = true }
        );

        // ── Identity & Permissions Setup ──
        modelBuilder.Entity<UserGroup>(b => {
            b.HasKey(ug => new { ug.UserId, ug.GroupId });
            b.HasOne(ug => ug.User).WithMany(u => u.UserGroups).HasForeignKey(ug => ug.UserId);
            b.HasOne(ug => ug.Group).WithMany(g => g.UserGroups).HasForeignKey(ug => ug.GroupId);
        });

        modelBuilder.Entity<GroupPermission>(b => {
            b.HasKey(gp => new { gp.GroupId, gp.PermissionId });
            b.HasOne(gp => gp.Group).WithMany(g => g.GroupPermissions).HasForeignKey(gp => gp.GroupId);
            b.HasOne(gp => gp.Permission).WithMany(p => p.GroupPermissions).HasForeignKey(gp => gp.PermissionId);
        });
    }
}

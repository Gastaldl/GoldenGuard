using GoldenGuard.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoldenGuard.Data;

public sealed class GgDbContext(DbContextOptions<GgDbContext> options) : DbContext(options)
{
    public DbSet<UserProfile> Users => Set<UserProfile>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<UserProfile>(e =>
        {
            e.ToTable("USERS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.Name).HasColumnName("NAME").HasMaxLength(120).IsRequired();
            e.Property(x => x.Email).HasColumnName("EMAIL").HasMaxLength(200).IsRequired();
            e.Property(x => x.MonthlyIncome).HasColumnName("MONTHLY_INCOME").HasPrecision(12, 2).HasDefaultValue(0);
            e.Property(x => x.CreatedAt).HasColumnName("CREATED_AT");
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Transaction>(e =>
        {
            e.ToTable("TRANSACTIONS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("USER_ID").IsRequired();
            e.Property(x => x.Operator).HasColumnName("OPERATOR").HasMaxLength(120).IsRequired();
            e.Property(x => x.Kind).HasColumnName("KIND").HasMaxLength(30).IsRequired();
            e.Property(x => x.Amount).HasColumnName("AMOUNT").HasPrecision(12, 2).IsRequired();
            e.Property(x => x.OccurredAt).HasColumnName("OCCURRED_AT").IsRequired();
            e.Property(x => x.RawLabel).HasColumnName("RAW_LABEL").HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasColumnName("CREATED_AT");

            e.HasOne<UserProfile>()
             .WithMany()
             .HasForeignKey(x => x.UserId)
             .HasConstraintName("FK_TX_USER");
            e.HasIndex(x => new { x.UserId, x.OccurredAt }).HasDatabaseName("IX_TX_USER_DATE");
        });

        b.Entity<UserAccount>(e =>
        {
            e.ToTable("USER_ACCOUNTS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("USER_ID").IsRequired();
            e.Property(x => x.Username).HasColumnName("USERNAME").HasMaxLength(60).IsRequired();
            e.Property(x => x.PasswordHash).HasColumnName("PASSWORD_HASH").HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasColumnName("ROLE").HasMaxLength(20).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("CREATED_AT");
            e.HasIndex(x => x.Username).IsUnique();
        });
    }
}
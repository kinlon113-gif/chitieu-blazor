// Data/AppDbContext.cs
using ChiTieu.Data.Entities;
using ChiTieu.Services;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChiTieu.Data;

public class AppDbContext : IdentityDbContext<AppUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Group>           Groups           { get; set; }
    public DbSet<GroupMember>     GroupMembers     { get; set; }
    public DbSet<Transaction>     Transactions     { get; set; }
    public DbSet<SplitBill>       SplitBills       { get; set; }
    public DbSet<SplitBillItem>   SplitBillItems   { get; set; }
    public DbSet<Debt>            Debts            { get; set; }
    public DbSet<Budget>          Budgets          { get; set; }
    public DbSet<SharedFund>      SharedFunds      { get; set; }
    public DbSet<FundTransaction> FundTransactions { get; set; }
    public DbSet<Notification>    Notifications    { get; set; }
    public DbSet<UserEmailConfig> UserEmailConfigs { get; set; }
    public DbSet<UserLocationShare> UserLocationShares { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Group
        builder.Entity<Group>(e =>
        {
            e.HasIndex(g => g.InviteCode).IsUnique();
            e.HasOne(g => g.Owner).WithMany().HasForeignKey(g => g.OwnerId).OnDelete(DeleteBehavior.Restrict);
        });

        // GroupMember — unique per user per group
        builder.Entity<GroupMember>(e =>
        {
            e.HasIndex(m => new { m.GroupId, m.UserId }).IsUnique();
            e.HasOne(m => m.Group).WithMany(g => g.Members).HasForeignKey(m => m.GroupId);
            e.HasOne(m => m.User).WithMany(u => u.GroupMemberships).HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Transaction
        builder.Entity<Transaction>(e =>
        {
            e.Property(t => t.Amount).HasPrecision(18, 0);
            e.Property(t => t.LocationName).HasMaxLength(160);
            e.HasOne(t => t.Group).WithMany(g => g.Transactions).HasForeignKey(t => t.GroupId);
            e.HasOne(t => t.User).WithMany(u => u.Transactions).HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(t => new { t.GroupId, t.Month });
            e.HasIndex(t => new { t.GroupId, t.Latitude, t.Longitude });
        });

        // SplitBill
        builder.Entity<SplitBill>(e =>
        {
            e.Property(s => s.TotalAmount).HasPrecision(18, 0);
            e.HasOne(s => s.Group).WithMany().HasForeignKey(s => s.GroupId);
        });

        builder.Entity<SplitBillItem>(e =>
        {
            e.Property(s => s.Amount).HasPrecision(18, 2);
            e.Property(s => s.Percent).HasPrecision(5, 2);
        });

        // Debt
        builder.Entity<Debt>(e =>
        {
            e.Property(d => d.Amount).HasPrecision(18, 0);
            e.HasOne(d => d.Group).WithMany().HasForeignKey(d => d.GroupId);
            e.HasOne(d => d.Debtor).WithMany().HasForeignKey(d => d.DebtorId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(d => d.Creditor).WithMany().HasForeignKey(d => d.CreditorId).OnDelete(DeleteBehavior.Restrict);
        });

        // Budget
        builder.Entity<Budget>(e =>
        {
            e.Property(b => b.Amount).HasPrecision(18, 0);
            e.HasIndex(b => new { b.GroupId, b.CategoryId, b.Month }).IsUnique();
            e.HasOne(b => b.Group).WithMany(g => g.Budgets).HasForeignKey(b => b.GroupId);
        });

        // SharedFund
        builder.Entity<SharedFund>(e =>
        {
            e.Property(f => f.Balance).HasPrecision(18, 0);
            e.HasOne(f => f.Group).WithMany(g => g.Funds).HasForeignKey(f => f.GroupId);
        });

        builder.Entity<FundTransaction>(e =>
        {
            e.Property(f => f.Amount).HasPrecision(18, 0);
            e.HasOne(f => f.Fund).WithMany(f => f.Transactions).HasForeignKey(f => f.FundId);
            e.HasOne(f => f.User).WithMany().HasForeignKey(f => f.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // Notification
        builder.Entity<Notification>(e =>
        {
            e.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(n => n.Group).WithMany().HasForeignKey(n => n.GroupId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserLocationShare>(e =>
        {
            e.Property(l => l.Label).HasMaxLength(160);
            e.HasIndex(l => new { l.GroupId, l.UserId }).IsUnique();
            e.HasOne(l => l.Group).WithMany().HasForeignKey(l => l.GroupId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(l => l.User).WithMany().HasForeignKey(l => l.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<UserEmailConfig>(e =>
        {
            e.HasIndex(c => new { c.UserId, c.GroupId }).IsUnique();
        });
    }
}

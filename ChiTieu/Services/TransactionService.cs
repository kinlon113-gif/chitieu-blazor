// Services/TransactionService.cs
using ChiTieu.Data;
using ChiTieu.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChiTieu.Services;

public class TransactionService
{
    private readonly AppDbContext _db;
    private readonly NotificationService _notif;

    public TransactionService(AppDbContext db, NotificationService notif)
    {
        _db    = db;
        _notif = notif;
    }

    // Lấy tất cả giao dịch trong nhóm theo tháng
    public async Task<List<Transaction>> GetByGroupMonthAsync(int groupId, string month)
        => await _db.Transactions
            .Include(t => t.User)
            .Where(t => t.GroupId == groupId && t.Month == month)
            .OrderByDescending(t => t.Date)
            .ToListAsync();

    // Tổng thu/chi theo tháng
    public async Task<(decimal Income, decimal Expense)> GetSummaryAsync(int groupId, string month)
    {
        var txs = await GetByGroupMonthAsync(groupId, month);
        return (
            txs.Where(t => t.Type == "income").Sum(t => t.Amount),
            txs.Where(t => t.Type == "expense").Sum(t => t.Amount)
        );
    }

    // Chi theo danh mục
    public async Task<Dictionary<string, decimal>> GetExpenseByCategoryAsync(int groupId, string month)
    {
        var txs = await _db.Transactions
            .Where(t => t.GroupId == groupId && t.Month == month && t.Type == "expense")
            .ToListAsync();
        return txs.GroupBy(t => t.Category)
                  .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    }

    // Chi theo từng thành viên
    public async Task<Dictionary<string, decimal>> GetExpenseByMemberAsync(int groupId, string month)
    {
        var txs = await _db.Transactions
            .Include(t => t.User)
            .Where(t => t.GroupId == groupId && t.Month == month && t.Type == "expense")
            .ToListAsync();
        return txs.GroupBy(t => t.User.DisplayName)
                  .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    }

    // Thêm giao dịch mới
    public async Task<Transaction> AddAsync(Transaction tx)
    {
        tx.Month = $"{tx.Date:yyyy-MM}";
        _db.Transactions.Add(tx);
        await _db.SaveChangesAsync();

        // Gửi thông báo cho thành viên khác
        await _notif.NotifyGroupAsync(tx.GroupId, tx.UserId, "new_expense",
            "Giao dịch mới",
            $"Có khoản {(tx.Type == "income" ? "thu" : "chi")} {tx.Amount:N0}đ được thêm vào nhóm");

        // Kiểm tra vượt ngân sách
        await CheckBudgetAlertAsync(tx);

        return tx;
    }

    // Thêm từ email VCB
    public async Task<Transaction?> AddFromVcbEmailAsync(
        int groupId, string userId, VcbTransaction vcb)
    {
        // Kiểm tra trùng mã lệnh
        if (vcb.OrderId != null)
        {
            var exists = await _db.Transactions.AnyAsync(t =>
                t.GroupId == groupId && t.VcbOrderId == vcb.OrderId);
            if (exists) return null;
        }

        var tx = new Transaction
        {
            GroupId    = groupId,
            UserId     = userId,
            Type       = "expense",
            Amount     = vcb.Amount,
            Category   = vcb.CategoryId,
            Note       = vcb.Note,
            Date       = vcb.Date,
            FromEmail  = true,
            VcbOrderId = vcb.OrderId,
        };

        return await AddAsync(tx);
    }

    public async Task DeleteAsync(int id, string userId)
    {
        var tx = await _db.Transactions.FindAsync(id);
        if (tx == null || tx.UserId != userId) return;
        _db.Transactions.Remove(tx);
        await _db.SaveChangesAsync();
    }

    // Kiểm tra cảnh báo ngân sách
    private async Task CheckBudgetAlertAsync(Transaction tx)
    {
        if (tx.Type != "expense") return;

        var budget = await _db.Budgets.FirstOrDefaultAsync(b =>
            b.GroupId == tx.GroupId &&
            b.CategoryId == tx.Category &&
            b.Month == tx.Month);

        if (budget == null) return;

        var spent = await _db.Transactions
            .Where(t => t.GroupId == tx.GroupId &&
                        t.Type == "expense" &&
                        t.Category == tx.Category &&
                        t.Month == tx.Month)
            .SumAsync(t => t.Amount);

        var pct = spent / budget.Amount;
        if (pct >= 1.0m)
        {
            await _notif.NotifyGroupAsync(tx.GroupId, "", "over_budget",
                "⚠️ Vượt ngân sách!",
                $"Danh mục đã dùng {pct:P0} ngân sách tháng này ({spent:N0}đ / {budget.Amount:N0}đ)");
        }
        else if (pct >= 0.8m)
        {
            await _notif.NotifyGroupAsync(tx.GroupId, "", "over_budget",
                "⚠️ Gần vượt ngân sách",
                $"Đã dùng {pct:P0} ngân sách ({spent:N0}đ / {budget.Amount:N0}đ)");
        }
    }
}

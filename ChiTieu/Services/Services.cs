// Services/GroupService.cs
using ChiTieu.Data;
using ChiTieu.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ChiTieu.Services;

public class GroupService
{
    private readonly AppDbContext _db;

    public GroupService(AppDbContext db) => _db = db;

    public async Task<List<Group>> GetUserGroupsAsync(string userId)
        => await _db.Groups
            .Include(g => g.Members).ThenInclude(m => m.User)
            .Where(g => g.Members.Any(m => m.UserId == userId))
            .ToListAsync();

    public async Task<Group> CreateAsync(string name, string description, string ownerId)
    {
        var code  = GenerateCode();
        var group = new Group
        {
            Name = name, Description = description,
            OwnerId = ownerId, InviteCode = code,
        };
        _db.Groups.Add(group);
        await _db.SaveChangesAsync();

        _db.GroupMembers.Add(new GroupMember
        {
            GroupId = group.Id, UserId = ownerId, Role = "owner"
        });
        await _db.SaveChangesAsync();
        return group;
    }

    public async Task<Group?> JoinByCodeAsync(string code, string userId)
    {
        var group = await _db.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.InviteCode == code.ToUpper());

        if (group == null) throw new Exception("Mã không hợp lệ");
        if (group.Members.Any(m => m.UserId == userId)) throw new Exception("Bạn đã là thành viên");

        _db.GroupMembers.Add(new GroupMember
        {
            GroupId = group.Id, UserId = userId, Role = "member"
        });
        await _db.SaveChangesAsync();
        return group;
    }

    public async Task LeaveAsync(int groupId, string userId)
    {
        var group  = await _db.Groups.FindAsync(groupId);
        if (group?.OwnerId == userId) throw new Exception("Chủ nhóm không thể rời nhóm");
        var member = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
        if (member != null) { _db.GroupMembers.Remove(member); await _db.SaveChangesAsync(); }
    }

    public async Task RemoveMemberAsync(int groupId, string ownerId, string memberId)
    {
        var group = await _db.Groups.FindAsync(groupId);
        if (group == null || group.OwnerId != ownerId) throw new Exception("Khong co quyen xoa thanh vien");
        if (memberId == ownerId) throw new Exception("Chu nhom khong the tu xoa minh");

        var member = await _db.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == memberId);
        if (member != null)
        {
            _db.GroupMembers.Remove(member);
            await _db.SaveChangesAsync();
        }
    }

    public async Task DeleteAsync(int groupId, string ownerId)
    {
        var group = await _db.Groups.FindAsync(groupId);
        if (group == null || group.OwnerId != ownerId) throw new Exception("Không có quyền xóa nhóm");
        _db.Groups.Remove(group);
        await _db.SaveChangesAsync();
    }

    private static string GenerateCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var rng  = new Random();
        return new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
    }
}

// ─── DEBT SERVICE ──────────────────────────────────────────────
public class DebtService
{
    private readonly AppDbContext _db;
    public DebtService(AppDbContext db) => _db = db;

    public async Task<List<Debt>> GetGroupDebtsAsync(int groupId)
        => await _db.Debts
            .Include(d => d.Debtor)
            .Include(d => d.Creditor)
            .Where(d => d.GroupId == groupId && !d.IsSettled)
            .ToListAsync();

    // Tính lại công nợ từ split bills
    public async Task RecalculateFromSplitAsync(int splitBillId)
    {
        var bill = await _db.SplitBills
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == splitBillId);
        if (bill == null) return;

        foreach (var item in bill.Items.Where(i => i.UserId != bill.PaidBy && !i.IsPaid))
        {
            var existing = await _db.Debts.FirstOrDefaultAsync(d =>
                d.GroupId == bill.GroupId &&
                d.DebtorId == item.UserId &&
                d.CreditorId == bill.PaidBy &&
                !d.IsSettled);

            if (existing != null)
                existing.Amount += item.Amount;
            else
                _db.Debts.Add(new Debt
                {
                    GroupId    = bill.GroupId,
                    DebtorId   = item.UserId,
                    CreditorId = bill.PaidBy,
                    Amount     = item.Amount,
                    Note       = bill.Description,
                });
        }
        await _db.SaveChangesAsync();
    }

    public async Task SettleAsync(int debtId, string userId)
    {
        var debt = await _db.Debts.FindAsync(debtId);
        if (debt == null || (debt.DebtorId != userId && debt.CreditorId != userId)) return;
        debt.IsSettled = true;
        debt.SettledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }
}

// ─── NOTIFICATION SERVICE ──────────────────────────────────────
public class NotificationService
{
    private readonly AppDbContext _db;
    public NotificationService(AppDbContext db) => _db = db;

    public async Task<List<Notification>> GetUserNotifAsync(string userId, int limit = 20)
        => await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();

    public async Task<int> GetUnreadCountAsync(string userId)
        => await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);

    public async Task MarkAllReadAsync(string userId)
    {
        var notifs = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
        notifs.ForEach(n => n.IsRead = true);
        await _db.SaveChangesAsync();
    }

    public async Task NotifyUserAsync(int groupId, string userId, string type, string title, string message)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            GroupId = groupId,
            Type = type,
            Title = title,
            Message = message,
        });
        await _db.SaveChangesAsync();
    }

    // Gửi thông báo tới tất cả thành viên (trừ người tạo)
    public async Task NotifyGroupAsync(int groupId, string exceptUserId, string type, string title, string message)
    {
        var memberIds = await _db.GroupMembers
            .Where(m => m.GroupId == groupId && m.UserId != exceptUserId)
            .Select(m => m.UserId)
            .ToListAsync();

        var notifs = memberIds.Select(uid => new Notification
        {
            UserId  = uid, GroupId = groupId,
            Type    = type, Title  = title,
            Message = message,
        }).ToList();

        _db.Notifications.AddRange(notifs);
        await _db.SaveChangesAsync();
    }
}

// ─── SPLIT BILL SERVICE ───────────────────────────────────────
public class SplitBillService
{
    private readonly AppDbContext _db;
    private readonly DebtService  _debt;
    private readonly NotificationService _notif;

    public SplitBillService(AppDbContext db, DebtService debt, NotificationService notif)
    {
        _db   = db;
        _debt = debt;
        _notif = notif;
    }

    public async Task<List<SplitBill>> GetGroupSplitsAsync(int groupId)
        => await _db.SplitBills
            .Include(s => s.Items).ThenInclude(i => i.User)
            .Where(s => s.GroupId == groupId)
            .OrderByDescending(s => s.Date)
            .ToListAsync();

    // Chia đều
    public async Task<SplitBill> SplitEquallyAsync(
        int groupId, string paidByUserId,
        decimal total, string description,
        List<string> participantIds)
    {
        var perPerson = Math.Round(total / participantIds.Count, 0);
        var bill = new SplitBill
        {
            GroupId = groupId, PaidBy = paidByUserId,
            TotalAmount = total, SplitType = "equal",
            Description = description, Date = DateTime.UtcNow,
        };
        _db.SplitBills.Add(bill);
        await _db.SaveChangesAsync();

        foreach (var uid in participantIds)
        {
            _db.SplitBillItems.Add(new SplitBillItem
            {
                SplitBillId = bill.Id,
                UserId      = uid,
                Amount      = perPerson,
                Percent     = 100m / participantIds.Count,
                IsPaid      = uid == paidByUserId,
            });
        }
        await _db.SaveChangesAsync();
        await _debt.RecalculateFromSplitAsync(bill.Id);
        await NotifySplitDebtorsAsync(bill.Id);
        return bill;
    }

    // Chia theo %
    public async Task<SplitBill> SplitByPercentAsync(
        int groupId, string paidByUserId,
        decimal total, string description,
        Dictionary<string, decimal> userPercents) // userId -> percent
    {
        var bill = new SplitBill
        {
            GroupId = groupId, PaidBy = paidByUserId,
            TotalAmount = total, SplitType = "percent",
            Description = description, Date = DateTime.UtcNow,
        };
        _db.SplitBills.Add(bill);
        await _db.SaveChangesAsync();

        foreach (var (uid, pct) in userPercents)
        {
            _db.SplitBillItems.Add(new SplitBillItem
            {
                SplitBillId = bill.Id, UserId = uid,
                Amount      = Math.Round(total * pct / 100, 0),
                Percent     = pct, IsPaid = uid == paidByUserId,
            });
        }
        await _db.SaveChangesAsync();
        await _debt.RecalculateFromSplitAsync(bill.Id);
        await NotifySplitDebtorsAsync(bill.Id);
        return bill;
    }

    // Chia theo số tiền cụ thể
    public async Task<SplitBill> SplitByAmountAsync(
        int groupId, string paidByUserId,
        decimal total, string description,
        Dictionary<string, decimal> userAmounts) // userId -> amount
    {
        var bill = new SplitBill
        {
            GroupId = groupId, PaidBy = paidByUserId,
            TotalAmount = total, SplitType = "custom",
            Description = description, Date = DateTime.UtcNow,
        };
        _db.SplitBills.Add(bill);
        await _db.SaveChangesAsync();

        foreach (var (uid, amt) in userAmounts)
        {
            _db.SplitBillItems.Add(new SplitBillItem
            {
                SplitBillId = bill.Id, UserId = uid,
                Amount      = amt,
                Percent     = total > 0 ? amt / total * 100 : 0,
                IsPaid      = uid == paidByUserId,
            });
        }
        await _db.SaveChangesAsync();
        await _debt.RecalculateFromSplitAsync(bill.Id);
        await NotifySplitDebtorsAsync(bill.Id);
        return bill;
    }

    private async Task NotifySplitDebtorsAsync(int splitBillId)
    {
        var bill = await _db.SplitBills
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == splitBillId);
        if (bill == null) return;

        foreach (var item in bill.Items.Where(i => i.UserId != bill.PaidBy && !i.IsPaid))
        {
            await _notif.NotifyUserAsync(
                bill.GroupId,
                item.UserId,
                "debt_reminder",
                "Khoan can thanh toan",
                $"{bill.Description}: ban can thanh toan {item.Amount:N0}d");
        }
    }
}

// ─── BUDGET SERVICE ───────────────────────────────────────────
public class BudgetService
{
    private readonly AppDbContext _db;
    public BudgetService(AppDbContext db) => _db = db;

    public async Task<List<Budget>> GetByGroupMonthAsync(int groupId, string month)
        => await _db.Budgets.Where(b => b.GroupId == groupId && b.Month == month).ToListAsync();

    public async Task SetAsync(int groupId, string categoryId, string month, decimal amount)
    {
        var existing = await _db.Budgets.FirstOrDefaultAsync(b =>
            b.GroupId == groupId && b.CategoryId == categoryId && b.Month == month);

        if (existing != null)
            existing.Amount = amount;
        else
            _db.Budgets.Add(new Budget { GroupId = groupId, CategoryId = categoryId, Month = month, Amount = amount });

        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var b = await _db.Budgets.FindAsync(id);
        if (b != null) { _db.Budgets.Remove(b); await _db.SaveChangesAsync(); }
    }
}

// Data/Entities/Models.cs
using Microsoft.AspNetCore.Identity;

namespace ChiTieu.Data.Entities;

// ─── USER ─────────────────────────────────────────────────────
public class AppUser : IdentityUser
{
    public string   DisplayName { get; set; } = "";
    public DateTime CreatedAt   { get; set; } = DateTime.UtcNow;
    public List<GroupMember> GroupMemberships { get; set; } = new();
    public List<Transaction> Transactions     { get; set; } = new();
}

// ─── GROUP ────────────────────────────────────────────────────
public class Group
{
    public int    Id          { get; set; }
    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public string InviteCode  { get; set; } = "";   // 6-char unique code
    public string OwnerId     { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser           Owner   { get; set; } = null!;
    public List<GroupMember> Members { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
    public List<Budget>      Budgets      { get; set; } = new();
    public List<SharedFund>  Funds        { get; set; } = new();
}

public class GroupMember
{
    public int    Id      { get; set; }
    public int    GroupId { get; set; }
    public string UserId  { get; set; } = "";
    public string Role    { get; set; } = "member"; // owner | member
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Group   Group { get; set; } = null!;
    public AppUser User  { get; set; } = null!;
}

// ─── TRANSACTION ──────────────────────────────────────────────
public class Transaction
{
    public int      Id         { get; set; }
    public int      GroupId    { get; set; }
    public string   UserId     { get; set; } = "";
    public string   Type       { get; set; } = "expense"; // income | expense
    public decimal  Amount     { get; set; }
    public string   Category   { get; set; } = "other";
    public string   Note       { get; set; } = "";
    public bool     IsShared   { get; set; } = false;
    public DateTime Date       { get; set; } = DateTime.UtcNow;
    public string   Month      { get; set; } = "";  // "2024-06"
    public bool     FromEmail  { get; set; } = false;
    public string?  VcbOrderId { get; set; }        // mã lệnh VCB
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    // Receipt / invoice
    public string? ReceiptUrl  { get; set; }
    public double?  Latitude   { get; set; }
    public double?  Longitude  { get; set; }
    public double?  LocationAccuracy { get; set; }
    public string   LocationName { get; set; } = "";
    public DateTime? CheckedInAt { get; set; }

    public Group   Group { get; set; } = null!;
    public AppUser User  { get; set; } = null!;
}

public class UserLocationShare
{
    public int      Id        { get; set; }
    public int      GroupId   { get; set; }
    public string   UserId    { get; set; } = "";
    public bool     IsSharing { get; set; } = true;
    public double   Latitude  { get; set; }
    public double   Longitude { get; set; }
    public double?  Accuracy  { get; set; }
    public string   Label     { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group   Group { get; set; } = null!;
    public AppUser User  { get; set; } = null!;
}

// ─── SPLIT BILL ───────────────────────────────────────────────
public class SplitBill
{
    public int     Id          { get; set; }
    public int     GroupId     { get; set; }
    public string  PaidBy      { get; set; } = "";
    public decimal TotalAmount { get; set; }
    public string  SplitType   { get; set; } = "equal"; // equal | percent | custom
    public string  Description { get; set; } = "";
    public DateTime Date       { get; set; } = DateTime.UtcNow;
    public bool    IsSettled   { get; set; } = false;

    public Group   Group { get; set; } = null!;
    public List<SplitBillItem> Items { get; set; } = new();
}

public class SplitBillItem
{
    public int     Id          { get; set; }
    public int     SplitBillId { get; set; }
    public string  UserId      { get; set; } = "";
    public decimal Amount      { get; set; }
    public decimal Percent     { get; set; }
    public bool    IsPaid      { get; set; } = false;

    public SplitBill Bill { get; set; } = null!;
    public AppUser   User { get; set; } = null!;
}

// ─── DEBT ─────────────────────────────────────────────────────
public class Debt
{
    public int     Id        { get; set; }
    public int     GroupId   { get; set; }
    public string  DebtorId  { get; set; } = "";   // người nợ
    public string  CreditorId{ get; set; } = "";   // người được nhận
    public decimal Amount    { get; set; }
    public bool    IsSettled { get; set; } = false;
    public DateTime? SettledAt { get; set; }
    public string   Note     { get; set; } = "";
    public DateTime CreatedAt{ get; set; } = DateTime.UtcNow;

    public Group   Group    { get; set; } = null!;
    public AppUser Debtor   { get; set; } = null!;
    public AppUser Creditor { get; set; } = null!;
}

// ─── BUDGET ───────────────────────────────────────────────────
public class Budget
{
    public int     Id         { get; set; }
    public int     GroupId    { get; set; }
    public string  CategoryId { get; set; } = "";
    public decimal Amount     { get; set; }
    public string  Month      { get; set; } = "";  // "2024-06"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
}

// ─── SHARED FUND ──────────────────────────────────────────────
public class SharedFund
{
    public int     Id      { get; set; }
    public int     GroupId { get; set; }
    public string  Name    { get; set; } = "Quỹ chung";
    public decimal Balance { get; set; } = 0;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Group Group { get; set; } = null!;
    public List<FundTransaction> Transactions { get; set; } = new();
}

public class FundTransaction
{
    public int     Id      { get; set; }
    public int     FundId  { get; set; }
    public string  UserId  { get; set; } = "";
    public string  Type    { get; set; } = "deposit"; // deposit | withdraw
    public decimal Amount  { get; set; }
    public string  Note    { get; set; } = "";
    public DateTime Date   { get; set; } = DateTime.UtcNow;

    public SharedFund Fund { get; set; } = null!;
    public AppUser    User { get; set; } = null!;
}

// ─── NOTIFICATION ─────────────────────────────────────────────
public class Notification
{
    public int    Id      { get; set; }
    public string UserId  { get; set; } = "";
    public int    GroupId { get; set; }
    public string Type    { get; set; } = ""; // new_expense | over_budget | new_member | debt_reminder
    public string Title   { get; set; } = "";
    public string Message { get; set; } = "";
    public bool   IsRead  { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser User  { get; set; } = null!;
    public Group   Group { get; set; } = null!;
}

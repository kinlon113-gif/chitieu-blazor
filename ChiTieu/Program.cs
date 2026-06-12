// Program.cs
using ChiTieu.Data;
using ChiTieu.Data.Entities;
using ChiTieu.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using MudBlazor.Services;
using Npgsql;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();

var port = Environment.GetEnvironmentVariable("PORT");
builder.WebHost.UseUrls($"http://0.0.0.0:{(string.IsNullOrWhiteSpace(port) ? "8080" : port)}");

// ─── DATABASE ──────────────────────────────────────────────────
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine("==== DATABASE CONFIG ====");
Console.WriteLine(string.IsNullOrWhiteSpace(connectionString)
    ? "DefaultConnection is NULL or EMPTY"
    : MaskConnectionString(connectionString));
Console.WriteLine("=========================");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        connectionString,
        npgsql =>
        {
            npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null);
            npgsql.CommandTimeout(120);
        }));

// ─── IDENTITY ─────────────────────────────────────────────────
builder.Services.AddIdentity<AppUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";
    options.SlidingExpiration = true;
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// ─── BLAZOR ───────────────────────────────────────────────────
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();

// ─── SERVICES ─────────────────────────────────────────────────
builder.Services.AddScoped<GroupService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<DebtService>();
builder.Services.AddScoped<SplitBillService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<VcbEmailParserService>();

// Tạm thời tắt background email khi deploy test DB.
// Bật lại sau khi Supabase chạy ổn.
// builder.Services.AddHostedService<VcbEmailBackgroundService>();

// ─── CORS ─────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

// ─── TEST + MIGRATE DATABASE KHI KHỞI ĐỘNG ────────────────────
try
{
    using var scope = app.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    Console.WriteLine("Skipping startup Supabase connection check so Railway can bind the port quickly.");

    var canConnect = false;

    if (canConnect)
    {
        var autoMigrate = !string.Equals(
            Environment.GetEnvironmentVariable("AUTO_MIGRATE"),
            "false",
            StringComparison.OrdinalIgnoreCase);

        if (autoMigrate)
        {
            Console.WriteLine("Running database migration...");
            await db.Database.MigrateAsync();
            Console.WriteLine("DATABASE MIGRATION SUCCESS");
        }
        else
        {
            Console.WriteLine("SUPABASE CONNECT OK - SKIP AUTO MIGRATION (AUTO_MIGRATE=false)");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine("SUPABASE CONNECT OR MIGRATION FAILED");
    Console.WriteLine(ex.ToString());
}

if (args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase))
{
    await RunSmokeTestAsync(app.Services);
    return;
}

if (args.Contains("--cleanup-smoke-users", StringComparer.OrdinalIgnoreCase))
{
    await CleanupSmokeUsersAsync(app.Services);
    return;
}

// ─── MIDDLEWARE ───────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

static string AccountPage(string title, string body)
{
    return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>{{title}} - Chi Tieu</title>
    <link rel="stylesheet" href="/css/app.css" />
</head>
<body>
    <main style="min-height:100vh;display:grid;place-items:center;padding:24px;background:var(--bg);">
        <section class="card" style="width:min(420px,100%);padding:28px;">
            {{body}}
        </section>
    </main>
</body>
</html>
""";
}

static bool IsLocalReturnUrl(string? returnUrl)
    => !string.IsNullOrWhiteSpace(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//");

app.MapGet("/account/login", (string? returnUrl) =>
{
    var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(returnUrl ?? "/dashboard");
    var html = AccountPage("Dang nhap", $$"""
<h1 class="page-title" style="margin-bottom:8px">Dang nhap</h1>
<p class="page-sub" style="margin-bottom:20px">Vao Chi Tieu de quan ly thu chi.</p>
<form method="post" action="/account/login" style="display:grid;gap:14px">
    <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}" />
    <div class="input-group">
        <label class="input-label">Email</label>
        <input class="input" type="email" name="email" autocomplete="email" required />
    </div>
    <div class="input-group">
        <label class="input-label">Mat khau</label>
        <input class="input" type="password" name="password" autocomplete="current-password" required />
    </div>
    <button class="btn btn-primary btn-block" type="submit">Dang nhap</button>
</form>
<p style="margin-top:16px;color:var(--text-2)">Chua co tai khoan? <a href="/account/register">Dang ky</a></p>
""");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/account/login", async (
    HttpContext http,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    ILogger<Program> logger) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();

    try
    {
        var user = await userManager.FindByEmailAsync(email)
            ?? await userManager.FindByNameAsync(email);

        if (user == null)
        {
            logger.LogWarning("Login failed: user not found for {Email}", email);
            return Results.Redirect("/account/login?error=1");
        }

        var result = await signInManager.PasswordSignInAsync(user, password, isPersistent: true, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return Results.Redirect(IsLocalReturnUrl(returnUrl) ? returnUrl : "/dashboard");
        }

        logger.LogWarning(
            "Login failed for {Email}. IsLockedOut={IsLockedOut}, IsNotAllowed={IsNotAllowed}, RequiresTwoFactor={RequiresTwoFactor}",
            email,
            result.IsLockedOut,
            result.IsNotAllowed,
            result.RequiresTwoFactor);

        return Results.Redirect("/account/login?error=1");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Login error for {Email}", email);
        return Results.Redirect("/account/login?error=1");
    }
});

app.MapGet("/account/register", () =>
{
    var html = AccountPage("Dang ky", """
<h1 class="page-title" style="margin-bottom:8px">Dang ky</h1>
<p class="page-sub" style="margin-bottom:20px">Tao tai khoan moi de bat dau.</p>
<form method="post" action="/account/register" style="display:grid;gap:14px">
    <div class="input-group">
        <label class="input-label">Ten hien thi</label>
        <input class="input" name="displayName" autocomplete="name" required />
    </div>
    <div class="input-group">
        <label class="input-label">Email</label>
        <input class="input" type="email" name="email" autocomplete="email" required />
    </div>
    <div class="input-group">
        <label class="input-label">Mat khau</label>
        <input class="input" type="password" name="password" autocomplete="new-password" required />
    </div>
    <button class="btn btn-primary btn-block" type="submit">Dang ky</button>
</form>
<p style="margin-top:16px;color:var(--text-2)">Da co tai khoan? <a href="/account/login">Dang nhap</a></p>
""");
    return Results.Content(html, "text/html; charset=utf-8");
});

app.MapPost("/account/register", async (
    HttpContext http,
    UserManager<AppUser> userManager,
    SignInManager<AppUser> signInManager,
    ILogger<Program> logger) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var displayName = form["displayName"].ToString();

    try
    {
        logger.LogInformation("Register POST received for {Email}", email);

        var existingUser = await userManager.FindByEmailAsync(email)
            ?? await userManager.FindByNameAsync(email);

        if (existingUser != null)
        {
            existingUser.Email = email;
            existingUser.UserName = email;
            existingUser.DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? existingUser.DisplayName
                : displayName;

            var update = await userManager.UpdateAsync(existingUser);
            if (!update.Succeeded)
            {
                logger.LogWarning(
                    "Update existing account failed for {Email}: {Errors}",
                    email,
                    string.Join(", ", update.Errors.Select(e => $"{e.Code}:{e.Description}")));

                return Results.Redirect("/account/register?error=1");
            }

            if (await userManager.HasPasswordAsync(existingUser))
            {
                var removePassword = await userManager.RemovePasswordAsync(existingUser);
                if (!removePassword.Succeeded)
                {
                    logger.LogWarning(
                        "Remove old password failed for {Email}: {Errors}",
                        email,
                        string.Join(", ", removePassword.Errors.Select(e => $"{e.Code}:{e.Description}")));

                    return Results.Redirect("/account/register?error=1");
                }
            }

            var addPassword = await userManager.AddPasswordAsync(existingUser, password);
            if (!addPassword.Succeeded)
            {
                logger.LogWarning(
                    "Add new password failed for {Email}: {Errors}",
                    email,
                    string.Join(", ", addPassword.Errors.Select(e => $"{e.Code}:{e.Description}")));

                return Results.Redirect("/account/register?error=1");
            }

            logger.LogWarning("Reset password for existing account {Email} from register form", email);
            await signInManager.SignInAsync(existingUser, isPersistent: true);
            return Results.Redirect("/dashboard");
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = displayName
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning(
                "Register failed for {Email}: {Errors}",
                email,
                string.Join(", ", result.Errors.Select(e => $"{e.Code}:{e.Description}")));

            return Results.Redirect("/account/register?error=1");
        }

        await signInManager.SignInAsync(user, isPersistent: true);
        return Results.Redirect("/dashboard");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Register error for {Email}", email);
        return Results.Redirect("/account/register?error=1");
    }
});

app.MapPost("/account/logout", async (SignInManager<AppUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.Redirect("/account/login");
});

app.MapGet("/", () => Results.Redirect("/dashboard"));
app.MapGet("/healthz", () => Results.Ok("OK"));
app.MapGet("/react", () => Results.Redirect("/react/index.html"));

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/app", async (
    HttpContext http,
    GroupService groupService,
    TransactionService txService,
    BudgetService budgetService,
    DebtService debtService,
    SplitBillService splitService,
    AppDbContext db,
    string? month) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

    var monthKey = NormalizeMonth(month);
    var groups = await groupService.GetUserGroupsAsync(userId);
    var group = groups.FirstOrDefault();
    if (group == null)
    {
        return Results.Ok(new AppStateResponse(
            null,
            Array.Empty<MemberResponse>(),
            Array.Empty<TransactionResponse>(),
            Array.Empty<BudgetResponse>(),
            Array.Empty<DebtResponse>(),
            null,
            Array.Empty<FundTransactionResponse>(),
            Array.Empty<SplitResponse>(),
            new ReportResponse(0, 0, 0, 0, new(), new())));
    }

    var transactions = await txService.GetByGroupMonthAsync(group.Id, monthKey);
    var budgets = await budgetService.GetByGroupMonthAsync(group.Id, monthKey);
    var spending = await txService.GetExpenseByCategoryAsync(group.Id, monthKey);
    var debts = await debtService.GetGroupDebtsAsync(group.Id);
    var splits = await splitService.GetGroupSplitsAsync(group.Id);
    var fund = await GetOrCreateSharedFundAsync(db, group.Id);
    var fundTransactions = await db.FundTransactions
        .Where(t => t.FundId == fund.Id)
        .OrderByDescending(t => t.Date)
        .Take(30)
        .Select(t => new FundTransactionResponse(t.Id, t.Type, t.Amount, t.Note, t.Date, t.UserId))
        .ToListAsync();

    var income = transactions.Where(t => t.Type == "income").Sum(t => t.Amount);
    var expense = transactions.Where(t => t.Type == "expense").Sum(t => t.Amount);
    var byCategory = await txService.GetExpenseByCategoryAsync(group.Id, monthKey);
    var byMember = await txService.GetExpenseByMemberAsync(group.Id, monthKey);

    return Results.Ok(new AppStateResponse(
        new GroupResponse(group.Id, group.Name, group.Description, group.InviteCode),
        group.Members.Select(m => new MemberResponse(
            m.UserId,
            m.User.DisplayName,
            m.User.Email ?? "",
            m.Role,
            transactions.Where(t => t.UserId == m.UserId && t.Type == "expense").Sum(t => t.Amount))).ToList(),
        transactions.Select(ToTransactionResponse).ToList(),
        budgets.Select(b => new BudgetResponse(
            b.Id,
            b.CategoryId,
            b.Amount,
            b.Month,
            spending.GetValueOrDefault(b.CategoryId, 0))).ToList(),
        debts.Select(d => new DebtResponse(
            d.Id,
            d.DebtorId,
            d.Debtor.DisplayName,
            d.CreditorId,
            d.Creditor.DisplayName,
            d.Amount,
            d.Note,
            d.CreatedAt)).ToList(),
        new FundResponse(fund.Id, fund.Name, fund.Balance),
        fundTransactions,
        splits.Select(s => new SplitResponse(
            s.Id,
            s.Description,
            s.TotalAmount,
            s.SplitType,
            s.Date,
            s.IsSettled,
            s.Items.Count,
            s.Items.Count(i => i.IsPaid))).ToList(),
        new ReportResponse(
            income,
            expense,
            income - expense,
            transactions.Count,
            byCategory,
            byMember)));
});

api.MapPost("/groups", async (
    HttpContext http,
    GroupService groupService,
    GroupCreateRequest request) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name)) return Results.BadRequest(new { message = "Tên nhóm chưa hợp lệ." });

    var group = await groupService.CreateAsync(name, request.Description?.Trim() ?? "", userId);
    return Results.Ok(new GroupResponse(group.Id, group.Name, group.Description, group.InviteCode));
});

api.MapPost("/groups/join", async (
    HttpContext http,
    GroupService groupService,
    GroupJoinRequest request) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var code = request.InviteCode?.Trim();
    if (string.IsNullOrWhiteSpace(code)) return Results.BadRequest(new { message = "Mã mời chưa hợp lệ." });

    var group = await groupService.JoinByCodeAsync(code, userId);
    return Results.Ok(group == null ? null : new GroupResponse(group.Id, group.Name, group.Description, group.InviteCode));
});

api.MapPost("/transactions", async (
    HttpContext http,
    GroupService groupService,
    TransactionService txService,
    SplitBillService splitService,
    AppDbContext db,
    TransactionRequest request) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

    var group = (await groupService.GetUserGroupsAsync(userId)).FirstOrDefault();
    if (group == null) return Results.BadRequest(new { message = "Bạn cần tạo hoặc tham gia nhóm trước." });

    if (request.Amount <= 0) return Results.BadRequest(new { message = "Số tiền chưa hợp lệ." });
    var type = request.Type == "income" ? "income" : "expense";
    var category = string.IsNullOrWhiteSpace(request.Category) ? (type == "income" ? "other" : "other") : request.Category.Trim();
    var date = request.Date == default ? DateTime.UtcNow : DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc);

    var tx = await txService.AddAsync(new Transaction
    {
        GroupId = group.Id,
        UserId = userId,
        Type = type,
        Amount = request.Amount,
        Category = category,
        Note = request.Note?.Trim() ?? "",
        IsShared = request.IsShared && type == "expense",
        Date = date,
        Latitude = request.Latitude,
        Longitude = request.Longitude,
        LocationAccuracy = request.LocationAccuracy,
        LocationName = request.LocationName?.Trim() ?? "",
        CheckedInAt = request.Latitude.HasValue && request.Longitude.HasValue ? DateTime.UtcNow : null,
    });

    if (tx.IsShared)
    {
        if (request.FundAction is "deposit" or "withdraw")
        {
            var fund = await GetOrCreateSharedFundAsync(db, group.Id);
            db.FundTransactions.Add(new FundTransaction
            {
                FundId = fund.Id,
                UserId = userId,
                Type = request.FundAction,
                Amount = tx.Amount,
                Note = string.IsNullOrWhiteSpace(tx.Note) ? "Giao dich chung" : $"Giao dich chung: {tx.Note}",
                Date = DateTime.UtcNow,
            });
            fund.Balance += request.FundAction == "deposit" ? tx.Amount : -tx.Amount;
            await db.SaveChangesAsync();
        }

        var splitParticipants = request.SplitParticipantIds?
            .Where(id => group.Members.Any(m => m.UserId == id))
            .Distinct()
            .ToList();
        if (splitParticipants is { Count: > 0 })
        {
            if (!splitParticipants.Contains(userId)) splitParticipants.Insert(0, userId);
            await splitService.SplitEquallyAsync(
                group.Id,
                userId,
                tx.Amount,
                string.IsNullOrWhiteSpace(tx.Note) ? "Giao dich chung" : tx.Note,
                splitParticipants);
        }
    }

    return Results.Ok(ToTransactionResponse(tx));
});

api.MapDelete("/transactions/{id:int}", async (HttpContext http, TransactionService txService, int id) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    await txService.DeleteAsync(id, userId);
    return Results.NoContent();
});

api.MapPost("/budgets", async (
    HttpContext http,
    GroupService groupService,
    BudgetService budgetService,
    BudgetRequest request) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var group = (await groupService.GetUserGroupsAsync(userId)).FirstOrDefault();
    if (group == null) return Results.BadRequest(new { message = "Bạn cần tạo hoặc tham gia nhóm trước." });
    if (request.Amount <= 0) return Results.BadRequest(new { message = "Số tiền ngân sách chưa hợp lệ." });

    await budgetService.SetAsync(group.Id, request.CategoryId.Trim(), NormalizeMonth(request.Month), request.Amount);
    return Results.NoContent();
});

api.MapDelete("/budgets/{id:int}", async (
    HttpContext http,
    GroupService groupService,
    BudgetService budgetService,
    AppDbContext db,
    int id) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var groupIds = (await groupService.GetUserGroupsAsync(userId)).Select(g => g.Id).ToHashSet();
    var budget = await db.Budgets.FindAsync(id);
    if (budget == null) return Results.NoContent();
    if (!groupIds.Contains(budget.GroupId)) return Results.Forbid();

    await budgetService.DeleteAsync(id);
    return Results.NoContent();
});

api.MapPost("/fund-transactions", async (
    HttpContext http,
    GroupService groupService,
    AppDbContext db,
    FundTransactionRequest request) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var group = (await groupService.GetUserGroupsAsync(userId)).FirstOrDefault();
    if (group == null) return Results.BadRequest(new { message = "Bạn cần tạo hoặc tham gia nhóm trước." });
    if (request.Amount <= 0) return Results.BadRequest(new { message = "Số tiền quỹ chưa hợp lệ." });

    var fund = await GetOrCreateSharedFundAsync(db, group.Id);
    var type = request.Type == "withdraw" ? "withdraw" : "deposit";
    db.FundTransactions.Add(new FundTransaction
    {
        FundId = fund.Id,
        UserId = userId,
        Type = type,
        Amount = request.Amount,
        Note = request.Note?.Trim() ?? "",
        Date = DateTime.UtcNow,
    });
    fund.Balance += type == "deposit" ? request.Amount : -request.Amount;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

api.MapPost("/splits", async (
    HttpContext http,
    GroupService groupService,
    SplitBillService splitService,
    SplitCreateRequest request) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var group = (await groupService.GetUserGroupsAsync(userId)).FirstOrDefault();
    if (group == null) return Results.BadRequest(new { message = "Bạn cần tạo hoặc tham gia nhóm trước." });
    if (request.TotalAmount <= 0) return Results.BadRequest(new { message = "Số tiền chia chưa hợp lệ." });

    var memberIds = group.Members.Select(m => m.UserId).ToHashSet();
    var participants = request.ParticipantIds?
        .Where(id => memberIds.Contains(id))
        .Distinct()
        .ToList() ?? new List<string>();
    if (!participants.Contains(userId)) participants.Insert(0, userId);
    if (participants.Count == 0) return Results.BadRequest(new { message = "Chọn ít nhất một thành viên." });

    var bill = await splitService.SplitEquallyAsync(
        group.Id,
        userId,
        request.TotalAmount,
        request.Description?.Trim() ?? "",
        participants);

    return Results.Ok(new SplitResponse(
        bill.Id,
        bill.Description,
        bill.TotalAmount,
        bill.SplitType,
        bill.Date,
        bill.IsSettled,
        bill.Items.Count,
        bill.Items.Count(i => i.IsPaid)));
});

api.MapPost("/debts/{id:int}/settle", async (HttpContext http, DebtService debtService, int id) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    await debtService.SettleAsync(id, userId);
    return Results.NoContent();
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

static string MaskConnectionString(string connectionString)
{
    try
    {
        var csb = new NpgsqlConnectionStringBuilder(connectionString);

        if (!string.IsNullOrWhiteSpace(csb.Password))
        {
            csb.Password = "******";
        }

        return csb.ConnectionString;
    }
    catch
    {
        return connectionString.Replace("Password=", "Password=******");
    }
}

static async Task RunSmokeTestAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var groupService = scope.ServiceProvider.GetRequiredService<GroupService>();
    var txService = scope.ServiceProvider.GetRequiredService<TransactionService>();
    var budgetService = scope.ServiceProvider.GetRequiredService<BudgetService>();
    var splitService = scope.ServiceProvider.GetRequiredService<SplitBillService>();

    var suffix = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
    var email = $"smoke-{suffix}@example.com";
    var user = new AppUser
    {
        UserName = email,
        Email = email,
        DisplayName = "Smoke Test",
    };

    var created = await users.CreateAsync(user, "123456");
    if (!created.Succeeded)
    {
        throw new InvalidOperationException("Create user failed: " + string.Join(", ", created.Errors.Select(e => e.Description)));
    }

    Group? group = null;
    try
    {
        group = await groupService.CreateAsync("Smoke Test Group", "temporary", user.Id);
        var groups = await groupService.GetUserGroupsAsync(user.Id);
        if (!groups.Any(g => g.Id == group.Id)) throw new InvalidOperationException("Created group is not visible to owner.");

        var month = DateTime.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        await budgetService.SetAsync(group.Id, "food", month, 2_000_000m);
        await txService.AddAsync(new Transaction
        {
            GroupId = group.Id,
            UserId = user.Id,
            Type = "income",
            Amount = 5_000_000m,
            Category = "salary",
            Note = "smoke income",
            Date = DateTime.UtcNow,
        });
        await txService.AddAsync(new Transaction
        {
            GroupId = group.Id,
            UserId = user.Id,
            Type = "expense",
            Amount = 120_000m,
            Category = "food",
            Note = "smoke expense",
            Date = DateTime.UtcNow,
            IsShared = true,
            Latitude = 10.7769,
            Longitude = 106.7009,
            LocationAccuracy = 25,
            LocationName = "Smoke Check-in",
            CheckedInAt = DateTime.UtcNow,
        });

        var txs = await txService.GetByGroupMonthAsync(group.Id, month);
        if (txs.Count < 2) throw new InvalidOperationException("Transactions were not saved or loaded.");
        if (!txs.Any(t => t.LocationName == "Smoke Check-in" && t.Latitude.HasValue && t.Longitude.HasValue))
        {
            throw new InvalidOperationException("Transaction location was not saved or loaded.");
        }

        var summary = await txService.GetSummaryAsync(group.Id, month);
        if (summary.Income < 5_000_000m || summary.Expense < 120_000m)
        {
            throw new InvalidOperationException("Transaction summary is incorrect.");
        }

        await splitService.SplitEquallyAsync(group.Id, user.Id, 300_000m, "smoke split", new List<string> { user.Id });
        var splits = await splitService.GetGroupSplitsAsync(group.Id);
        if (!splits.Any()) throw new InvalidOperationException("Split bill was not saved.");

        var fund = new SharedFund { GroupId = group.Id, Name = "Smoke Fund", Balance = 50_000m };
        db.SharedFunds.Add(fund);
        await db.SaveChangesAsync();
        db.FundTransactions.Add(new FundTransaction
        {
            FundId = fund.Id,
            UserId = user.Id,
            Type = "deposit",
            Amount = 50_000m,
            Note = "smoke fund",
            Date = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        Console.WriteLine("SMOKE_TEST_OK");
    }
    finally
    {
        if (group != null)
        {
            var splitIds = await db.SplitBills.Where(s => s.GroupId == group.Id).Select(s => s.Id).ToListAsync();
            db.SplitBillItems.RemoveRange(db.SplitBillItems.Where(i => splitIds.Contains(i.SplitBillId)));
            db.SplitBills.RemoveRange(db.SplitBills.Where(s => s.GroupId == group.Id));
            db.Debts.RemoveRange(db.Debts.Where(d => d.GroupId == group.Id));
            db.Notifications.RemoveRange(db.Notifications.Where(n => n.GroupId == group.Id));
            db.Budgets.RemoveRange(db.Budgets.Where(b => b.GroupId == group.Id));
            db.Transactions.RemoveRange(db.Transactions.Where(t => t.GroupId == group.Id));

            var fundIds = await db.SharedFunds.Where(f => f.GroupId == group.Id).Select(f => f.Id).ToListAsync();
            db.FundTransactions.RemoveRange(db.FundTransactions.Where(t => fundIds.Contains(t.FundId)));
            db.SharedFunds.RemoveRange(db.SharedFunds.Where(f => f.GroupId == group.Id));
            db.UserEmailConfigs.RemoveRange(db.UserEmailConfigs.Where(c => c.GroupId == group.Id));
            db.GroupMembers.RemoveRange(db.GroupMembers.Where(m => m.GroupId == group.Id));
            db.Groups.RemoveRange(db.Groups.Where(g => g.Id == group.Id));
            await db.SaveChangesAsync();
        }

        var cleanupUser = await users.FindByIdAsync(user.Id);
        if (cleanupUser != null)
        {
            await users.DeleteAsync(cleanupUser);
        }
    }
}

static async Task CleanupSmokeUsersAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
    var smokeUsers = await db.Users
        .Where(u => u.Email != null && (u.Email.StartsWith("codex-smoke-") || u.Email.StartsWith("smoke-")))
        .ToListAsync();

    foreach (var user in smokeUsers)
    {
        var groupIds = await db.GroupMembers.Where(m => m.UserId == user.Id).Select(m => m.GroupId).Distinct().ToListAsync();
        foreach (var groupId in groupIds)
        {
            var splitIds = await db.SplitBills.Where(s => s.GroupId == groupId).Select(s => s.Id).ToListAsync();
            db.SplitBillItems.RemoveRange(db.SplitBillItems.Where(i => splitIds.Contains(i.SplitBillId)));
            db.SplitBills.RemoveRange(db.SplitBills.Where(s => s.GroupId == groupId));
            db.Debts.RemoveRange(db.Debts.Where(d => d.GroupId == groupId));
            db.Notifications.RemoveRange(db.Notifications.Where(n => n.GroupId == groupId));
            db.Budgets.RemoveRange(db.Budgets.Where(b => b.GroupId == groupId));
            db.Transactions.RemoveRange(db.Transactions.Where(t => t.GroupId == groupId));

            var fundIds = await db.SharedFunds.Where(f => f.GroupId == groupId).Select(f => f.Id).ToListAsync();
            db.FundTransactions.RemoveRange(db.FundTransactions.Where(t => fundIds.Contains(t.FundId)));
            db.SharedFunds.RemoveRange(db.SharedFunds.Where(f => f.GroupId == groupId));
            db.UserEmailConfigs.RemoveRange(db.UserEmailConfigs.Where(c => c.GroupId == groupId));
            db.GroupMembers.RemoveRange(db.GroupMembers.Where(m => m.GroupId == groupId));
            db.Groups.RemoveRange(db.Groups.Where(g => g.Id == groupId));
        }

        await db.SaveChangesAsync();
        await users.DeleteAsync(user);
    }

    Console.WriteLine($"CLEANED_SMOKE_USERS {smokeUsers.Count}");
}

static string GetCurrentUserId(HttpContext http)
    => http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";

static string NormalizeMonth(string? month)
    => string.IsNullOrWhiteSpace(month) ? DateTime.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture) : month;

static async Task<SharedFund> GetOrCreateSharedFundAsync(AppDbContext db, int groupId)
{
    var fund = await db.SharedFunds.FirstOrDefaultAsync(f => f.GroupId == groupId);
    if (fund != null) return fund;

    fund = new SharedFund { GroupId = groupId, Name = "Quỹ chung" };
    db.SharedFunds.Add(fund);
    await db.SaveChangesAsync();
    return fund;
}

static TransactionResponse ToTransactionResponse(Transaction tx)
    => new(
        tx.Id,
        tx.Type,
        tx.Amount,
        tx.Category,
        tx.Note,
        tx.IsShared,
        tx.Date,
        tx.Month,
        tx.FromEmail,
        tx.UserId,
        tx.User?.DisplayName ?? "",
        tx.Latitude,
        tx.Longitude,
        tx.LocationAccuracy,
        tx.LocationName,
        tx.CheckedInAt);

record AppStateResponse(
    GroupResponse? Group,
    IReadOnlyList<MemberResponse> Members,
    IReadOnlyList<TransactionResponse> Transactions,
    IReadOnlyList<BudgetResponse> Budgets,
    IReadOnlyList<DebtResponse> Debts,
    FundResponse? Fund,
    IReadOnlyList<FundTransactionResponse> FundTransactions,
    IReadOnlyList<SplitResponse> Splits,
    ReportResponse Report);

record GroupResponse(int Id, string Name, string Description, string InviteCode);
record MemberResponse(string Id, string Name, string Email, string Role, decimal Spent);
record TransactionResponse(
    int Id,
    string Type,
    decimal Amount,
    string Category,
    string Note,
    bool IsShared,
    DateTime Date,
    string Month,
    bool FromEmail,
    string UserId,
    string UserName,
    double? Latitude,
    double? Longitude,
    double? LocationAccuracy,
    string LocationName,
    DateTime? CheckedInAt);
record BudgetResponse(int Id, string CategoryId, decimal Amount, string Month, decimal Spent);
record DebtResponse(int Id, string DebtorId, string DebtorName, string CreditorId, string CreditorName, decimal Amount, string Note, DateTime CreatedAt);
record FundResponse(int Id, string Name, decimal Balance);
record FundTransactionResponse(int Id, string Type, decimal Amount, string Note, DateTime Date, string UserId);
record SplitResponse(int Id, string Description, decimal TotalAmount, string SplitType, DateTime Date, bool IsSettled, int MemberCount, int PaidCount);
record ReportResponse(decimal Income, decimal Expense, decimal Balance, int Count, Dictionary<string, decimal> ByCategory, Dictionary<string, decimal> ByMember);

record TransactionRequest(
    string Type,
    decimal Amount,
    string Category,
    string? Note,
    bool IsShared,
    string? FundAction,
    List<string>? SplitParticipantIds,
    DateTime Date,
    double? Latitude,
    double? Longitude,
    double? LocationAccuracy,
    string? LocationName);
record GroupCreateRequest(string? Name, string? Description);
record GroupJoinRequest(string? InviteCode);
record BudgetRequest(string CategoryId, string Month, decimal Amount);
record FundTransactionRequest(string Type, decimal Amount, string? Note);
record SplitCreateRequest(decimal TotalAmount, string? Description, List<string>? ParticipantIds);

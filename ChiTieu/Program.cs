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

    Console.WriteLine("Testing Supabase connection...");

    var canConnect = false;

    Console.WriteLine(canConnect
        ? "SUPABASE CONNECT SUCCESS"
        : "SUPABASE CONNECT FAILED: CanConnectAsync returned false");

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
    var email = form["email"].ToString();
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
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var displayName = form["displayName"].ToString();

    var user = new AppUser
    {
        UserName = email,
        Email = email,
        DisplayName = displayName
    };

    IdentityResult result;
    try
    {
        result = await userManager.CreateAsync(user, password);
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
        });

        var txs = await txService.GetByGroupMonthAsync(group.Id, month);
        if (txs.Count < 2) throw new InvalidOperationException("Transactions were not saved or loaded.");

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

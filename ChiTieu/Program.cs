// Program.cs
using ChiTieu.Data;
using ChiTieu.Data.Entities;
using ChiTieu.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
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

var configuredExternalProviders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
var authenticationBuilder = builder.Services.AddAuthentication();
var googleClientId = builder.Configuration["Authentication:Google:ClientId"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID");
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET");
if (!string.IsNullOrWhiteSpace(googleClientId) && !string.IsNullOrWhiteSpace(googleClientSecret))
{
    authenticationBuilder.AddGoogle(options =>
    {
        options.ClientId = googleClientId;
        options.ClientSecret = googleClientSecret;
        options.CallbackPath = "/signin-google";
    });
    configuredExternalProviders.Add("Google");
}

var facebookAppId = builder.Configuration["Authentication:Facebook:AppId"] ?? Environment.GetEnvironmentVariable("FACEBOOK_APP_ID");
var facebookAppSecret = builder.Configuration["Authentication:Facebook:AppSecret"] ?? Environment.GetEnvironmentVariable("FACEBOOK_APP_SECRET");
if (!string.IsNullOrWhiteSpace(facebookAppId) && !string.IsNullOrWhiteSpace(facebookAppSecret))
{
    authenticationBuilder.AddFacebook(options =>
    {
        options.AppId = facebookAppId;
        options.AppSecret = facebookAppSecret;
        options.CallbackPath = "/signin-facebook";
        options.Scope.Add("email");
        options.Fields.Add("email");
    });
    configuredExternalProviders.Add("Facebook");
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "ChiTieu.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
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
app.Use(async (context, next) =>
{
    context.Response.Headers["Permissions-Policy"] = "geolocation=(self)";

    if (context.Request.Path.Equals("/react/index.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.Redirect("/react/home", permanent: false);
        return;
    }

    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseCors();
app.Use(async (context, next) =>
{
    await next();

    if (!context.Request.Path.StartsWithSegments("/api") || !IsRedirectStatus(context.Response.StatusCode))
    {
        return;
    }

    var location = context.Response.Headers.Location.ToString();
    var isAccessDenied = location.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase);
    context.Response.Clear();
    context.Response.StatusCode = isAccessDenied
        ? StatusCodes.Status403Forbidden
        : StatusCodes.Status401Unauthorized;
    context.Response.ContentType = "application/json; charset=utf-8";
    await context.Response.WriteAsync(isAccessDenied
        ? """{"message":"Forbidden"}"""
        : """{"message":"Unauthorized"}""");
});
app.UseAuthentication();
app.UseAuthorization();

static string AccountPage(string title, string body)
{
    return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0" />
    <meta name="theme-color" content="#2563EB" />
    <meta name="application-name" content="Chi Tieu" />
    <meta name="mobile-web-app-capable" content="yes" />
    <meta name="apple-mobile-web-app-capable" content="yes" />
    <meta name="apple-mobile-web-app-title" content="Chi Tieu" />
    <meta name="apple-mobile-web-app-status-bar-style" content="default" />
    <title>{{title}} - Chi Tieu</title>
    <link rel="manifest" href="/manifest.webmanifest" />
    <link rel="apple-touch-icon" sizes="180x180" href="/icons/app-icon-180.png" />
    <link rel="stylesheet" href="/css/app.css" />
    <style>
      body{margin:0;background:#f4f7fb;color:#111827;font-family:Inter,system-ui,sans-serif}
      .auth-shell{min-height:100vh;display:grid;place-items:center;padding:20px;background:linear-gradient(135deg,#eff6ff 0%,#f8fafc 48%,#ecfdf5 100%)}
      .auth-card{width:min(440px,100%);border:1px solid #dbe4ef;background:rgba(255,255,255,.94);box-shadow:0 22px 60px rgba(15,23,42,.14);border-radius:18px;padding:26px}
      .auth-brand{display:flex;align-items:center;gap:12px;margin-bottom:22px}
      .auth-logo{width:44px;height:44px;border-radius:12px;display:grid;place-items:center;background:#0f172a;color:#fff;font-weight:900}
      .auth-title{font-size:24px;font-weight:900;letter-spacing:0;margin:0}
      .auth-sub{color:#64748b;margin:4px 0 0}
      .auth-form{display:grid;gap:13px}
      .auth-input{width:100%;min-height:46px;border:1px solid #cbd5e1;border-radius:10px;padding:0 13px;font-size:16px;outline:none}
      .auth-input:focus{border-color:#2563eb;box-shadow:0 0 0 4px rgba(37,99,235,.14)}
      .auth-label{font-size:13px;font-weight:700;color:#475569}
      .auth-btn{min-height:46px;border:0;border-radius:10px;font-size:15px;font-weight:800;cursor:pointer;display:flex;align-items:center;justify-content:center;gap:8px;text-decoration:none}
      .auth-primary{background:#2563eb;color:white;box-shadow:0 10px 22px rgba(37,99,235,.25)}
      .auth-social{background:white;color:#0f172a;border:1px solid #cbd5e1}
      .auth-social-row{display:grid;grid-template-columns:1fr 1fr;gap:10px;margin:14px 0}
      .auth-divider{display:flex;align-items:center;gap:10px;color:#94a3b8;font-size:12px;font-weight:700;text-transform:uppercase}
      .auth-divider:before,.auth-divider:after{content:"";height:1px;background:#e2e8f0;flex:1}
      .auth-error{border:1px solid #fecaca;background:#fef2f2;color:#991b1b;border-radius:10px;padding:11px 12px;margin-bottom:14px;font-size:14px;font-weight:700}
      .auth-foot{margin-top:16px;color:#64748b}
      .auth-foot a{font-weight:800;color:#2563eb;text-decoration:none}
      @media(max-width:460px){.auth-card{padding:20px;border-radius:14px}.auth-social-row{grid-template-columns:1fr} }
    </style>
</head>
<body>
    <main class="auth-shell">
        <section class="auth-card">
            {{body}}
        </section>
    </main>
</body>
</html>
""";
}

static bool IsLocalReturnUrl(string? returnUrl)
    => !string.IsNullOrWhiteSpace(returnUrl)
        && returnUrl.StartsWith('/')
        && !returnUrl.StartsWith("//")
        && !returnUrl.StartsWith("/api", StringComparison.OrdinalIgnoreCase);

static bool IsRedirectStatus(int statusCode)
    => statusCode is StatusCodes.Status301MovedPermanently
        or StatusCodes.Status302Found
        or StatusCodes.Status303SeeOther
        or StatusCodes.Status307TemporaryRedirect
        or StatusCodes.Status308PermanentRedirect;

static string AuthStatusMessage(string? error, string? external)
{
    var message = (external ?? error) switch
    {
        "not_configured" => "Dang nhap Google/Facebook chua duoc cau hinh tren server deploy.",
        "failed" => "Dang nhap ben ngoai that bai. Hay kiem tra redirect URL va domain OAuth.",
        "no_email" => "Tai khoan ben ngoai khong tra ve email. Hay cap quyen email cho ung dung.",
        "create_failed" => "Khong tao duoc tai khoan tu dang nhap ben ngoai.",
        "link_failed" => "Khong lien ket duoc tai khoan ben ngoai.",
        "1" => "Email hoac mat khau chua dung.",
        _ => string.Empty
    };

    return string.IsNullOrWhiteSpace(message)
        ? string.Empty
        : $"""<div class="auth-error">{System.Net.WebUtility.HtmlEncode(message)}</div>""";
}

static void NoStore(HttpContext http)
{
    http.Response.Headers.CacheControl = "no-store, no-cache, max-age=0";
    http.Response.Headers.Pragma = "no-cache";
    http.Response.Headers.Expires = "0";
}

app.MapGet("/account/login", (HttpContext http, string? returnUrl, string? error, string? external) =>
{
    var safeReturnUrl = IsLocalReturnUrl(returnUrl) ? returnUrl! : "/react/home";
    if (http.User.Identity?.IsAuthenticated == true)
    {
        return Results.Redirect(safeReturnUrl);
    }

    NoStore(http);
    var encodedReturnUrl = System.Net.WebUtility.HtmlEncode(safeReturnUrl);
    var statusMessage = AuthStatusMessage(error, external);
    var html = AccountPage("Dang nhap", $$"""
<div class="auth-brand"><div class="auth-logo">đ</div><div><h1 class="auth-title">Chi Tieu Money</h1><p class="auth-sub">Tai chinh va cong viec trong mot workspace.</p></div></div>
{{statusMessage}}
<div class="auth-social-row">
  <form method="post" action="/account/external-login"><input type="hidden" name="provider" value="Google" /><input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}" /><button class="auth-btn auth-social" type="submit">G Google</button></form>
  <form method="post" action="/account/external-login"><input type="hidden" name="provider" value="Facebook" /><input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}" /><button class="auth-btn auth-social" type="submit">f Facebook</button></form>
</div>
<div class="auth-divider">hoac email</div>
<form method="post" action="/account/login" class="auth-form">
    <input type="hidden" name="returnUrl" value="{{encodedReturnUrl}}" />
    <div>
        <label class="auth-label">Email</label>
        <input class="auth-input" type="email" name="email" autocomplete="email" required />
    </div>
    <div>
        <label class="auth-label">Mat khau</label>
        <input class="auth-input" type="password" name="password" autocomplete="current-password" required />
    </div>
    <button class="auth-btn auth-primary" type="submit">Dang nhap</button>
</form>
<p class="auth-foot">Chua co tai khoan? <a href="/account/register">Dang ky</a></p>
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
            return Results.Redirect(IsLocalReturnUrl(returnUrl) ? returnUrl : "/react/home");
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

app.MapGet("/account/register", (string? error) =>
{
    var statusMessage = AuthStatusMessage(error, null);
    var html = AccountPage("Dang ky", $$"""
<div class="auth-brand"><div class="auth-logo">đ</div><div><h1 class="auth-title">Tao tai khoan</h1><p class="auth-sub">Bat dau quan ly nhom, quy va task.</p></div></div>
{{statusMessage}}
<div class="auth-social-row">
  <form method="post" action="/account/external-login"><input type="hidden" name="provider" value="Google" /><input type="hidden" name="returnUrl" value="/react/home" /><button class="auth-btn auth-social" type="submit">G Google</button></form>
  <form method="post" action="/account/external-login"><input type="hidden" name="provider" value="Facebook" /><input type="hidden" name="returnUrl" value="/react/home" /><button class="auth-btn auth-social" type="submit">f Facebook</button></form>
</div>
<div class="auth-divider">hoac email</div>
<form method="post" action="/account/register" class="auth-form">
    <div>
        <label class="auth-label">Ten hien thi</label>
        <input class="auth-input" name="displayName" autocomplete="name" required />
    </div>
    <div>
        <label class="auth-label">Email</label>
        <input class="auth-input" type="email" name="email" autocomplete="email" required />
    </div>
    <div>
        <label class="auth-label">Mat khau</label>
        <input class="auth-input" type="password" name="password" autocomplete="new-password" required />
    </div>
    <button class="auth-btn auth-primary" type="submit">Dang ky</button>
</form>
<p class="auth-foot">Da co tai khoan? <a href="/account/login">Dang nhap</a></p>
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
            return Results.Redirect("/react/home");
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
        return Results.Redirect("/react/home");
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

app.MapPost("/account/external-login", async (
    HttpContext http,
    SignInManager<AppUser> signInManager) =>
{
    var form = await http.Request.ReadFormAsync();
    var provider = form["provider"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    if (!configuredExternalProviders.Contains(provider))
    {
        return Results.Redirect("/account/login?external=not_configured");
    }

    var redirectUrl = $"/account/external-callback?returnUrl={Uri.EscapeDataString(IsLocalReturnUrl(returnUrl) ? returnUrl : "/react/home")}";
    var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
    return Results.Challenge(properties, new[] { provider });
});

app.MapGet("/account/external-callback", async (
    string? returnUrl,
    SignInManager<AppUser> signInManager,
    UserManager<AppUser> userManager,
    ILogger<Program> logger) =>
{
    var info = await signInManager.GetExternalLoginInfoAsync();
    if (info == null) return Results.Redirect("/account/login?external=failed");

    var result = await signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true, bypassTwoFactor: true);
    if (result.Succeeded) return Results.Redirect(IsLocalReturnUrl(returnUrl) ? returnUrl! : "/react/home");

    var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
    if (string.IsNullOrWhiteSpace(email))
    {
        logger.LogWarning("External login failed because provider {Provider} did not return email", info.LoginProvider);
        return Results.Redirect("/account/login?external=no_email");
    }

    var user = await userManager.FindByEmailAsync(email);
    if (user == null)
    {
        user = new AppUser
        {
            UserName = email,
            Email = email,
            DisplayName = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email
        };
        var create = await userManager.CreateAsync(user);
        if (!create.Succeeded)
        {
            logger.LogWarning("External user create failed for {Email}: {Errors}", email, string.Join(", ", create.Errors.Select(e => e.Description)));
            return Results.Redirect("/account/login?external=create_failed");
        }
    }

    var addLogin = await userManager.AddLoginAsync(user, info);
    if (!addLogin.Succeeded && !addLogin.Errors.Any(e => e.Code == "LoginAlreadyAssociated"))
    {
        logger.LogWarning("Add external login failed for {Email}: {Errors}", email, string.Join(", ", addLogin.Errors.Select(e => e.Description)));
        return Results.Redirect("/account/login?external=link_failed");
    }

    await signInManager.SignInAsync(user, isPersistent: true);
    return Results.Redirect(IsLocalReturnUrl(returnUrl) ? returnUrl! : "/react/home");
});

app.MapGet("/", () => Results.Redirect("/react/home"));
app.MapGet("/home", () => Results.Redirect("/react/home"));
app.MapGet("/dashboard", () => Results.Redirect("/react/home"));
app.MapGet("/healthz", () => Results.Ok("OK"));
app.MapGet("/react", () => Results.Redirect("/react/home"));
app.MapGet("/react/home", (HttpContext http, IWebHostEnvironment env) =>
{
    NoStore(http);
    return Results.File(Path.Combine(env.WebRootPath, "react", "index.html"), "text/html");
}).RequireAuthorization();
app.MapGet("/react/assets/{*path}", () => Results.NotFound());

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/app", async (
    HttpContext http,
    GroupService groupService,
    TransactionService txService,
    BudgetService budgetService,
    DebtService debtService,
    SplitBillService splitService,
    NotificationService notificationService,
    AppDbContext db,
    string? month,
    int? groupId) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();

    var monthKey = NormalizeMonth(month);
    var groups = await groupService.GetUserGroupsAsync(userId);
    var group = groupId.HasValue ? groups.FirstOrDefault(g => g.Id == groupId.Value) : groups.FirstOrDefault();
    if (group == null)
    {
        return Results.Ok(new AppStateResponse(
            null,
            groups.Select(g => new GroupResponse(g.Id, g.Name, g.Description, g.InviteCode)).ToList(),
            Array.Empty<MemberResponse>(),
            Array.Empty<TransactionResponse>(),
            Array.Empty<BudgetResponse>(),
            Array.Empty<DebtResponse>(),
            null,
            Array.Empty<FundTransactionResponse>(),
            Array.Empty<SplitResponse>(),
            new ReportResponse(0, 0, 0, 0, new(), new()),
            new ReportResponse(0, 0, 0, 0, new(), new()),
            0,
            Array.Empty<TransactionResponse>(),
            Array.Empty<NotificationResponse>(),
            Array.Empty<LocationShareResponse>(),
            userId));
    }

    var groupIds = groups.Select(g => g.Id).ToList();
    var allTransactions = await db.Transactions
        .Include(t => t.User)
        .Where(t => groupIds.Contains(t.GroupId) && t.Month == monthKey && (t.UserId == userId || t.IsShared))
        .OrderByDescending(t => t.Date)
        .ToListAsync();
    var allIncome = allTransactions.Where(t => t.Type == "income").Sum(t => t.Amount);
    var allExpense = allTransactions.Where(t => t.Type == "expense").Sum(t => t.Amount);
    var allByCategory = allTransactions
        .Where(t => t.Type == "expense")
        .GroupBy(t => t.Category)
        .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    var allByMember = allTransactions
        .Where(t => t.Type == "expense")
        .GroupBy(t => string.IsNullOrWhiteSpace(t.User.DisplayName) ? t.User.Email ?? "An danh" : t.User.DisplayName)
        .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    var budgetTotal = await db.Budgets
        .Where(b => groupIds.Contains(b.GroupId) && b.Month == monthKey)
        .SumAsync(b => b.Amount);
    var notifications = await notificationService.GetUserNotifAsync(userId, 10);
    var locationShares = await db.UserLocationShares
        .Include(l => l.User)
        .Where(l => l.GroupId == group.Id && l.IsSharing)
        .OrderByDescending(l => l.UpdatedAt)
        .Select(l => new LocationShareResponse(
            l.UserId,
            string.IsNullOrWhiteSpace(l.User.DisplayName) ? l.User.Email ?? "Thanh vien" : l.User.DisplayName,
            l.User.Email ?? "",
            l.Latitude,
            l.Longitude,
            l.Accuracy,
            l.Label,
            l.UpdatedAt,
            l.UserId == userId))
        .ToListAsync();

    var transactions = await txService.GetVisibleByGroupMonthAsync(group.Id, monthKey, userId);
    var budgets = await budgetService.GetByGroupMonthAsync(group.Id, monthKey);
    var spending = await txService.GetVisibleExpenseByCategoryAsync(group.Id, monthKey, userId);
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
    var byCategory = await txService.GetVisibleExpenseByCategoryAsync(group.Id, monthKey, userId);
    var byMember = await txService.GetVisibleExpenseByMemberAsync(group.Id, monthKey, userId);

    return Results.Ok(new AppStateResponse(
        new GroupResponse(group.Id, group.Name, group.Description, group.InviteCode),
        groups.Select(g => new GroupResponse(g.Id, g.Name, g.Description, g.InviteCode)).ToList(),
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
            byMember),
        new ReportResponse(
            allIncome,
            allExpense,
            allIncome - allExpense,
            allTransactions.Count,
            allByCategory,
            allByMember),
        budgetTotal,
        allTransactions.Take(8).Select(ToTransactionResponse).ToList(),
        notifications.Select(n => new NotificationResponse(
            n.Id,
            n.GroupId,
            n.Type,
            n.Title,
            n.Message,
            n.IsRead,
            n.CreatedAt)).ToList(),
        locationShares,
        userId));
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

api.MapDelete("/groups/{groupId:int}/members/{memberId}", async (
    HttpContext http,
    GroupService groupService,
    int groupId,
    string memberId) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    await groupService.RemoveMemberAsync(groupId, userId, memberId);
    return Results.NoContent();
});

api.MapGet("/notifications", async (HttpContext http, NotificationService notificationService) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var notifications = await notificationService.GetUserNotifAsync(userId, 20);
    return Results.Ok(notifications.Select(n => new NotificationResponse(
        n.Id,
        n.GroupId,
        n.Type,
        n.Title,
        n.Message,
        n.IsRead,
        n.CreatedAt)));
});

api.MapPost("/notifications/read", async (HttpContext http, NotificationService notificationService) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    await notificationService.MarkAllReadAsync(userId);
    return Results.NoContent();
});

api.MapPost("/location-shares", async (
    HttpContext http,
    GroupService groupService,
    AppDbContext db,
    LocationShareRequest request) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var groups = await groupService.GetUserGroupsAsync(userId);
    var group = request.GroupId.HasValue ? groups.FirstOrDefault(g => g.Id == request.GroupId.Value) : groups.FirstOrDefault();
    if (group == null) return Results.BadRequest(new { message = "Ban can chon nhom truoc khi chia se vi tri." });
    if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
    {
        return Results.BadRequest(new { message = "Toa do khong hop le." });
    }

    var share = await db.UserLocationShares.FirstOrDefaultAsync(l => l.GroupId == group.Id && l.UserId == userId);
    if (share == null)
    {
        share = new UserLocationShare { GroupId = group.Id, UserId = userId };
        db.UserLocationShares.Add(share);
    }

    share.IsSharing = true;
    share.Latitude = request.Latitude;
    share.Longitude = request.Longitude;
    share.Accuracy = request.Accuracy;
    share.Label = request.Label?.Trim() ?? "";
    share.UpdatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();
    return Results.NoContent();
});

api.MapDelete("/location-shares/{groupId:int}", async (
    HttpContext http,
    GroupService groupService,
    AppDbContext db,
    int groupId) =>
{
    var userId = GetCurrentUserId(http);
    if (string.IsNullOrWhiteSpace(userId)) return Results.Unauthorized();
    var groupIds = (await groupService.GetUserGroupsAsync(userId)).Select(g => g.Id).ToHashSet();
    if (!groupIds.Contains(groupId)) return Results.Forbid();

    var share = await db.UserLocationShares.FirstOrDefaultAsync(l => l.GroupId == groupId && l.UserId == userId);
    if (share != null)
    {
        share.IsSharing = false;
        share.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
    return Results.NoContent();
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

    var userGroups = await groupService.GetUserGroupsAsync(userId);
    var group = request.GroupId.HasValue ? userGroups.FirstOrDefault(g => g.Id == request.GroupId.Value) : userGroups.FirstOrDefault();
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
    var userGroups = await groupService.GetUserGroupsAsync(userId);
    var group = request.GroupId.HasValue ? userGroups.FirstOrDefault(g => g.Id == request.GroupId.Value) : userGroups.FirstOrDefault();
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
    var userGroups = await groupService.GetUserGroupsAsync(userId);
    var group = request.GroupId.HasValue ? userGroups.FirstOrDefault(g => g.Id == request.GroupId.Value) : userGroups.FirstOrDefault();
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
    var userGroups = await groupService.GetUserGroupsAsync(userId);
    var group = request.GroupId.HasValue ? userGroups.FirstOrDefault(g => g.Id == request.GroupId.Value) : userGroups.FirstOrDefault();
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
            db.UserLocationShares.RemoveRange(db.UserLocationShares.Where(l => l.GroupId == group.Id));
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
            db.UserLocationShares.RemoveRange(db.UserLocationShares.Where(l => l.GroupId == groupId));
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
    IReadOnlyList<GroupResponse> Groups,
    IReadOnlyList<MemberResponse> Members,
    IReadOnlyList<TransactionResponse> Transactions,
    IReadOnlyList<BudgetResponse> Budgets,
    IReadOnlyList<DebtResponse> Debts,
    FundResponse? Fund,
    IReadOnlyList<FundTransactionResponse> FundTransactions,
    IReadOnlyList<SplitResponse> Splits,
    ReportResponse Report,
    ReportResponse OverviewReport,
    decimal BudgetTotal,
    IReadOnlyList<TransactionResponse> OverviewTransactions,
    IReadOnlyList<NotificationResponse> Notifications,
    IReadOnlyList<LocationShareResponse> LocationShares,
    string CurrentUserId);

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
record NotificationResponse(int Id, int GroupId, string Type, string Title, string Message, bool IsRead, DateTime CreatedAt);
record LocationShareResponse(string UserId, string UserName, string Email, double Latitude, double Longitude, double? Accuracy, string Label, DateTime UpdatedAt, bool IsMe);

record TransactionRequest(
    int? GroupId,
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
record BudgetRequest(int? GroupId, string CategoryId, string Month, decimal Amount);
record FundTransactionRequest(int? GroupId, string Type, decimal Amount, string? Note);
record SplitCreateRequest(int? GroupId, decimal TotalAmount, string? Description, List<string>? ParticipantIds);
record LocationShareRequest(int? GroupId, double Latitude, double Longitude, double? Accuracy, string? Label);

// Services/VcbEmailBackgroundService.cs
using ChiTieu.Data;
using Microsoft.EntityFrameworkCore;

namespace ChiTieu.Services;

/// <summary>
/// Chạy nền mỗi 15 phút — tự động lấy email VCB mới và thêm vào app
/// </summary>
public class VcbEmailBackgroundService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<VcbEmailBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(15);

    public VcbEmailBackgroundService(IServiceProvider services, ILogger<VcbEmailBackgroundService> logger)
    {
        _services = services;
        _logger   = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VCB Email Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessAllUsersAsync();
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessAllUsersAsync()
    {
        using var scope = _services.CreateScope();
        var db          = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var parser      = scope.ServiceProvider.GetRequiredService<VcbEmailParserService>();
        var txService   = scope.ServiceProvider.GetRequiredService<TransactionService>();

        // Lấy tất cả user có cấu hình Gmail
        var configs = await db.Set<UserEmailConfig>().ToListAsync();

        foreach (var config in configs)
        {
            try
            {
                var since = config.LastFetchedAt ?? DateTime.UtcNow.AddDays(-7);
                var vcbTxs = await parser.FetchNewVcbEmailsAsync(
                    config.GmailAddress,
                    config.GmailAppPassword,
                    since);

                foreach (var vcb in vcbTxs)
                {
                    await txService.AddFromVcbEmailAsync(config.GroupId, config.UserId, vcb);
                }

                config.LastFetchedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();

                _logger.LogInformation(
                    "Fetched {Count} VCB transactions for user {UserId}",
                    vcbTxs.Count, config.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching emails for user {UserId}", config.UserId);
            }
        }
    }
}

// Config lưu thông tin Gmail của từng user
public class UserEmailConfig
{
    public int      Id               { get; set; }
    public string   UserId           { get; set; } = "";
    public int      GroupId          { get; set; }
    public string   GmailAddress     { get; set; } = "";
    public string   GmailAppPassword { get; set; } = ""; // Mã hóa trong production
    public bool     IsActive         { get; set; } = true;
    public DateTime? LastFetchedAt   { get; set; }
}

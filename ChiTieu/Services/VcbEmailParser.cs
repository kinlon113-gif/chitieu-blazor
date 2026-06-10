// Services/VcbEmailParser.cs
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using ChiTieu.Data.Entities;

namespace ChiTieu.Services;

// ─── Kết quả parse 1 email VCB ────────────────────────────────
public class VcbTransaction
{
    public decimal  Amount      { get; set; }
    public string   SenderName  { get; set; } = "";
    public string   ReceiverName{ get; set; } = "";
    public string   Note        { get; set; } = "";
    public string?  OrderId     { get; set; }
    public DateTime Date        { get; set; }
    public string   CategoryId  { get; set; } = "other";
    public string   CategoryLabel { get; set; } = "Khác";
    public double   Confidence  { get; set; }
}

public class VcbEmailParserService
{
    private readonly ILogger<VcbEmailParserService> _logger;

    // ─── Từ điển phân loại thông minh ─────────────────────────
    // Key = categoryId, Value = list từ khóa không dấu
    private static readonly Dictionary<string, string[]> Keywords = new()
    {
        ["food"] = [
            "com","com trua","com toi","an com","an trua","an toi","an sang",
            "bun","pho","chao","banh mi","banh","an vat","cafe","ca phe",
            "nuoc","tra sua","tran chau","lau","bbq","nha hang","quan an",
            "fastfood","kfc","mcdonalds","pizza","sushi","do an","bua an",
            "breakfast","lunch","dinner","food","eat","com nha","toi com",
            "com rang","chao","xoi","banh cuon","hu tieu","mi","bun bo"
        ],
        ["transport"] = [
            "xang","xang xe","do xang","xang oto","xang xe may",
            "grab","be","gojek","taxi","xe om","di xe","ve xe",
            "tau","ve tau","xe khach","vexere","di chuyen","xe buyt"
        ],
        ["rent"] = [
            "tien nha","thue nha","nha tro","phong tro","tien phong",
            "coc nha","tien mat bang","mat bang","tien thue","nha"
        ],
        ["utilities"] = [
            "dien","tien dien","nuoc","tien nuoc","gas","tien gas",
            "rac","tien rac","phi quan ly","phi dich vu","evn","pvgas"
        ],
        ["internet"] = [
            "internet","wifi","viettel","vnpt","fpt","vinaphone","mobifone",
            "tien cuoc","cuoc phi","cuoc thang","data","4g","5g"
        ],
        ["supermarket"] = [
            "sieu thi","vinmart","coopmart","bigc","lotte","aeon",
            "bach hoa","bachhoaxanh","winmart","mm mega","cho","tap hoa"
        ],
        ["shopping"] = [
            "mua sam","quan ao","giay dep","tui xach","phu kien","my pham",
            "lazada","shopee","tiki","sendo","amazon","tgdd","dienmaycheap",
            "dien may","laptop","dien thoai","iphone","samsung","mua"
        ],
        ["entertainment"] = [
            "giai tri","phim","rap phim","cgv","galaxy","lotte cinema",
            "game","karaoke","bar","bia","ruou","club","choi game",
            "netflix","spotify","youtube","premium","ve xem"
        ],
        ["health"] = [
            "thuoc","benh vien","kham benh","vien phi","bac si","nha khoa",
            "rang","mat","kham","xet nghiem","sieu am","thuoc tay","pharmacy"
        ],
        ["education"] = [
            "hoc","hoc phi","truong","khoa hoc","sach","tai lieu",
            "tieng anh","ngoai ngu","hoc them","luyen thi","hoc phi"
        ],
        ["travel"] = [
            "du lich","nghi duong","khach san","resort","hotel","motel",
            "ve may bay","tour","tham quan","bien","nui","check in"
        ],
    };

    private static readonly Dictionary<string, string> CategoryLabels = new()
    {
        ["food"]          = "Ăn uống",
        ["transport"]     = "Xăng xe / Di chuyển",
        ["rent"]          = "Tiền nhà",
        ["utilities"]     = "Điện nước",
        ["internet"]      = "Internet / Điện thoại",
        ["supermarket"]   = "Siêu thị / Chợ",
        ["shopping"]      = "Mua sắm",
        ["entertainment"] = "Giải trí",
        ["health"]        = "Y tế",
        ["education"]     = "Học tập",
        ["travel"]        = "Du lịch",
        ["other"]         = "Khác",
    };

    public VcbEmailParserService(ILogger<VcbEmailParserService> logger)
    {
        _logger = logger;
    }

    // ─── Parse từ nội dung email (string) ─────────────────────
    public VcbTransaction? ParseEmailBody(string body)
    {
        try
        {
            var result = new VcbTransaction();

            // Số tiền
            var amountMatch = Regex.Match(body, @"Amount[\s\S]{0,30}?([\d,]+)\s*VND", RegexOptions.IgnoreCase)
                           ?? Regex.Match(body, @"Số tiền[\s\S]{0,10}?([\d,]+)", RegexOptions.IgnoreCase);
            if (amountMatch.Success)
                result.Amount = decimal.Parse(amountMatch.Groups[1].Value.Replace(",", ""));

            // Người chuyển
            var senderMatch = Regex.Match(body, @"Remitter[''`]s name\s*\r?\n([A-Z\s]+)", RegexOptions.IgnoreCase);
            if (senderMatch.Success) result.SenderName = senderMatch.Groups[1].Value.Trim();

            // Người nhận
            var receiverMatch = Regex.Match(body, @"Beneficiary Name\s*\r?\n([A-Z\s]+)", RegexOptions.IgnoreCase);
            if (receiverMatch.Success) result.ReceiverName = receiverMatch.Groups[1].Value.Trim();

            // Nội dung chuyển tiền
            var noteMatch = Regex.Match(body, @"Details of Payment\s*\r?\n(.+)", RegexOptions.IgnoreCase);
            if (noteMatch.Success)
            {
                result.Note = noteMatch.Groups[1].Value.Trim();
                var (catId, label, confidence) = ClassifyNote(result.Note);
                result.CategoryId    = catId;
                result.CategoryLabel = label;
                result.Confidence    = confidence;
            }

            // Mã lệnh
            var orderMatch = Regex.Match(body, @"Order Number\s*\r?\n(\d+)", RegexOptions.IgnoreCase);
            if (orderMatch.Success) result.OrderId = orderMatch.Groups[1].Value.Trim();

            // Ngày giờ
            var dateMatch = Regex.Match(body, @"(\d{2}:\d{2})\s+Thứ\s+\w+\s+(\d{2}/\d{2}/\d{4})");
            if (dateMatch.Success &&
                DateTime.TryParseExact($"{dateMatch.Groups[2].Value} {dateMatch.Groups[1].Value}",
                    "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out var dt))
                result.Date = dt;
            else
                result.Date = DateTime.UtcNow;

            return result.Amount > 0 ? result : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi parse email VCB");
            return null;
        }
    }

    // ─── Phân loại nội dung chuyển tiền ───────────────────────
    public (string CategoryId, string Label, double Confidence) ClassifyNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note)) return ("other", "Khác", 0);

        // Chuẩn hóa: bỏ dấu, lower case
        var normalized = RemoveDiacritics(note.ToLower())
            .Replace("đ", "d")
            .Replace("-", " ");

        string bestCat   = "other";
        double bestScore = 0;
        string matchedKw = "";

        foreach (var (catId, keywords) in Keywords)
        {
            foreach (var kw in keywords)
            {
                if (!normalized.Contains(kw)) continue;

                // Score: từ dài hơn = chính xác hơn
                double score = (double)kw.Length / Math.Max(normalized.Length, 1) + kw.Length * 0.05;
                if (score > bestScore)
                {
                    bestScore  = score;
                    bestCat    = catId;
                    matchedKw  = kw;
                }
            }
        }

        var label = CategoryLabels.GetValueOrDefault(bestCat, "Khác");
        return (bestCat, label, bestScore);
    }

    // ─── Kết nối Gmail qua IMAP và lấy email VCB mới ──────────
    public async Task<List<VcbTransaction>> FetchNewVcbEmailsAsync(
        string gmailAddress,
        string gmailAppPassword,  // Google App Password (không phải mật khẩu thường)
        DateTime since)
    {
        var results = new List<VcbTransaction>();

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync("imap.gmail.com", 993, true);
            await client.AuthenticateAsync(gmailAddress, gmailAppPassword);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            // Tìm email từ VCB kể từ ngày `since`
            var query = SearchQuery.FromContains("vietcombank")
                .And(SearchQuery.DeliveredAfter(since));

            var uids = await inbox.SearchAsync(query);
            foreach (var uid in uids.TakeLast(50)) // max 50 email mỗi lần
            {
                var message = await inbox.GetMessageAsync(uid);
                var body    = message.TextBody ?? message.HtmlBody ?? "";

                // Chỉ xử lý email biên lai chuyển tiền
                if (!body.Contains("Biên lai chuyển tiền") && !body.Contains("Payment Receipt"))
                    continue;

                var tx = ParseEmailBody(body);
                if (tx != null) results.Add(tx);
            }

            await client.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi kết nối IMAP Gmail");
        }

        return results;
    }

    // ─── Bỏ dấu tiếng Việt ────────────────────────────────────
    private static string RemoveDiacritics(string text)
    {
        var normalized = text.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (var c in normalized)
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);
            if (cat != System.Globalization.UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }
}

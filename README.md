# 💰 Chi Tiêu App — Blazor Server + C# + PostgreSQL

App quản lý chi tiêu nhóm (cặp đôi / gia đình), tự động đọc email VCB.

---

## 🚀 Chạy app trong 5 bước

### Bước 1 — Tạo Supabase database (miễn phí)

1. Vào https://supabase.com → Đăng ký
2. Tạo **New Project** (chọn region Singapore)
3. Vào **Settings → Database** → copy **Connection String**
4. Dán vào `appsettings.json`:

```json
"DefaultConnection": "Host=db.nshvbwjgpjpnprodukzy.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=YOUR_PASSWORD;SSL Mode=Require;Trust Server Certificate=true"
```

### Bước 2 — Cài extensions VS Code

```
C# Dev Kit          (ms-dotnettools.csdevkit)
C# (OmniSharp)      (ms-dotnettools.csharp)
.NET Install Tool   (ms-dotnettools.vscode-dotnet-runtime)
```

### Bước 3 — Restore packages & migrate database

Mở terminal trong VS Code (`Ctrl + \``):

```bash
cd ChiTieu
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
```

> Nếu chưa có EF tools:
> ```bash
> dotnet tool install --global dotnet-ef
> ```

### Bước 4 — Chạy app

```bash
dotnet run
```

Mở trình duyệt: **https://localhost:5001**

### Bước 5 — Deploy lên Railway (public URL, free)

1. Vào https://railway.app → Đăng ký bằng GitHub
2. New Project → Deploy from GitHub repo
3. Add environment variable: `ConnectionStrings__DefaultConnection` = connection string Supabase
4. Railway tự build và deploy → bạn có URL public
5. Chia URL cho bạn gái để cùng dùng!

---

## 📧 Kết nối email VCB tự động

### Cách bật Gmail App Password

1. Vào https://myaccount.google.com/security
2. Bật **2-Step Verification**
3. Vào **App passwords** → Tạo password cho "Mail"
4. Copy mã 16 ký tự

### Cấu hình trong app

Sau khi đăng nhập, vào **Cài đặt → Kết nối email** và điền:
- Gmail: email nhận biên lai VCB
- App Password: mã 16 ký tự vừa tạo

App sẽ tự fetch email mỗi **15 phút**, parse số tiền, nội dung và phân loại vào đúng danh mục.

---

## 📁 Cấu trúc project

```
ChiTieu/
├── Data/
│   ├── Entities/Models.cs      # Tất cả entity models
│   └── AppDbContext.cs         # Database context + relationships
├── Services/
│   ├── VcbEmailParser.cs       # Parse email VCB + phân loại danh mục
│   ├── VcbEmailBackgroundService.cs  # Auto-fetch email mỗi 15 phút
│   └── Services.cs             # GroupService, TransactionService, DebtService...
├── Components/
│   ├── Pages/
│   │   ├── Dashboard.razor     # Tổng quan
│   │   ├── Transactions.razor  # Giao dịch + thêm mới
│   │   └── Groups.razor        # Quản lý nhóm
│   ├── Layout/
│   │   ├── MainLayout.razor    # Layout chính
│   │   └── Sidebar.razor       # Menu sidebar
│   └── Shared/NavItem.razor    # Component nav link
├── wwwroot/css/app.css         # Toàn bộ CSS
├── Program.cs                  # Startup + DI
└── appsettings.json            # Config (điền Supabase connection string)
```

---

## 🗺️ Lộ trình

| Giai đoạn | Tính năng | Trạng thái |
|-----------|-----------|------------|
| 1 | Tài khoản, Nhóm, Thu chi, Parse VCB | ✅ Hoàn thành |
| 2 | Chia tiền, Công nợ, Ngân sách | 🔜 Tiếp theo |
| 3 | Báo cáo biểu đồ, Quỹ chung, OCR hóa đơn | 🔜 |

---

## ❓ Gặp lỗi?

**Lỗi kết nối database:**
- Kiểm tra connection string trong `appsettings.json`
- Đảm bảo Supabase project đang chạy (free tier pause sau 1 tuần không dùng)

**Lỗi migrations:**
```bash
dotnet ef migrations remove  # xóa migration lỗi
dotnet ef migrations add InitialCreate
dotnet ef database update
```

**Lỗi NuGet packages:**
```bash
dotnet restore --force
```

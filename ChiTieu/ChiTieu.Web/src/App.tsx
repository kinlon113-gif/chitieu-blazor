import { useMemo, useState } from "react";
import {
  ArrowDownRight,
  ArrowUpRight,
  BadgeDollarSign,
  Bell,
  CalendarDays,
  CreditCard,
  Home,
  LocateFixed,
  MapPin,
  Menu,
  PieChart,
  Plus,
  ReceiptText,
  Search,
  Settings,
  Split,
  Users,
  WalletCards,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { money } from "@/lib/utils";

type ViewId = "overview" | "transactions" | "split" | "budget" | "groups" | "settings";

const navItems: Array<{ id: ViewId; label: string; icon: typeof Home }> = [
  { id: "overview", label: "Tổng quan", icon: Home },
  { id: "transactions", label: "Giao dịch", icon: ReceiptText },
  { id: "split", label: "Chia tiền", icon: Split },
  { id: "budget", label: "Ngân sách", icon: PieChart },
  { id: "groups", label: "Nhóm", icon: Users },
  { id: "settings", label: "Cài đặt", icon: Settings },
];

const metrics = [
  { label: "Thu tháng này", value: 18500000, icon: ArrowUpRight, tone: "text-emerald-600", bg: "bg-emerald-50" },
  { label: "Chi tháng này", value: 7920000, icon: ArrowDownRight, tone: "text-red-600", bg: "bg-red-50" },
  { label: "Số dư dự kiến", value: 10580000, icon: WalletCards, tone: "text-blue-600", bg: "bg-blue-50" },
];

const transactions = [
  {
    title: "Cơm trưa quán quen",
    category: "Ăn uống",
    amount: -82000,
    date: "Hôm nay",
    location: "Quán cơm Nguyễn Trãi",
    shared: true,
  },
  {
    title: "Lương tháng",
    category: "Thu nhập",
    amount: 18000000,
    date: "10/06",
    location: "Công ty",
    shared: false,
  },
  {
    title: "Grab về nhà",
    category: "Di chuyển",
    amount: -54000,
    date: "09/06",
    location: "Quận 1",
    shared: false,
  },
  {
    title: "Siêu thị cuối tuần",
    category: "Mua sắm",
    amount: -640000,
    date: "08/06",
    location: "GO! An Lạc",
    shared: true,
  },
];

const budgets = [
  { label: "Ăn uống", spent: 2400000, limit: 4000000, color: "bg-blue-500" },
  { label: "Di chuyển", spent: 950000, limit: 1500000, color: "bg-teal-500" },
  { label: "Mua sắm", spent: 2100000, limit: 2500000, color: "bg-amber-500" },
];

const splitItems = [
  { name: "Bữa tối cuối tuần", amount: 680000, members: 4, status: "Đang chia" },
  { name: "Tiền điện tháng 6", amount: 420000, members: 2, status: "Chờ xác nhận" },
  { name: "Đặt phòng Đà Lạt", amount: 1800000, members: 3, status: "Đã xong" },
];

const members = [
  { name: "Kint", role: "Chủ nhóm", spent: 3920000 },
  { name: "Ngọc", role: "Thành viên", spent: 2810000 },
  { name: "Minh", role: "Thành viên", spent: 1190000 },
];

export default function App() {
  const [activeView, setActiveView] = useState<ViewId>("overview");
  const activeLabel = useMemo(() => navItems.find((item) => item.id === activeView)?.label ?? "Tổng quan", [activeView]);

  return (
    <div className="app-shell min-h-screen">
      <aside className="fixed left-0 top-0 z-30 hidden h-screen w-72 border-r border-white/10 bg-slate-950 text-white lg:block">
        <div className="flex h-full flex-col p-5">
          <div className="flex items-center gap-3 px-2 py-2">
            <div className="grid h-11 w-11 place-items-center rounded-lg bg-gradient-to-br from-blue-500 to-teal-400 font-black shadow-lg">
              đ
            </div>
            <div>
              <div className="text-lg font-black">Chi Tiêu</div>
              <div className="text-xs text-slate-400">Tài chính nhóm</div>
            </div>
          </div>

          <nav className="mt-8 space-y-1">
            {navItems.map((item) => (
              <button
                key={item.id}
                onClick={() => setActiveView(item.id)}
                className={[
                  "flex w-full items-center gap-3 rounded-md px-3 py-2.5 text-sm font-semibold transition",
                  activeView === item.id
                    ? "bg-blue-500/20 text-white shadow-[inset_3px_0_0_#2dd4bf]"
                    : "text-slate-400 hover:bg-white/8 hover:text-white",
                ].join(" ")}
              >
                <item.icon className="h-4 w-4" />
                {item.label}
              </button>
            ))}
          </nav>

          <div className="mt-auto rounded-lg border border-white/10 bg-white/5 p-4">
            <div className="text-sm font-semibold">Nhóm hiện tại</div>
            <div className="mt-1 text-xs text-slate-400">Gia đình nhỏ</div>
            <Button className="mt-4 w-full" size="sm">
              <Plus className="h-4 w-4" />
              Mời thành viên
            </Button>
          </div>
        </div>
      </aside>

      <main className="min-h-screen lg:pl-72">
        <header className="sticky top-0 z-20 border-b border-white/70 bg-white/75 backdrop-blur-xl">
          <div className="container flex h-16 items-center gap-3">
            <Button variant="ghost" size="icon" className="lg:hidden">
              <Menu className="h-5 w-5" />
            </Button>
            <div>
              <div className="text-sm text-muted-foreground">Xin chào, Kint</div>
              <h1 className="text-xl font-black tracking-tight">{activeLabel}</h1>
            </div>
            <div className="ml-auto hidden w-full max-w-sm items-center gap-2 rounded-md border bg-white px-3 md:flex">
              <Search className="h-4 w-4 text-muted-foreground" />
              <input className="h-10 flex-1 bg-transparent text-sm outline-none" placeholder="Tìm giao dịch, địa điểm..." />
            </div>
            <Button variant="outline" size="icon">
              <Bell className="h-4 w-4" />
            </Button>
            <QuickAddDialog />
          </div>
        </header>

        <div className="container grid gap-5 py-5 pb-24 lg:pb-5">
          {activeView === "overview" && <OverviewView />}
          {activeView === "transactions" && <TransactionsView />}
          {activeView === "split" && <SplitView />}
          {activeView === "budget" && <BudgetView />}
          {activeView === "groups" && <GroupsView />}
          {activeView === "settings" && <SettingsView />}
        </div>
      </main>

      <nav className="mobile-safe fixed bottom-0 left-0 right-0 z-40 border-t bg-white/90 px-3 py-2 backdrop-blur-xl lg:hidden">
        <div className="grid grid-cols-4 gap-1">
          {navItems.slice(0, 4).map((item) => (
            <button
              key={item.id}
              onClick={() => setActiveView(item.id)}
              className={`flex flex-col items-center gap-1 rounded-md px-2 py-2 text-[11px] font-semibold ${
                activeView === item.id ? "bg-blue-50 text-blue-600" : "text-slate-500"
              }`}
            >
              <item.icon className="h-4 w-4" />
              {item.label}
            </button>
          ))}
        </div>
      </nav>
    </div>
  );
}

function OverviewView() {
  return (
    <>
      <section className="grid gap-4 md:grid-cols-3">
        {metrics.map((metric) => (
          <div key={metric.label} className={`metric-card ${metric.tone}`}>
            <div className={`mb-4 grid h-10 w-10 place-items-center rounded-md ${metric.bg}`}>
              <metric.icon className="h-5 w-5" />
            </div>
            <div className="text-sm font-medium text-slate-500">{metric.label}</div>
            <div className="mt-1 text-2xl font-black text-slate-950">{money(metric.value)}</div>
          </div>
        ))}
      </section>

      <section className="grid gap-5 xl:grid-cols-[1.35fr_0.75fr]">
        <RecentTransactionsCard />
        <div className="grid gap-5">
          <BudgetCard />
          <CheckInCard />
        </div>
      </section>
    </>
  );
}

function TransactionsView() {
  return (
    <section className="grid gap-5 xl:grid-cols-[1.25fr_0.75fr]">
      <Card className="overflow-hidden">
        <CardHeader className="gap-4 md:flex-row md:items-center md:justify-between">
          <div>
            <CardTitle>Giao dịch</CardTitle>
            <CardDescription>Lọc, tìm kiếm và theo dõi các khoản thu chi.</CardDescription>
          </div>
          <div className="flex gap-2">
            <Input className="w-40" placeholder="Tìm kiếm" />
            <Button variant="outline" size="sm">
              <CalendarDays className="h-4 w-4" />
              Tháng 06
            </Button>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          <TransactionRows />
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Gợi ý nhanh</CardTitle>
          <CardDescription>Phân loại dựa trên ghi chú và vị trí.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {["Cơm, bún, phở -> Ăn uống", "Grab, xăng, xe -> Di chuyển", "Siêu thị, chợ -> Mua sắm"].map((text) => (
            <div key={text} className="rounded-md border bg-slate-50 p-3 text-sm font-medium">
              {text}
            </div>
          ))}
        </CardContent>
      </Card>
    </section>
  );
}

function SplitView() {
  return (
    <section className="grid gap-5 lg:grid-cols-[1fr_0.8fr]">
      <Card>
        <CardHeader>
          <CardTitle>Chia tiền</CardTitle>
          <CardDescription>Theo dõi các khoản cần chia trong nhóm.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {splitItems.map((item) => (
            <div key={item.name} className="flex items-center justify-between rounded-md border bg-white p-4">
              <div>
                <div className="font-bold">{item.name}</div>
                <div className="text-sm text-muted-foreground">{item.members} người tham gia</div>
              </div>
              <div className="text-right">
                <div className="font-black">{money(item.amount)}</div>
                <Badge variant={item.status === "Đã xong" ? "success" : "default"}>{item.status}</Badge>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card className="bg-slate-950 text-white">
        <CardHeader>
          <CardTitle>Tạo khoản chia mới</CardTitle>
          <CardDescription className="text-slate-400">Nhập tổng tiền, chọn thành viên và tỷ lệ chia.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <Input placeholder="Tên khoản chia" />
          <Input placeholder="Số tiền" inputMode="decimal" />
          <Button className="bg-white text-slate-950 hover:bg-slate-100">Tạo khoản chia</Button>
        </CardContent>
      </Card>
    </section>
  );
}

function BudgetView() {
  return (
    <section className="grid gap-5 lg:grid-cols-[1fr_0.8fr]">
      <BudgetCard />
      <Card>
        <CardHeader>
          <CardTitle>Cảnh báo</CardTitle>
          <CardDescription>Những mục chi gần chạm giới hạn.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="rounded-md border border-amber-200 bg-amber-50 p-4 text-sm text-amber-800">
            Mua sắm đã dùng 84% ngân sách tháng này.
          </div>
          <div className="rounded-md border border-blue-200 bg-blue-50 p-4 text-sm text-blue-800">
            Ăn uống vẫn còn {money(1600000)} để chi.
          </div>
        </CardContent>
      </Card>
    </section>
  );
}

function GroupsView() {
  return (
    <section className="grid gap-5 lg:grid-cols-[1fr_0.8fr]">
      <Card>
        <CardHeader>
          <CardTitle>Gia đình nhỏ</CardTitle>
          <CardDescription>Mã mời: GDN123</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          {members.map((member) => (
            <div key={member.name} className="flex items-center justify-between rounded-md border bg-white p-4">
              <div className="flex items-center gap-3">
                <div className="grid h-10 w-10 place-items-center rounded-full bg-blue-50 font-black text-blue-600">
                  {member.name[0]}
                </div>
                <div>
                  <div className="font-bold">{member.name}</div>
                  <div className="text-sm text-muted-foreground">{member.role}</div>
                </div>
              </div>
              <div className="font-black">{money(member.spent)}</div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Mời thành viên</CardTitle>
          <CardDescription>Chia sẻ mã nhóm cho người thân.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <div className="rounded-md border bg-slate-50 p-4 text-center text-2xl font-black tracking-[0.3em] text-blue-600">
            GDN123
          </div>
          <Button>Sao chép mã mời</Button>
        </CardContent>
      </Card>
    </section>
  );
}

function SettingsView() {
  return (
    <section className="grid gap-5 lg:grid-cols-2">
      <Card>
        <CardHeader>
          <CardTitle>Cài đặt tài khoản</CardTitle>
          <CardDescription>Thông tin hiển thị và tuỳ chọn cá nhân.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <Input defaultValue="Kint" />
          <Input defaultValue="kintko321@gmail.com" />
          <Button>Lưu thay đổi</Button>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Kết nối email VCB</CardTitle>
          <CardDescription>Bật lại khi cần tự động đọc giao dịch.</CardDescription>
        </CardHeader>
        <CardContent className="space-y-3">
          <div className="rounded-md border bg-slate-50 p-4 text-sm text-muted-foreground">
            Hiện tại ưu tiên nhập thủ công kèm check-in vị trí.
          </div>
          <Button variant="outline">Cấu hình email</Button>
        </CardContent>
      </Card>
    </section>
  );
}

function RecentTransactionsCard() {
  return (
    <Card className="overflow-hidden">
      <CardHeader className="flex-row items-center justify-between gap-4">
        <div>
          <CardTitle>Giao dịch gần đây</CardTitle>
          <CardDescription>Tự động gắn check-in khi bạn lưu chi tiêu.</CardDescription>
        </div>
        <Button variant="outline" size="sm">
          <CalendarDays className="h-4 w-4" />
          Tháng 06
        </Button>
      </CardHeader>
      <CardContent className="p-0">
        <TransactionRows />
      </CardContent>
    </Card>
  );
}

function TransactionRows() {
  return (
    <div className="divide-y">
      {transactions.map((tx) => (
        <div key={tx.title} className="flex items-start gap-4 px-5 py-4 transition hover:bg-slate-50">
          <div className="grid h-11 w-11 shrink-0 place-items-center rounded-md bg-slate-100 text-slate-700">
            {tx.amount > 0 ? <BadgeDollarSign className="h-5 w-5" /> : <CreditCard className="h-5 w-5" />}
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <div className="font-bold">{tx.title}</div>
              {tx.shared && <Badge variant="default">Chung</Badge>}
            </div>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
              <span>{tx.category}</span>
              <span>-</span>
              <span>{tx.date}</span>
              <Badge variant="success" className="gap-1">
                <MapPin className="h-3 w-3" />
                {tx.location}
              </Badge>
            </div>
          </div>
          <div className={tx.amount > 0 ? "font-black text-emerald-600" : "font-black text-red-600"}>
            {tx.amount > 0 ? "+" : "-"}
            {money(Math.abs(tx.amount))}
          </div>
        </div>
      ))}
    </div>
  );
}

function BudgetCard() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Ngân sách</CardTitle>
        <CardDescription>Theo dõi các mục chi lớn.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {budgets.map((budget) => {
          const pct = Math.min((budget.spent / budget.limit) * 100, 100);
          return (
            <div key={budget.label}>
              <div className="mb-2 flex items-center justify-between text-sm">
                <span className="font-semibold">{budget.label}</span>
                <span className="text-muted-foreground">
                  {money(budget.spent)} / {money(budget.limit)}
                </span>
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                <div className={`h-full rounded-full ${budget.color}`} style={{ width: `${pct}%` }} />
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}

function CheckInCard() {
  return (
    <Card className="bg-slate-950 text-white">
      <CardHeader>
        <CardTitle>Check-in chi tiêu</CardTitle>
        <CardDescription className="text-slate-400">
          Lưu toạ độ khi vừa thanh toán để xem lại mình chi ở đâu.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <Button className="w-full bg-white text-slate-950 hover:bg-slate-100">
          <LocateFixed className="h-4 w-4" />
          Thử lấy vị trí
        </Button>
      </CardContent>
    </Card>
  );
}

function QuickAddDialog() {
  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button>
          <Plus className="h-4 w-4" />
          <span className="hidden sm:inline">Thêm giao dịch</span>
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Thêm giao dịch nhanh</DialogTitle>
          <DialogDescription>Lưu chi tiêu kèm check-in hiện tại.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid gap-2">
            <label className="text-sm font-semibold">Số tiền</label>
            <Input placeholder="500000, 500k, 1.2m" inputMode="decimal" />
          </div>
          <div className="grid gap-2">
            <label className="text-sm font-semibold">Ghi chú</label>
            <Input placeholder="Cơm trưa, cà phê, siêu thị..." />
          </div>
          <div className="rounded-md border bg-slate-50 p-3">
            <div className="flex items-center justify-between gap-3">
              <div>
                <div className="text-sm font-bold">Vị trí check-in</div>
                <div className="text-xs text-muted-foreground">Xin quyền GPS của trình duyệt khi lưu.</div>
              </div>
              <Button variant="outline" size="sm">
                <MapPin className="h-4 w-4" />
                Lấy vị trí
              </Button>
            </div>
          </div>
          <Button className="w-full">
            <ReceiptText className="h-4 w-4" />
            Lưu giao dịch
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

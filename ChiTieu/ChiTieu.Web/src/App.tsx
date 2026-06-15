import { useEffect, useMemo, useState, type Dispatch, type SetStateAction } from "react";
import {
  ArrowDownRight,
  ArrowUpRight,
  BadgeDollarSign,
  Bell,
  BriefcaseBusiness,
  CalendarDays,
  Check,
  ClipboardList,
  Clock3,
  CreditCard,
  HandCoins,
  Home,
  LocateFixed,
  MapPin,
  Menu,
  Navigation,
  PieChart,
  Plus,
  ReceiptText,
  RefreshCw,
  Search,
  Settings,
  Split,
  Target,
  Trash2,
  Users,
  WalletCards,
} from "lucide-react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { money } from "@/lib/utils";

type ViewId = "overview" | "transactions" | "budget" | "fund" | "debts" | "reports" | "split" | "groups" | "settings" | "tasks" | "task-board" | "task-calendar" | "locator";
type AppMode = "finance" | "work";

type AppState = {
  group: Group | null;
  groups: Group[];
  members: Member[];
  transactions: Transaction[];
  budgets: Budget[];
  debts: Debt[];
  fund: Fund | null;
  fundTransactions: FundTransaction[];
  splits: SplitItem[];
  report: Report;
  overviewReport: Report;
  budgetTotal: number;
  overviewTransactions: Transaction[];
  notifications: NotificationItem[];
  currentUserId: string;
};

type Group = { id: number; name: string; description: string; inviteCode: string };
type Member = { id: string; name: string; email: string; role: string; spent: number };
type Transaction = {
  id: number;
  type: "income" | "expense";
  amount: number;
  category: string;
  note: string;
  isShared: boolean;
  date: string;
  fromEmail: boolean;
  userId: string;
  userName: string;
  latitude?: number | null;
  longitude?: number | null;
  locationAccuracy?: number | null;
  locationName: string;
};
type Budget = { id: number; categoryId: string; amount: number; month: string; spent: number };
type Debt = { id: number; debtorName: string; creditorName: string; amount: number; note: string; createdAt: string };
type Fund = { id: number; name: string; balance: number };
type FundTransaction = { id: number; type: "deposit" | "withdraw"; amount: number; note: string; date: string };
type SplitItem = {
  id: number;
  description: string;
  totalAmount: number;
  splitType: string;
  date: string;
  isSettled: boolean;
  memberCount: number;
  paidCount: number;
};
type NotificationItem = { id: number; groupId: number; type: string; title: string; message: string; isRead: boolean; createdAt: string };
type TaskItem = {
  id: string;
  title: string;
  description?: string;
  period: "morning" | "afternoon" | "evening";
  cadence: "daily" | "weekly" | "monthly" | "event";
  priority?: "low" | "normal" | "high";
  project?: string;
  date: string;
  time?: string;
  done: boolean;
};
type Report = {
  income: number;
  expense: number;
  balance: number;
  count: number;
  byCategory: Record<string, number>;
  byMember: Record<string, number>;
};

const emptyState: AppState = {
  group: null,
  groups: [],
  members: [],
  transactions: [],
  budgets: [],
  debts: [],
  fund: null,
  fundTransactions: [],
  splits: [],
  report: { income: 0, expense: 0, balance: 0, count: 0, byCategory: {}, byMember: {} },
  overviewReport: { income: 0, expense: 0, balance: 0, count: 0, byCategory: {}, byMember: {} },
  budgetTotal: 0,
  overviewTransactions: [],
  notifications: [],
  currentUserId: "",
};

const financeNavItems: Array<{ id: ViewId; label: string; icon: typeof Home }> = [
  { id: "overview", label: "Tổng quan", icon: Home },
  { id: "transactions", label: "Giao dịch", icon: ReceiptText },
  { id: "budget", label: "Ngân sách", icon: PieChart },
  { id: "fund", label: "Quỹ chung", icon: WalletCards },
  { id: "debts", label: "Công nợ", icon: HandCoins },
  { id: "reports", label: "Báo cáo", icon: BadgeDollarSign },
  { id: "split", label: "Chia tiền", icon: Split },
  { id: "groups", label: "Nhóm", icon: Users },
  { id: "settings", label: "Cài đặt", icon: Settings },
];

const workNavItems: Array<{ id: ViewId; label: string; icon: typeof Home }> = [
  { id: "tasks", label: "Today", icon: ClipboardList },
  { id: "task-board", label: "Board", icon: Target },
  { id: "task-calendar", label: "Calendar", icon: CalendarDays },
  { id: "locator", label: "Dinh vi", icon: MapPin },
  { id: "settings", label: "Cài đặt", icon: Settings },
];

const allNavItems = [...financeNavItems, ...workNavItems];

const expenseCategories = [
  { id: "food", label: "Ăn uống" },
  { id: "transport", label: "Di chuyển" },
  { id: "rent", label: "Tiền nhà" },
  { id: "utilities", label: "Điện nước" },
  { id: "internet", label: "Internet" },
  { id: "supermarket", label: "Siêu thị" },
  { id: "shopping", label: "Mua sắm" },
  { id: "entertainment", label: "Giải trí" },
  { id: "health", label: "Y tế" },
  { id: "education", label: "Học tập" },
  { id: "travel", label: "Du lịch" },
  { id: "other", label: "Khác" },
];

const incomeCategories = [
  { id: "salary", label: "Lương" },
  { id: "bonus", label: "Thưởng" },
  { id: "freelance", label: "Làm thêm" },
  { id: "investment", label: "Đầu tư" },
  { id: "other", label: "Khác" },
];

function monthKey() {
  return new Date().toISOString().slice(0, 7);
}

function dateKey() {
  return new Date().toISOString().slice(0, 10);
}

function parseAmount(input: string) {
  const normalized = input.trim().toLowerCase().replace(/vnd/g, "").replace(/đ/g, "").replace(/,/g, "").replace(/\s/g, "");
  if (!normalized) return 0;
  const multiplier = normalized.endsWith("k") ? 1000 : normalized.endsWith("m") ? 1000000 : 1;
  const raw = multiplier === 1 ? normalized : normalized.slice(0, -1);
  const value = Number(raw);
  return Number.isFinite(value) ? value * multiplier : 0;
}

function formatAmountInput(input: string) {
  if (/[a-zA-Z.]/.test(input)) return input;
  const digits = input.replace(/\D/g, "");
  if (!digits) return "";
  return Number(digits).toLocaleString("en-US");
}

function useLocalTasks() {
  const [tasks, setTasks] = useState<TaskItem[]>(() => {
    try {
      return JSON.parse(localStorage.getItem("chitieu.tasks") || "[]") as TaskItem[];
    } catch {
      return [];
    }
  });

  useEffect(() => {
    localStorage.setItem("chitieu.tasks", JSON.stringify(tasks));
  }, [tasks]);

  return [tasks, setTasks] as const;
}

function labelOf(category: string) {
  return [...expenseCategories, ...incomeCategories].find((item) => item.id === category)?.label ?? "Khác";
}

async function api<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    credentials: "include",
    headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
    ...init,
  });
  if (response.status === 401) {
    window.location.href = "/account/login?returnUrl=/react/home";
    throw new Error("Bạn cần đăng nhập lại.");
  }
  if (response.redirected) {
    const redirectedUrl = new URL(response.url);
    window.location.href = redirectedUrl.pathname.startsWith("/api")
      ? "/account/login?returnUrl=/react/home"
      : response.url;
    throw new Error("Bạn cần đăng nhập lại.");
  }
  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || `HTTP ${response.status}`);
  }
  if (response.status === 204) return undefined as T;
  return response.json();
}

export default function App() {
  const [activeView, setActiveView] = useState<ViewId>("overview");
  const [appMode, setAppMode] = useState<AppMode>("finance");
  const [activeGroupId, setActiveGroupId] = useState<number | null>(null);
  const [state, setState] = useState<AppState>(emptyState);
  const [tasks, setTasks] = useLocalTasks();
  const [month, setMonth] = useState(monthKey());
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const currentNavItems = appMode === "finance" ? financeNavItems : workNavItems;
  const activeLabel = useMemo(() => allNavItems.find((item) => item.id === activeView)?.label ?? "Tổng quan", [activeView]);

  const load = async () => {
    setLoading(true);
    setMessage("");
    try {
      const query = new URLSearchParams({ month });
      if (activeGroupId) query.set("groupId", String(activeGroupId));
      const nextState = await api<AppState>(`/api/app?${query.toString()}`);
      setState(nextState);
      if (!activeGroupId && nextState.group) setActiveGroupId(nextState.group.id);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không tải được dữ liệu.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [month, activeGroupId]);

  const navigate = (view: ViewId) => {
    setActiveView(view);
    setMobileMenuOpen(false);
    window.requestAnimationFrame(() => window.scrollTo({ top: 0, behavior: "smooth" }));
  };

  const switchMode = () => {
    const nextMode: AppMode = appMode === "finance" ? "work" : "finance";
    setAppMode(nextMode);
    setActiveView(nextMode === "finance" ? "overview" : "tasks");
    setMobileMenuOpen(false);
    window.requestAnimationFrame(() => window.scrollTo({ top: 0, behavior: "smooth" }));
  };

  const filteredTransactions = state.transactions.filter((tx) => {
    const haystack = `${tx.note} ${tx.userName} ${labelOf(tx.category)} ${tx.locationName}`.toLowerCase();
    return (!typeFilter || tx.type === typeFilter) && (!search || haystack.includes(search.toLowerCase()));
  });

  return (
    <div className="app-shell min-h-screen">
      <aside className="fixed left-0 top-0 z-30 hidden h-screen w-72 border-r border-slate-200 bg-slate-950 text-white lg:block">
        <div className="flex h-full flex-col p-5">
          <div className="flex items-center gap-3 px-2 py-2">
            <div className="grid h-11 w-11 place-items-center rounded-md bg-teal-500 font-black text-slate-950">đ</div>
            <div>
              <div className="text-lg font-black">Chi Tiêu</div>
              <div className="text-xs text-slate-400">{state.group?.name ?? "Chưa có nhóm"}</div>
            </div>
          </div>

          <nav className="mt-8 space-y-1">
            {currentNavItems.map((item) => (
              <button
                key={item.id}
                onClick={() => navigate(item.id)}
                className={[
                  "flex w-full items-center gap-3 rounded-md px-3 py-2.5 text-sm font-semibold transition",
                  activeView === item.id ? "bg-white text-slate-950" : "text-slate-400 hover:bg-white/10 hover:text-white",
                ].join(" ")}
              >
                <item.icon className="h-4 w-4" />
                {item.label}
              </button>
            ))}
          </nav>

          <div className="mt-auto rounded-md border border-white/10 bg-white/5 p-4">
            <div className="text-sm font-semibold">Mã mời</div>
            <div className="mt-2 text-2xl font-black tracking-[0.25em] text-teal-300">{state.group?.inviteCode ?? "------"}</div>
          </div>
        </div>
      </aside>

      <main className="min-h-screen lg:pl-72">
        <header className="sticky top-0 z-20 border-b border-white/70 bg-white/85 backdrop-blur-xl">
          <div className="container flex h-16 items-center gap-3">
            <Button variant="ghost" size="icon" className="lg:hidden" onClick={() => setMobileMenuOpen((current) => !current)}>
              <Menu className="h-5 w-5" />
            </Button>
            <div className="min-w-0">
              <div className="truncate text-sm text-muted-foreground">{state.group?.name ?? "Tài chính nhóm"}</div>
              <h1 className="whitespace-normal text-xl font-black leading-tight tracking-tight">{activeLabel}</h1>
            </div>
            {state.groups.length > 1 && appMode === "finance" && (
              <select className="input-like hidden w-44 md:block" value={activeGroupId ?? state.group?.id ?? ""} onChange={(event) => setActiveGroupId(Number(event.target.value))}>
                {state.groups.map((group) => (
                  <option key={group.id} value={group.id}>{group.name}</option>
                ))}
              </select>
            )}
            <div className="ml-auto hidden w-full max-w-sm items-center gap-2 rounded-md border bg-white px-3 md:flex">
              <Search className="h-4 w-4 text-muted-foreground" />
              <input
                className="h-10 flex-1 bg-transparent text-sm outline-none"
                placeholder="Tìm giao dịch, địa điểm..."
                value={search}
                onChange={(event) => setSearch(event.target.value)}
              />
            </div>
            <Button variant="outline" size="icon" onClick={load} disabled={loading}>
              <RefreshCw className={`h-4 w-4 ${loading ? "animate-spin" : ""}`} />
            </Button>
            <NotificationsButton notifications={state.notifications} onChanged={load} />
            <Button variant={appMode === "work" ? "default" : "outline"} size="icon" onClick={switchMode} title={appMode === "finance" ? "Mở quản lý công việc" : "Quay lại tài chính"}>
              {appMode === "finance" ? <BriefcaseBusiness className="h-4 w-4" /> : <WalletCards className="h-4 w-4" />}
            </Button>
            {appMode === "finance" && <QuickAddDialog onSaved={load} disabled={!state.group} members={state.members} groupId={state.group?.id} />}
          </div>
          {mobileMenuOpen && (
            <div className="container grid gap-2 pb-3 lg:hidden">
              <div className="grid grid-cols-2 gap-2 rounded-md border bg-white p-2 shadow-soft">
                {currentNavItems.slice(appMode === "finance" ? 5 : 0).map((item) => (
                  <button
                    key={item.id}
                    onClick={() => navigate(item.id)}
                    className={`flex min-w-0 items-center gap-2 rounded-md px-3 py-2 text-sm font-semibold ${
                      activeView === item.id ? "bg-blue-50 text-blue-700" : "text-slate-600"
                    }`}
                  >
                    <item.icon className="h-4 w-4 shrink-0" />
                    <span className="truncate">{item.label}</span>
                  </button>
                ))}
              </div>
            </div>
          )}
        </header>

        <div className="container grid gap-5 py-5 pb-24 lg:pb-5">
          {message && <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm font-medium text-red-700">{message}</div>}
          {!state.group && !loading && <EmptyGroup onSaved={load} />}
          {state.group && (
            <>
              {activeView === "overview" && <OverviewView state={state} onRefresh={load} />}
              {activeView === "transactions" && (
                <TransactionsView
                  transactions={filteredTransactions}
                  month={month}
                  setMonth={setMonth}
                  typeFilter={typeFilter}
                  setTypeFilter={setTypeFilter}
                  onDeleted={load}
                />
              )}
              {activeView === "budget" && <BudgetView budgets={state.budgets} month={month} setMonth={setMonth} onSaved={load} groupId={state.group.id} />}
              {activeView === "fund" && <FundView state={state} onSaved={load} groupId={state.group.id} />}
              {activeView === "debts" && <DebtsView debts={state.debts} onSaved={load} />}
              {activeView === "reports" && <ReportsView report={state.report} month={month} setMonth={setMonth} />}
              {activeView === "split" && <SplitView splits={state.splits} members={state.members} onSaved={load} groupId={state.group.id} />}
              {activeView === "groups" && <GroupsView state={state} month={month} activeGroupId={state.group.id} onSaved={load} onSelectGroup={setActiveGroupId} />}
              {(activeView === "tasks" || activeView === "task-board" || activeView === "task-calendar") && <TasksView tasks={tasks} setTasks={setTasks} view={activeView} />}
              {activeView === "locator" && <LocatorView />}
              {activeView === "settings" && <SettingsView />}
            </>
          )}
        </div>
      </main>

      <nav className="mobile-safe fixed bottom-0 left-0 right-0 z-40 border-t bg-white/95 px-3 py-2 backdrop-blur-xl lg:hidden">
        <div className="grid grid-cols-5 gap-1">
          {currentNavItems.slice(0, 5).map((item) => (
            <button
              key={item.id}
              onClick={() => navigate(item.id)}
              className={`flex h-12 min-w-0 flex-col items-center gap-1 rounded-md px-1 py-2 text-[10px] font-semibold ${
                activeView === item.id ? "bg-blue-50 text-blue-600" : "text-slate-500"
              }`}
            >
              <item.icon className="h-4 w-4 shrink-0" />
              <span className="w-full text-center leading-tight">{item.label}</span>
            </button>
          ))}
        </div>
      </nav>
    </div>
  );
}

function NotificationsButton({ notifications, onChanged }: { notifications: NotificationItem[]; onChanged: () => void }) {
  const [open, setOpen] = useState(false);
  const unread = notifications.filter((item) => !item.isRead).length;

  const markRead = async () => {
    await api<void>("/api/notifications/read", { method: "POST" });
    setOpen(false);
    onChanged();
  };

  return (
    <div className="relative">
      <Button variant="outline" size="icon" onClick={() => setOpen((current) => !current)} title="Thong bao">
        <Bell className="h-4 w-4" />
        {unread > 0 && <span className="absolute -right-1 -top-1 grid h-5 min-w-5 place-items-center rounded-full bg-red-600 px-1 text-[10px] font-black text-white">{unread}</span>}
      </Button>
      {open && (
        <div className="absolute right-0 top-12 z-50 w-[min(22rem,calc(100vw-1.5rem))] overflow-hidden rounded-md border bg-white shadow-xl">
          <div className="flex items-center justify-between border-b px-4 py-3">
            <div className="font-black">Thong bao</div>
            <button className="text-xs font-bold text-blue-600" onClick={markRead}>Da doc</button>
          </div>
          <div className="max-h-80 overflow-y-auto">
            {notifications.length === 0 ? <div className="p-4 text-sm text-muted-foreground">Chua co thong bao.</div> : null}
            {notifications.map((item) => (
              <div key={item.id} className={`border-b px-4 py-3 text-sm ${item.isRead ? "bg-white" : "bg-blue-50"}`}>
                <div className="font-bold">{item.title}</div>
                <div className="mt-1 text-muted-foreground">{item.message}</div>
                <div className="mt-1 text-xs text-slate-400">{new Date(item.createdAt).toLocaleString("vi-VN")}</div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function OverviewView({ state, onRefresh }: { state: AppState; onRefresh: () => void }) {
  const overview = state.overviewReport ?? state.report;
  const homeTransactions = (state.overviewTransactions?.length ? state.overviewTransactions : state.transactions).slice(0, 6);
  const homeMetrics = [
    { label: "Tong ngan sach", value: state.budgetTotal, icon: Target, tone: "text-blue-600", bg: "bg-blue-50" },
    { label: "Thu tat ca nhom", value: overview.income, icon: ArrowUpRight, tone: "text-emerald-600", bg: "bg-emerald-50" },
    { label: "Chi tat ca nhom", value: overview.expense, icon: ArrowDownRight, tone: "text-red-600", bg: "bg-red-50" },
  ];

  return (
    <>
      <section className="grid gap-4 md:grid-cols-3">
        {homeMetrics.map((metric) => (
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
        <RecentTransactionsCard transactions={homeTransactions} onDeleted={onRefresh} />
        <div className="grid gap-5">
          <BudgetList budgets={state.budgets} />
          <CheckInCard />
        </div>
      </section>
    </>
  );

  const metrics = [
    { label: "Thu tháng này", value: state.report.income, icon: ArrowUpRight, tone: "text-emerald-600", bg: "bg-emerald-50" },
    { label: "Chi tháng này", value: state.report.expense, icon: ArrowDownRight, tone: "text-red-600", bg: "bg-red-50" },
    { label: "Số dư dự kiến", value: state.report.balance, icon: WalletCards, tone: "text-blue-600", bg: "bg-blue-50" },
  ];
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
        <RecentTransactionsCard transactions={state.transactions.slice(0, 6)} onDeleted={onRefresh} />
        <div className="grid gap-5">
          <BudgetList budgets={state.budgets} />
          <CheckInCard />
        </div>
      </section>
    </>
  );
}

function TransactionsView(props: {
  transactions: Transaction[];
  month: string;
  setMonth: (month: string) => void;
  typeFilter: string;
  setTypeFilter: (type: string) => void;
  onDeleted: () => void;
}) {
  return (
    <Card className="overflow-hidden">
      <CardHeader className="gap-4 md:flex-row md:items-center md:justify-between">
        <div>
          <CardTitle>Giao dịch</CardTitle>
          <CardDescription>Lọc, tìm kiếm và theo dõi các khoản thu chi.</CardDescription>
        </div>
        <div className="flex flex-wrap gap-2">
          <select className="input-like" value={props.typeFilter} onChange={(event) => props.setTypeFilter(event.target.value)}>
            <option value="">Tất cả</option>
            <option value="income">Thu</option>
            <option value="expense">Chi</option>
          </select>
          <Input className="w-40" type="month" value={props.month} onChange={(event) => props.setMonth(event.target.value)} />
        </div>
      </CardHeader>
      <CardContent className="p-0">
        <TransactionRows transactions={props.transactions} onDeleted={props.onDeleted} />
      </CardContent>
    </Card>
  );
}

function BudgetView({ budgets, month, setMonth, onSaved, groupId }: { budgets: Budget[]; month: string; setMonth: (month: string) => void; onSaved: () => void; groupId: number }) {
  const [categoryId, setCategoryId] = useState("food");
  const [amount, setAmount] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const save = async () => {
    setError("");
    const value = parseAmount(amount);
    if (value <= 0) return setError("Số tiền ngân sách chưa hợp lệ.");
    setSaving(true);
    try {
      await api<void>("/api/budgets", { method: "POST", body: JSON.stringify({ groupId, categoryId, month, amount: value }) });
      setAmount("");
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không lưu được ngân sách.");
    } finally {
      setSaving(false);
    }
  };

  const remove = async (id: number) => {
    await api<void>(`/api/budgets/${id}`, { method: "DELETE" });
    onSaved();
  };

  return (
    <section className="grid gap-5">
      <Card>
        <CardHeader>
          <CardTitle>Đặt ngân sách</CardTitle>
          <CardDescription>Thiết lập giới hạn chi theo danh mục cho từng tháng.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3 md:grid-cols-[1fr_180px_1fr_auto]">
          <select className="input-like" value={categoryId} onChange={(event) => setCategoryId(event.target.value)}>
            {expenseCategories.map((cat) => (
              <option value={cat.id} key={cat.id}>
                {cat.label}
              </option>
            ))}
          </select>
          <Input type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
          <Input placeholder="3m, 2500000" value={amount} onChange={(event) => setAmount(formatAmountInput(event.target.value))} />
          <Button onClick={save} disabled={saving}>Lưu</Button>
          {error && <div className="text-sm font-medium text-red-600 md:col-span-4">{error}</div>}
        </CardContent>
      </Card>
      <BudgetList budgets={budgets} onDelete={remove} />
    </section>
  );
}

function FundView({ state, onSaved, groupId }: { state: AppState; onSaved: () => void; groupId: number }) {
  const [type, setType] = useState("deposit");
  const [amount, setAmount] = useState("");
  const [note, setNote] = useState("");

  const save = async () => {
    const value = parseAmount(amount);
    if (value <= 0) return;
    await api<void>("/api/fund-transactions", { method: "POST", body: JSON.stringify({ groupId, type, amount: value, note }) });
    setAmount("");
    setNote("");
    onSaved();
  };

  return (
    <section className="grid gap-5 lg:grid-cols-[0.8fr_1.2fr]">
      <Card>
        <CardHeader>
          <CardTitle>{state.fund?.name ?? "Quỹ chung"}</CardTitle>
          <CardDescription>Số dư quỹ hiện tại</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="text-4xl font-black">{money(state.fund?.balance ?? 0)}</div>
          <div className="mt-5 grid gap-3">
            <select className="input-like" value={type} onChange={(event) => setType(event.target.value)}>
              <option value="deposit">Nạp quỹ</option>
              <option value="withdraw">Rút quỹ</option>
            </select>
            <Input placeholder="Số tiền" value={amount} onChange={(event) => setAmount(formatAmountInput(event.target.value))} />
            <Input placeholder="Ghi chú" value={note} onChange={(event) => setNote(event.target.value)} />
            <Button onClick={save}>Lưu quỹ</Button>
          </div>
        </CardContent>
      </Card>
      <Card className="overflow-hidden">
        <CardHeader>
          <CardTitle>Lịch sử quỹ</CardTitle>
          <CardDescription>Các lần nạp và rút gần đây.</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {state.fundTransactions.length === 0 ? <EmptyRows text="Chưa có giao dịch quỹ." /> : null}
          {state.fundTransactions.map((tx) => (
            <div key={tx.id} className="flex items-center justify-between border-t px-5 py-4">
              <div>
                <div className="font-bold">{tx.note || (tx.type === "deposit" ? "Nạp quỹ" : "Rút quỹ")}</div>
                <div className="text-sm text-muted-foreground">{new Date(tx.date).toLocaleDateString("vi-VN")}</div>
              </div>
              <div className={tx.type === "deposit" ? "font-black text-emerald-600" : "font-black text-red-600"}>
                {tx.type === "deposit" ? "+" : "-"}{money(tx.amount)}
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    </section>
  );
}

function DebtsView({ debts, onSaved }: { debts: Debt[]; onSaved: () => void }) {
  const settle = async (id: number) => {
    await api<void>(`/api/debts/${id}/settle`, { method: "POST" });
    onSaved();
  };

  return (
    <Card className="overflow-hidden">
      <CardHeader>
        <CardTitle>Công nợ</CardTitle>
        <CardDescription>Các khoản cần hoàn lại trong nhóm.</CardDescription>
      </CardHeader>
      <CardContent className="p-0">
        {debts.length === 0 ? <EmptyRows text="Chưa có công nợ." /> : null}
        {debts.map((debt) => (
          <div key={debt.id} className="flex flex-wrap items-center justify-between gap-3 border-t px-5 py-4">
            <div>
              <div className="font-bold">{debt.debtorName} trả {debt.creditorName}</div>
              <div className="text-sm text-muted-foreground">{debt.note || "Khoản chia tiền"} · {new Date(debt.createdAt).toLocaleDateString("vi-VN")}</div>
            </div>
            <div className="flex items-center gap-3">
              <div className="font-black text-red-600">{money(debt.amount)}</div>
              <Button size="sm" variant="outline" onClick={() => settle(debt.id)}>
                <Check className="h-4 w-4" />
                Đã trả
              </Button>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}

function ReportsView({ report, month, setMonth }: { report: Report; month: string; setMonth: (month: string) => void }) {
  return (
    <section className="grid gap-5">
      <div className="flex justify-end">
        <Input className="w-44" type="month" value={month} onChange={(event) => setMonth(event.target.value)} />
      </div>
      <section className="grid gap-4 md:grid-cols-4">
        <Stat label="Thu" value={`+${money(report.income)}`} tone="text-emerald-600" />
        <Stat label="Chi" value={`-${money(report.expense)}`} tone="text-red-600" />
        <Stat label="Số dư" value={money(report.balance)} tone={report.balance >= 0 ? "text-emerald-600" : "text-red-600"} />
        <Stat label="Giao dịch" value={String(report.count)} tone="text-slate-950" />
      </section>
      <section className="grid gap-5 lg:grid-cols-2">
        <ReportBars title="Chi theo danh mục" items={report.byCategory} total={report.expense} labeler={labelOf} />
        <ReportBars title="Chi theo thành viên" items={report.byMember} total={report.expense} labeler={(v) => v} />
      </section>
    </section>
  );
}

function SplitView({ splits, members, onSaved, groupId }: { splits: SplitItem[]; members: Member[]; onSaved: () => void; groupId: number }) {
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [participantIds, setParticipantIds] = useState<string[]>(members.map((member) => member.id));
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setParticipantIds(members.map((member) => member.id));
  }, [members]);

  const toggleMember = (id: string) => {
    setParticipantIds((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]));
  };

  const save = async () => {
    setError("");
    const totalAmount = parseAmount(amount);
    if (totalAmount <= 0) return setError("Số tiền chia chưa hợp lệ.");
    if (participantIds.length === 0) return setError("Chọn ít nhất một thành viên.");
    setSaving(true);
    try {
      await api<void>("/api/splits", {
        method: "POST",
        body: JSON.stringify({ groupId, totalAmount, description, participantIds }),
      });
      setAmount("");
      setDescription("");
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không tạo được khoản chia.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="grid gap-5 lg:grid-cols-[1fr_0.8fr]">
      <Card className="overflow-hidden">
        <CardHeader>
          <CardTitle>Chia tiền</CardTitle>
          <CardDescription>Các khoản chia đang có trong nhóm.</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {splits.length === 0 ? <EmptyRows text="Chưa có khoản chia tiền." /> : null}
          {splits.map((item) => (
            <div key={item.id} className="flex items-center justify-between border-t px-5 py-4">
              <div>
                <div className="font-bold">{item.description || "Khoản chia tiền"}</div>
                <div className="text-sm text-muted-foreground">{item.memberCount} người · {new Date(item.date).toLocaleDateString("vi-VN")}</div>
              </div>
              <div className="text-right">
                <div className="font-black">{money(item.totalAmount)}</div>
                <Badge variant={item.isSettled ? "success" : "default"}>{item.paidCount}/{item.memberCount} đã trả</Badge>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle>Tạo khoản chia</CardTitle>
          <CardDescription>Chia đều cho các thành viên được chọn và tự sinh công nợ.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <Input placeholder="Mô tả" value={description} onChange={(event) => setDescription(event.target.value)} />
          <Input placeholder="Tổng tiền" value={amount} onChange={(event) => setAmount(formatAmountInput(event.target.value))} inputMode="decimal" />
          <div className="grid gap-2">
            {members.map((member) => (
              <label key={member.id} className="flex items-center justify-between rounded-md border bg-white px-3 py-2 text-sm font-semibold">
                <span>{member.name || member.email}</span>
                <input type="checkbox" checked={participantIds.includes(member.id)} onChange={() => toggleMember(member.id)} />
              </label>
            ))}
          </div>
          {error && <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm font-medium text-red-700">{error}</div>}
          <Button onClick={save} disabled={saving}>
            <Split className="h-4 w-4" />
            {saving ? "Đang tạo..." : "Tạo khoản chia"}
          </Button>
        </CardContent>
      </Card>
    </section>
  );
}

function GroupsView({
  state,
  month,
  activeGroupId,
  onSaved,
  onSelectGroup,
}: {
  state: AppState;
  month: string;
  activeGroupId: number;
  onSaved: () => void;
  onSelectGroup: (id: number) => void;
}) {
  const [selectedMemberId, setSelectedMemberId] = useState(state.members[0]?.id ?? "");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [saving, setSaving] = useState(false);
  const selectedMember = state.members.find((member) => member.id === selectedMemberId) ?? state.members[0];
  const currentMember = state.members.find((member) => member.id === state.currentUserId);
  const canRemoveMembers = currentMember?.role === "owner";
  const memberTransactions = selectedMember ? state.transactions.filter((tx) => tx.userId === selectedMember.id) : [];
  const memberIncome = memberTransactions.filter((tx) => tx.type === "income").reduce((sum, tx) => sum + tx.amount, 0);
  const memberExpense = memberTransactions.filter((tx) => tx.type === "expense").reduce((sum, tx) => sum + tx.amount, 0);

  useEffect(() => {
    if (!selectedMemberId && state.members[0]) setSelectedMemberId(state.members[0].id);
  }, [selectedMemberId, state.members]);

  const createGroup = async () => {
    if (!name.trim()) return;
    setSaving(true);
    try {
      const group = await api<Group>("/api/groups", { method: "POST", body: JSON.stringify({ name, description }) });
      setName("");
      setDescription("");
      onSelectGroup(group.id);
      onSaved();
    } finally {
      setSaving(false);
    }
  };

  const removeMember = async (memberId: string) => {
    await api<void>(`/api/groups/${activeGroupId}/members/${encodeURIComponent(memberId)}`, { method: "DELETE" });
    if (selectedMemberId === memberId) setSelectedMemberId("");
    onSaved();
  };

  return (
    <section className="grid gap-5 xl:grid-cols-[0.8fr_1.2fr]">
      <Card className="overflow-hidden">
        <CardHeader>
          <CardTitle>Nhóm</CardTitle>
          <CardDescription>Chọn nhóm, tạo nhóm mới và xem riêng thu chi từng thành viên.</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {state.groups.map((group) => (
            <button key={group.id} className={`flex w-full items-center justify-between gap-3 border-t px-5 py-4 text-left ${group.id === activeGroupId ? "bg-blue-50" : "bg-white"}`} onClick={() => onSelectGroup(group.id)}>
              <div>
                <div className="font-black">{group.name}</div>
                <div className="text-sm text-muted-foreground">{group.description || "Nhóm chi tiêu"}</div>
              </div>
              <Badge variant={group.id === activeGroupId ? "default" : "secondary"}>{group.inviteCode}</Badge>
            </button>
          ))}
        </CardContent>
      </Card>

      <section className="grid gap-5">
        <Card>
          <CardHeader>
            <CardTitle>Tạo nhóm mới</CardTitle>
            <CardDescription>Chủ động tạo thêm nhóm cho nhà, chuyến đi, dự án hoặc quỹ riêng.</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-3 md:grid-cols-[1fr_1fr_auto]">
            <Input placeholder="Tên nhóm" value={name} onChange={(event) => setName(event.target.value)} />
            <Input placeholder="Mô tả" value={description} onChange={(event) => setDescription(event.target.value)} />
            <Button onClick={createGroup} disabled={saving || !name.trim()}>
              <Users className="h-4 w-4" />
              Tạo nhóm
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>{state.group?.name}</CardTitle>
            <CardDescription>Tháng {month} · mã mời {state.group?.inviteCode}</CardDescription>
          </CardHeader>
          <CardContent className="grid gap-4 md:grid-cols-[0.85fr_1.15fr]">
            <div className="grid gap-2">
              {state.members.map((member) => (
                <button key={member.id} className={`flex items-center justify-between rounded-md border px-3 py-2 text-left ${selectedMember?.id === member.id ? "border-blue-500 bg-blue-50" : "bg-white"}`} onClick={() => setSelectedMemberId(member.id)}>
                  <span className="flex items-center gap-3">
                    <span className="grid h-9 w-9 place-items-center rounded-full bg-blue-50 font-black text-blue-600">{member.name?.[0] ?? "U"}</span>
                    <span>
                      <span className="block font-bold">{member.name || member.email}</span>
                      <span className="text-xs text-muted-foreground">{member.role === "owner" ? "Chủ nhóm" : "Thành viên"}</span>
                    </span>
                  </span>
                  <span className="flex items-center gap-2">
                    <strong>{money(member.spent)}</strong>
                    {canRemoveMembers && member.role !== "owner" && member.id !== state.currentUserId && (
                      <span
                        role="button"
                        tabIndex={0}
                        className="rounded p-1 text-red-600 hover:bg-red-50"
                        title="Xoa thanh vien"
                        onClick={(event) => {
                          event.stopPropagation();
                          void removeMember(member.id);
                        }}
                      >
                        <Trash2 className="h-4 w-4" />
                      </span>
                    )}
                  </span>
                </button>
              ))}
            </div>
            <div className="rounded-md border bg-slate-50 p-4">
              <div className="flex items-center justify-between gap-3">
                <div>
                  <div className="font-black">{selectedMember?.name || selectedMember?.email || "Thành viên"}</div>
                  <div className="text-sm text-muted-foreground">Thu {money(memberIncome)} · Chi {money(memberExpense)}</div>
                </div>
                <Badge variant={memberExpense > memberIncome ? "danger" : "success"}>{money(memberIncome - memberExpense)}</Badge>
              </div>
              <div className="mt-4 divide-y rounded-md border bg-white">
                {memberTransactions.length === 0 ? <EmptyRows text="Chưa có giao dịch của thành viên này." /> : null}
                {memberTransactions.slice(0, 8).map((tx) => (
                  <div key={tx.id} className="flex items-center justify-between gap-3 px-3 py-3">
                    <div className="min-w-0">
                      <div className="truncate font-bold">{tx.note || labelOf(tx.category)}</div>
                      <div className="text-xs text-muted-foreground">{labelOf(tx.category)} · {new Date(tx.date).toLocaleDateString("vi-VN")}</div>
                    </div>
                    <strong className={tx.type === "income" ? "text-emerald-600" : "text-red-600"}>{tx.type === "income" ? "+" : "-"}{money(tx.amount)}</strong>
                  </div>
                ))}
              </div>
            </div>
          </CardContent>
        </Card>
      </section>
    </section>
  );
}

function TasksView({ tasks, setTasks, view }: { tasks: TaskItem[]; setTasks: Dispatch<SetStateAction<TaskItem[]>>; view: ViewId }) {
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [period, setPeriod] = useState<TaskItem["period"]>("morning");
  const [cadence, setCadence] = useState<TaskItem["cadence"]>("daily");
  const [priority, setPriority] = useState<TaskItem["priority"]>("normal");
  const [project, setProject] = useState("");
  const [time, setTime] = useState("");
  const [date, setDate] = useState(dateKey());

  const periods: Array<{ id: TaskItem["period"]; label: string }> = [
    { id: "morning", label: "Sáng" },
    { id: "afternoon", label: "Chiều" },
    { id: "evening", label: "Tối" },
  ];
  const today = dateKey();

  const addTask = () => {
    const cleanTitle = title.trim();
    if (!cleanTitle) return;
    setTasks((current) => [
      ...current,
      { id: crypto.randomUUID(), title: cleanTitle, description, period, cadence, priority, project, date, time, done: false },
    ]);
    setTitle("");
    setDescription("");
    setProject("");
    setTime("");
  };

  const toggleDone = (id: string) => {
    setTasks((current) => current.map((task) => (task.id === id ? { ...task, done: !task.done } : task)));
  };

  const reschedule = (id: string) => {
    setTasks((current) => current.map((task) => (task.id === id ? { ...task, date: today, done: false } : task)));
  };

  const removeTask = (id: string) => {
    setTasks((current) => current.filter((task) => task.id !== id));
  };

  const sorted = [...tasks].sort((a, b) => `${a.date}-${a.period}`.localeCompare(`${b.date}-${b.period}`));
  const lateCount = tasks.filter((task) => !task.done && task.date < today).length;

  return (
    <section className="grid gap-5 lg:grid-cols-[0.85fr_1.15fr]">
      <Card>
        <CardHeader>
          <CardTitle>Lịch task</CardTitle>
          <CardDescription>Tổ chức việc sáng, chiều, tối; task trễ có thể dời lại lịch để hoàn thành.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <Input placeholder="Tên công việc hoặc sự kiện" value={title} onChange={(event) => setTitle(event.target.value)} />
          <Input placeholder="Mô tả ngắn" value={description} onChange={(event) => setDescription(event.target.value)} />
          <div className="grid grid-cols-2 gap-2">
            <Input placeholder="Dự án / nhóm việc" value={project} onChange={(event) => setProject(event.target.value)} />
            <Input type="time" value={time} onChange={(event) => setTime(event.target.value)} />
          </div>
          <div className="grid grid-cols-3 gap-2">
            {periods.map((item) => (
              <button
                key={item.id}
                className={`rounded-md border px-3 py-2 text-sm font-bold ${period === item.id ? "border-blue-500 bg-blue-50 text-blue-700" : "bg-white"}`}
                onClick={() => setPeriod(item.id)}
              >
                {item.label}
              </button>
            ))}
          </div>
          <select className="input-like" value={cadence} onChange={(event) => setCadence(event.target.value as TaskItem["cadence"])}>
            <option value="daily">Hàng ngày</option>
            <option value="weekly">Hàng tuần</option>
            <option value="monthly">Hàng tháng</option>
            <option value="event">Sự kiện</option>
          </select>
          <Input type="date" value={date} onChange={(event) => setDate(event.target.value)} />
          <Button onClick={addTask}>
            <CalendarDays className="h-4 w-4" />
            Thêm task
          </Button>
        </CardContent>
      </Card>

      <Card className="overflow-hidden">
        <CardHeader className="flex-row items-center justify-between gap-3">
          <div>
            <CardTitle>Hôm nay và sắp tới</CardTitle>
            <CardDescription>{lateCount > 0 ? `${lateCount} task đang trễ cần xếp lại.` : "Không có task trễ."}</CardDescription>
          </div>
          <Badge variant={lateCount > 0 ? "danger" : "success"}>{tasks.filter((task) => task.done).length}/{tasks.length} done</Badge>
        </CardHeader>
        <CardContent className="p-0">
          {sorted.length === 0 ? <EmptyRows text="Chưa có task nào." /> : null}
          {periods.map((slot) => {
            const rows = sorted.filter((task) => task.period === slot.id);
            if (rows.length === 0) return null;
            return (
              <div key={slot.id} className="border-t">
                <div className="bg-slate-50 px-5 py-2 text-xs font-black uppercase tracking-wide text-slate-500">{slot.label}</div>
                {rows.map((task) => {
                  const isLate = !task.done && task.date < today;
                  return (
                    <div key={task.id} className="flex flex-wrap items-center justify-between gap-3 px-5 py-4">
                      <button className="flex min-w-0 flex-1 items-center gap-3 text-left" onClick={() => toggleDone(task.id)}>
                        <span className={`grid h-6 w-6 shrink-0 place-items-center rounded-full border ${task.done ? "border-emerald-500 bg-emerald-50 text-emerald-600" : "bg-white"}`}>
                          {task.done ? <Check className="h-4 w-4" /> : null}
                        </span>
                        <span className="min-w-0">
                          <span className={`block truncate font-bold ${task.done ? "text-slate-400 line-through" : ""}`}>{task.title}</span>
                          <span className="text-sm text-muted-foreground">{new Date(task.date).toLocaleDateString("vi-VN")} · {task.cadence}</span>
                        </span>
                      </button>
                      <div className="flex items-center gap-2">
                        {isLate && (
                          <Button size="sm" variant="outline" onClick={() => reschedule(task.id)}>
                            Xếp lại
                          </Button>
                        )}
                        <Button size="icon" variant="ghost" onClick={() => removeTask(task.id)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      </div>
                    </div>
                  );
                })}
              </div>
            );
          })}
        </CardContent>
      </Card>
    </section>
  );
}

function LocatorView() {
  const [location, setLocation] = useState<{ latitude: number; longitude: number; accuracy: number } | null>(null);
  const [status, setStatus] = useState("Bam lay vi tri de tao link chia se.");
  const [locating, setLocating] = useState(false);
  const mapUrl = location
    ? `https://www.google.com/maps/search/?api=1&query=${location.latitude},${location.longitude}`
    : "";

  const locate = () => {
    if (!navigator.geolocation) {
      setStatus("Trinh duyet khong ho tro GPS.");
      return;
    }
    setLocating(true);
    setStatus("Dang lay vi tri...");
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLocation({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracy: position.coords.accuracy,
        });
        setStatus("Da lay vi tri hien tai.");
        setLocating(false);
      },
      (error) => {
        setStatus(error.message || "Khong lay duoc vi tri.");
        setLocating(false);
      },
      { enableHighAccuracy: true, timeout: 15000, maximumAge: 15000 },
    );
  };

  const copy = async () => {
    if (!mapUrl) return;
    await navigator.clipboard?.writeText(mapUrl);
    setStatus("Da copy link Google Maps.");
  };

  return (
    <section className="grid gap-5 lg:grid-cols-[0.85fr_1.15fr]">
      <Card>
        <CardHeader>
          <CardTitle>Dinh vi nhanh</CardTitle>
          <CardDescription>Ban free: lay GPS hien tai, mo ban do va copy link chia se.</CardDescription>
        </CardHeader>
        <CardContent className="grid gap-3">
          <Button onClick={locate} disabled={locating}>
            <Navigation className="h-4 w-4" />
            {locating ? "Dang lay vi tri..." : "Lay vi tri"}
          </Button>
          {mapUrl && (
            <div className="grid gap-2">
              <Button asChild variant="outline">
                <a href={mapUrl} target="_blank" rel="noreferrer">
                  <MapPin className="h-4 w-4" />
                  Mo Google Maps
                </a>
              </Button>
              <Button variant="outline" onClick={copy}>Copy link</Button>
            </div>
          )}
          <div className="rounded-md border bg-slate-50 p-3 text-sm text-slate-600">{status}</div>
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Toa do</CardTitle>
          <CardDescription>Du lieu chi nam tren may ban va link ban chia se.</CardDescription>
        </CardHeader>
        <CardContent>
          {location ? (
            <div className="grid gap-2 text-sm">
              <div><strong>Lat:</strong> {location.latitude.toFixed(6)}</div>
              <div><strong>Lng:</strong> {location.longitude.toFixed(6)}</div>
              <div><strong>Sai so:</strong> ~{Math.round(location.accuracy)}m</div>
            </div>
          ) : (
            <EmptyRows text="Chua co vi tri." />
          )}
        </CardContent>
      </Card>
    </section>
  );
}

function SettingsView() {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Cài đặt</CardTitle>
        <CardDescription>Các cấu hình tài khoản và kết nối email vẫn dùng trang cũ.</CardDescription>
      </CardHeader>
      <CardContent>
        <Button asChild variant="outline">
          <a href="/settings">Mở cài đặt đầy đủ</a>
        </Button>
      </CardContent>
    </Card>
  );
}

function RecentTransactionsCard({ transactions, onDeleted }: { transactions: Transaction[]; onDeleted: () => void }) {
  return (
    <Card className="overflow-hidden">
      <CardHeader className="flex-row items-center justify-between gap-4">
        <div>
          <CardTitle>Giao dịch gần đây</CardTitle>
          <CardDescription>Các khoản mới nhất trong tháng.</CardDescription>
        </div>
        <Badge variant="default">{transactions.length}</Badge>
      </CardHeader>
      <CardContent className="p-0">
        <TransactionRows transactions={transactions} onDeleted={onDeleted} />
      </CardContent>
    </Card>
  );
}

function TransactionRows({ transactions, onDeleted }: { transactions: Transaction[]; onDeleted: () => void }) {
  const remove = async (id: number) => {
    await api<void>(`/api/transactions/${id}`, { method: "DELETE" });
    onDeleted();
  };

  if (transactions.length === 0) return <EmptyRows text="Chưa có giao dịch phù hợp." />;
  return (
    <div className="divide-y">
      {transactions.map((tx) => (
        <div key={tx.id} className="flex items-start gap-3 px-4 py-4 transition hover:bg-slate-50 sm:gap-4 sm:px-5">
          <div className="grid h-11 w-11 shrink-0 place-items-center rounded-md bg-slate-100 text-slate-700">
            {tx.type === "income" ? <BadgeDollarSign className="h-5 w-5" /> : <CreditCard className="h-5 w-5" />}
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <div className="min-w-0 truncate font-bold">{tx.note || labelOf(tx.category)}</div>
              {tx.isShared && <Badge variant="default">Chung</Badge>}
              {tx.fromEmail && <Badge variant="secondary">VCB</Badge>}
            </div>
            <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
              <span>{labelOf(tx.category)}</span>
              <span>·</span>
              <span>{new Date(tx.date).toLocaleDateString("vi-VN")}</span>
              {tx.latitude && tx.longitude ? (
                <a className="inline-flex items-center gap-1 text-emerald-700" href={`https://www.google.com/maps/search/?api=1&query=${tx.latitude},${tx.longitude}`} target="_blank" rel="noreferrer">
                  <MapPin className="h-3 w-3" />
                  {tx.locationName || "Check-in"}
                </a>
              ) : null}
            </div>
          </div>
          <div className={tx.type === "income" ? "shrink-0 text-sm font-black text-emerald-600 sm:text-base" : "shrink-0 text-sm font-black text-red-600 sm:text-base"}>
            {tx.type === "income" ? "+" : "-"}{money(tx.amount)}
          </div>
          <Button variant="ghost" size="icon" onClick={() => remove(tx.id)} title="Xóa giao dịch">
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      ))}
    </div>
  );
}

function BudgetList({ budgets, onDelete }: { budgets: Budget[]; onDelete?: (id: number) => void }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Ngân sách</CardTitle>
        <CardDescription>Theo dõi mức đã dùng theo danh mục.</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {budgets.length === 0 ? <div className="text-sm text-muted-foreground">Chưa có ngân sách tháng này.</div> : null}
        {budgets.map((budget) => {
          const pct = budget.amount > 0 ? Math.min((budget.spent / budget.amount) * 100, 100) : 0;
          return (
            <div key={budget.id}>
              <div className="mb-2 flex items-center justify-between gap-3 text-sm">
                <span className="font-semibold">{labelOf(budget.categoryId)}</span>
                <span className="ml-auto text-muted-foreground">{money(budget.spent)} / {money(budget.amount)}</span>
                {onDelete && (
                  <button className="text-red-600" onClick={() => onDelete(budget.id)} title="Xóa ngân sách">
                    <Trash2 className="h-4 w-4" />
                  </button>
                )}
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                <div className={`h-full rounded-full ${pct >= 90 ? "bg-red-500" : pct >= 75 ? "bg-amber-500" : "bg-blue-500"}`} style={{ width: `${pct}%` }} />
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}

function CheckInCard() {
  const [status, setStatus] = useState("Sẵn sàng lấy vị trí");
  const locate = () => {
    if (!navigator.geolocation) return setStatus("Trình duyệt không hỗ trợ GPS.");
    setStatus("Đang lấy vị trí...");
    navigator.geolocation.getCurrentPosition(
      (position) => setStatus(`${position.coords.latitude.toFixed(6)}, ${position.coords.longitude.toFixed(6)} · sai số ~${Math.round(position.coords.accuracy)}m`),
      (error) => setStatus(error.message),
      { enableHighAccuracy: true, timeout: 12000 },
    );
  };
  return (
    <Card className="bg-slate-950 text-white">
      <CardHeader>
        <CardTitle>Check-in chi tiêu</CardTitle>
        <CardDescription className="text-slate-400">Kiểm tra quyền GPS trước khi lưu giao dịch.</CardDescription>
      </CardHeader>
      <CardContent>
        <Button className="w-full bg-white text-slate-950 hover:bg-slate-100" onClick={locate}>
          <LocateFixed className="h-4 w-4" />
          Thử lấy vị trí
        </Button>
        <div className="mt-3 text-sm text-slate-300">{status}</div>
      </CardContent>
    </Card>
  );
}

function QuickAddDialog({ onSaved, disabled, members, groupId }: { onSaved: () => void; disabled?: boolean; members: Member[]; groupId?: number }) {
  const [open, setOpen] = useState(false);
  const [type, setType] = useState<"expense" | "income">("expense");
  const [amount, setAmount] = useState("");
  const [category, setCategory] = useState("food");
  const [note, setNote] = useState("");
  const [date, setDate] = useState(dateKey());
  const [isShared, setIsShared] = useState(false);
  const [fundAction, setFundAction] = useState("");
  const [createSplit, setCreateSplit] = useState(false);
  const [splitParticipantIds, setSplitParticipantIds] = useState<string[]>(members.map((member) => member.id));
  const [locationName, setLocationName] = useState("");
  const [location, setLocation] = useState<{ latitude: number; longitude: number; accuracy: number } | null>(null);
  const [locating, setLocating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const categories = type === "expense" ? expenseCategories : incomeCategories;

  useEffect(() => {
    setSplitParticipantIds(members.map((member) => member.id));
  }, [members]);

  useEffect(() => {
    if (open && !location && !locating) captureLocation();
  }, [open]);

  const switchType = (next: "expense" | "income") => {
    setType(next);
    setCategory(next === "expense" ? "food" : "salary");
    if (next === "income") {
      setIsShared(false);
      setFundAction("");
      setCreateSplit(false);
    }
  };

  const toggleSplitMember = (id: string) => {
    setSplitParticipantIds((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]));
  };

  const classify = (value: string) => {
    setNote(value);
    const lower = value.toLowerCase();
    if (type !== "expense") return;
    if (/(cơm|com|ăn|an|bún|bun|phở|pho|cafe|cà phê)/.test(lower)) setCategory("food");
    else if (/(grab|xăng|xang|xe|taxi)/.test(lower)) setCategory("transport");
    else if (/(nhà|nha|thuê|thue)/.test(lower)) setCategory("rent");
    else if (/(điện|dien|nước|nuoc)/.test(lower)) setCategory("utilities");
    else if (/(siêu thị|sieu thi|chợ|cho)/.test(lower)) setCategory("supermarket");
  };

  const captureLocation = () => {
    setError("");
    if (!navigator.geolocation) {
      setError("Trình duyệt không hỗ trợ lấy vị trí.");
      return;
    }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLocation({
          latitude: position.coords.latitude,
          longitude: position.coords.longitude,
          accuracy: position.coords.accuracy,
        });
        setLocating(false);
      },
      (err) => {
        setError(err.message || "Không lấy được vị trí.");
        setLocating(false);
      },
      { enableHighAccuracy: true, timeout: 12000, maximumAge: 30000 },
    );
  };

  const save = async () => {
    setError("");
    const value = parseAmount(amount);
    if (value <= 0) return setError("Số tiền chưa hợp lệ.");
    setSaving(true);
    try {
      await api<void>("/api/transactions", {
        method: "POST",
        body: JSON.stringify({
          groupId,
          type,
          amount: value,
          category,
          note,
          isShared,
          fundAction: isShared ? fundAction || null : null,
          splitParticipantIds: isShared && createSplit ? splitParticipantIds : null,
          date,
          latitude: location?.latitude,
          longitude: location?.longitude,
          locationAccuracy: location?.accuracy,
          locationName,
        }),
      });
      setOpen(false);
      setAmount("");
      setNote("");
      setIsShared(false);
      setFundAction("");
      setCreateSplit(false);
      setLocation(null);
      setLocationName("");
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không lưu được giao dịch.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button disabled={disabled}>
          <Plus className="h-4 w-4" />
          <span className="hidden sm:inline">Thêm giao dịch</span>
        </Button>
      </DialogTrigger>
      <DialogContent className="gap-3 p-4 sm:max-h-[88vh] sm:p-5">
        <DialogHeader>
          <DialogTitle>Thêm giao dịch</DialogTitle>
          <DialogDescription>Lưu khoản thu chi, phân loại và vị trí check-in.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid grid-cols-2 rounded-md bg-slate-100 p-1">
            <button className={`rounded px-3 py-2 text-sm font-bold ${type === "expense" ? "bg-white shadow-sm" : ""}`} onClick={() => switchType("expense")}>Chi tiêu</button>
            <button className={`rounded px-3 py-2 text-sm font-bold ${type === "income" ? "bg-white shadow-sm" : ""}`} onClick={() => switchType("income")}>Thu nhập</button>
          </div>
          <Input placeholder="500000, 500k, 1.2m" value={amount} onChange={(event) => setAmount(formatAmountInput(event.target.value))} inputMode="decimal" />
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-3">
            {categories.map((cat) => (
              <button
                key={cat.id}
                className={`rounded-md border px-3 py-2 text-sm font-semibold ${category === cat.id ? "border-blue-500 bg-blue-50 text-blue-700" : "bg-white"}`}
                onClick={() => setCategory(cat.id)}
              >
                {cat.label}
              </button>
            ))}
          </div>
          <Input placeholder="Ghi chú" value={note} onChange={(event) => classify(event.target.value)} />
          <Input type="date" value={date} onChange={(event) => setDate(event.target.value)} />
          <div className="rounded-md border bg-slate-50 p-3">
            <div className="flex items-center justify-between gap-3">
              <div>
                <div className="text-sm font-bold">Vị trí check-in</div>
                <div className="text-xs text-muted-foreground">
                  {location ? `${location.latitude.toFixed(6)}, ${location.longitude.toFixed(6)} · ~${Math.round(location.accuracy)}m` : "Chưa lấy vị trí"}
                </div>
              </div>
              <Button variant="outline" size="sm" onClick={captureLocation} disabled={locating}>
                <MapPin className="h-4 w-4" />
                {locating ? "Đang lấy" : "Lấy vị trí"}
              </Button>
            </div>
            <Input className="mt-3" placeholder="Tên nơi" value={locationName} onChange={(event) => setLocationName(event.target.value)} />
          </div>
          {type === "expense" && (
            <div className="grid gap-3 rounded-md border bg-white p-3">
              <label className="flex items-center gap-3 text-sm font-semibold">
                <input type="checkbox" checked={isShared} onChange={(event) => setIsShared(event.target.checked)} />
                Chi phí chung
              </label>
              {isShared && (
                <>
                  <select className="input-like" value={fundAction} onChange={(event) => setFundAction(event.target.value)}>
                    <option value="">Chỉ đánh dấu chung</option>
                    <option value="withdraw">Trừ quỹ chung</option>
                    <option value="deposit">Ghi nhận nạp quỹ</option>
                  </select>
                  <label className="flex items-center gap-3 text-sm font-semibold">
                    <input type="checkbox" checked={createSplit} onChange={(event) => setCreateSplit(event.target.checked)} />
                    Tạo chia tiền/công nợ
                  </label>
                  {createSplit && (
                    <div className="grid max-h-36 gap-2 overflow-y-auto pr-1">
                      {members.map((member) => (
                        <label key={member.id} className="flex items-center justify-between rounded-md border bg-slate-50 px-3 py-2 text-sm font-semibold">
                          <span>{member.name || member.email}</span>
                          <input type="checkbox" checked={splitParticipantIds.includes(member.id)} onChange={() => toggleSplitMember(member.id)} />
                        </label>
                      ))}
                    </div>
                  )}
                </>
              )}
            </div>
          )}
          {error && <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm font-medium text-red-700">{error}</div>}
          <Button className="w-full" onClick={save} disabled={saving}>
            <ReceiptText className="h-4 w-4" />
            {saving ? "Đang lưu..." : "Lưu giao dịch"}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function Stat({ label, value, tone }: { label: string; value: string; tone: string }) {
  return (
    <div className="metric-card">
      <div className="text-sm font-medium text-slate-500">{label}</div>
      <div className={`mt-1 text-2xl font-black ${tone}`}>{value}</div>
    </div>
  );
}

function ReportBars({ title, items, total, labeler }: { title: string; items: Record<string, number>; total: number; labeler: (id: string) => string }) {
  const rows = Object.entries(items).sort((a, b) => b[1] - a[1]);
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4">
        {rows.length === 0 ? <div className="text-sm text-muted-foreground">Chưa có dữ liệu.</div> : null}
        {rows.map(([key, value]) => {
          const pct = total > 0 ? Math.min((value / total) * 100, 100) : 0;
          return (
            <div key={key}>
              <div className="mb-2 flex items-center justify-between text-sm">
                <span className="font-semibold">{labeler(key)}</span>
                <strong>{money(value)}</strong>
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                <div className="h-full rounded-full bg-teal-500" style={{ width: `${pct}%` }} />
              </div>
            </div>
          );
        })}
      </CardContent>
    </Card>
  );
}

function EmptyGroup({ onSaved }: { onSaved: () => void }) {
  const [mode, setMode] = useState<"create" | "join">("create");
  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [inviteCode, setInviteCode] = useState("");
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const submit = async () => {
    setError("");
    setSaving(true);
    try {
      if (mode === "create") {
        await api<Group>("/api/groups", { method: "POST", body: JSON.stringify({ name, description }) });
      } else {
        await api<Group>("/api/groups/join", { method: "POST", body: JSON.stringify({ inviteCode }) });
      }
      setName("");
      setDescription("");
      setInviteCode("");
      onSaved();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Không xử lý được nhóm.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <CardContent className="mx-auto grid w-full max-w-lg gap-4 py-12">
        <Users className="h-10 w-10 text-slate-400" />
        <div>
          <div className="text-lg font-black">Bạn chưa có nhóm</div>
          <div className="text-sm text-muted-foreground">Tạo nhóm mới hoặc nhập mã mời để React tải dữ liệu thật của nhóm.</div>
        </div>
        <div className="grid grid-cols-2 rounded-md bg-slate-100 p-1">
          <button className={`rounded px-3 py-2 text-sm font-bold ${mode === "create" ? "bg-white shadow-sm" : ""}`} onClick={() => setMode("create")}>
            Tạo nhóm
          </button>
          <button className={`rounded px-3 py-2 text-sm font-bold ${mode === "join" ? "bg-white shadow-sm" : ""}`} onClick={() => setMode("join")}>
            Tham gia
          </button>
        </div>
        {mode === "create" ? (
          <>
            <Input placeholder="Tên nhóm" value={name} onChange={(event) => setName(event.target.value)} />
            <Input placeholder="Mô tả" value={description} onChange={(event) => setDescription(event.target.value)} />
          </>
        ) : (
          <Input placeholder="Mã mời" value={inviteCode} onChange={(event) => setInviteCode(event.target.value.toUpperCase())} />
        )}
        {error && <div className="rounded-md border border-red-200 bg-red-50 p-3 text-sm font-medium text-red-700">{error}</div>}
        <Button onClick={submit} disabled={saving}>
          <Users className="h-4 w-4" />
          {saving ? "Đang xử lý..." : mode === "create" ? "Tạo nhóm" : "Tham gia nhóm"}
        </Button>
      </CardContent>
    </Card>
  );
}

function EmptyRows({ text }: { text: string }) {
  return <div className="px-5 py-10 text-center text-sm text-muted-foreground">{text}</div>;
}

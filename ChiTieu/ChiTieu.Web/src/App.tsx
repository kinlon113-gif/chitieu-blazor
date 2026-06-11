import { useEffect, useMemo, useState } from "react";
import {
  ArrowDownRight,
  ArrowUpRight,
  BadgeDollarSign,
  Bell,
  CalendarDays,
  Check,
  CreditCard,
  HandCoins,
  Home,
  LocateFixed,
  MapPin,
  Menu,
  PieChart,
  Plus,
  ReceiptText,
  RefreshCw,
  Search,
  Settings,
  Split,
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

type ViewId = "overview" | "transactions" | "budget" | "fund" | "debts" | "reports" | "split" | "groups" | "settings";

type AppState = {
  group: Group | null;
  members: Member[];
  transactions: Transaction[];
  budgets: Budget[];
  debts: Debt[];
  fund: Fund | null;
  fundTransactions: FundTransaction[];
  splits: SplitItem[];
  report: Report;
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
  members: [],
  transactions: [],
  budgets: [],
  debts: [],
  fund: null,
  fundTransactions: [],
  splits: [],
  report: { income: 0, expense: 0, balance: 0, count: 0, byCategory: {}, byMember: {} },
};

const navItems: Array<{ id: ViewId; label: string; icon: typeof Home }> = [
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

function labelOf(category: string) {
  return [...expenseCategories, ...incomeCategories].find((item) => item.id === category)?.label ?? "Khác";
}

async function api<T>(url: string, init?: RequestInit): Promise<T> {
  const response = await fetch(url, {
    credentials: "include",
    headers: { "Content-Type": "application/json", ...(init?.headers ?? {}) },
    ...init,
  });
  if (response.redirected) {
    window.location.href = response.url;
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
  const [state, setState] = useState<AppState>(emptyState);
  const [month, setMonth] = useState(monthKey());
  const [search, setSearch] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState("");

  const activeLabel = useMemo(() => navItems.find((item) => item.id === activeView)?.label ?? "Tổng quan", [activeView]);

  const load = async () => {
    setLoading(true);
    setMessage("");
    try {
      setState(await api<AppState>(`/api/app?month=${month}`));
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "Không tải được dữ liệu.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [month]);

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
            {navItems.map((item) => (
              <button
                key={item.id}
                onClick={() => setActiveView(item.id)}
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
            <Button variant="ghost" size="icon" className="lg:hidden">
              <Menu className="h-5 w-5" />
            </Button>
            <div>
              <div className="text-sm text-muted-foreground">{state.group?.name ?? "Tài chính nhóm"}</div>
              <h1 className="text-xl font-black tracking-tight">{activeLabel}</h1>
            </div>
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
            <Button variant="outline" size="icon">
              <Bell className="h-4 w-4" />
            </Button>
            <QuickAddDialog onSaved={load} disabled={!state.group} />
          </div>
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
              {activeView === "budget" && <BudgetView budgets={state.budgets} month={month} setMonth={setMonth} onSaved={load} />}
              {activeView === "fund" && <FundView state={state} onSaved={load} />}
              {activeView === "debts" && <DebtsView debts={state.debts} onSaved={load} />}
              {activeView === "reports" && <ReportsView report={state.report} month={month} setMonth={setMonth} />}
              {activeView === "split" && <SplitView splits={state.splits} members={state.members} onSaved={load} />}
              {activeView === "groups" && <GroupsView group={state.group} members={state.members} />}
              {activeView === "settings" && <SettingsView />}
            </>
          )}
        </div>
      </main>

      <nav className="mobile-safe fixed bottom-0 left-0 right-0 z-40 border-t bg-white/95 px-3 py-2 backdrop-blur-xl lg:hidden">
        <div className="grid grid-cols-5 gap-1">
          {navItems.slice(0, 5).map((item) => (
            <button
              key={item.id}
              onClick={() => setActiveView(item.id)}
              className={`flex flex-col items-center gap-1 rounded-md px-1 py-2 text-[10px] font-semibold ${
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

function OverviewView({ state, onRefresh }: { state: AppState; onRefresh: () => void }) {
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

function BudgetView({ budgets, month, setMonth, onSaved }: { budgets: Budget[]; month: string; setMonth: (month: string) => void; onSaved: () => void }) {
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
      await api<void>("/api/budgets", { method: "POST", body: JSON.stringify({ categoryId, month, amount: value }) });
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
          <Input placeholder="3m, 2500000" value={amount} onChange={(event) => setAmount(event.target.value)} />
          <Button onClick={save} disabled={saving}>Lưu</Button>
          {error && <div className="text-sm font-medium text-red-600 md:col-span-4">{error}</div>}
        </CardContent>
      </Card>
      <BudgetList budgets={budgets} onDelete={remove} />
    </section>
  );
}

function FundView({ state, onSaved }: { state: AppState; onSaved: () => void }) {
  const [type, setType] = useState("deposit");
  const [amount, setAmount] = useState("");
  const [note, setNote] = useState("");

  const save = async () => {
    const value = parseAmount(amount);
    if (value <= 0) return;
    await api<void>("/api/fund-transactions", { method: "POST", body: JSON.stringify({ type, amount: value, note }) });
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
            <Input placeholder="Số tiền" value={amount} onChange={(event) => setAmount(event.target.value)} />
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

function SplitView({ splits, members, onSaved }: { splits: SplitItem[]; members: Member[]; onSaved: () => void }) {
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
        body: JSON.stringify({ totalAmount, description, participantIds }),
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
          <Input placeholder="Tổng tiền" value={amount} onChange={(event) => setAmount(event.target.value)} inputMode="decimal" />
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

function GroupsView({ group, members }: { group: Group; members: Member[] }) {
  return (
    <section className="grid gap-5 lg:grid-cols-[1fr_0.75fr]">
      <Card className="overflow-hidden">
        <CardHeader>
          <CardTitle>{group.name}</CardTitle>
          <CardDescription>{group.description || "Nhóm chi tiêu"}</CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {members.map((member) => (
            <div key={member.id} className="flex items-center justify-between border-t px-5 py-4">
              <div className="flex items-center gap-3">
                <div className="grid h-10 w-10 place-items-center rounded-full bg-blue-50 font-black text-blue-600">{member.name?.[0] ?? "U"}</div>
                <div>
                  <div className="font-bold">{member.name || member.email}</div>
                  <div className="text-sm text-muted-foreground">{member.role === "owner" ? "Chủ nhóm" : "Thành viên"}</div>
                </div>
              </div>
              <div className="font-black">{money(member.spent)}</div>
            </div>
          ))}
        </CardContent>
      </Card>
      <Card>
        <CardHeader>
          <CardTitle>Mã mời</CardTitle>
          <CardDescription>Chia sẻ mã này cho thành viên mới.</CardDescription>
        </CardHeader>
        <CardContent>
          <div className="rounded-md border bg-slate-50 p-4 text-center text-2xl font-black tracking-[0.3em] text-blue-600">{group.inviteCode}</div>
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
        <div key={tx.id} className="flex items-start gap-4 px-5 py-4 transition hover:bg-slate-50">
          <div className="grid h-11 w-11 shrink-0 place-items-center rounded-md bg-slate-100 text-slate-700">
            {tx.type === "income" ? <BadgeDollarSign className="h-5 w-5" /> : <CreditCard className="h-5 w-5" />}
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <div className="font-bold">{tx.note || labelOf(tx.category)}</div>
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
          <div className={tx.type === "income" ? "font-black text-emerald-600" : "font-black text-red-600"}>
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

function QuickAddDialog({ onSaved, disabled }: { onSaved: () => void; disabled?: boolean }) {
  const [open, setOpen] = useState(false);
  const [type, setType] = useState<"expense" | "income">("expense");
  const [amount, setAmount] = useState("");
  const [category, setCategory] = useState("food");
  const [note, setNote] = useState("");
  const [date, setDate] = useState(dateKey());
  const [isShared, setIsShared] = useState(false);
  const [locationName, setLocationName] = useState("");
  const [location, setLocation] = useState<{ latitude: number; longitude: number; accuracy: number } | null>(null);
  const [locating, setLocating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState("");

  const categories = type === "expense" ? expenseCategories : incomeCategories;

  const switchType = (next: "expense" | "income") => {
    setType(next);
    setCategory(next === "expense" ? "food" : "salary");
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
          type,
          amount: value,
          category,
          note,
          isShared,
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
      <DialogContent className="max-h-[92vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Thêm giao dịch</DialogTitle>
          <DialogDescription>Lưu khoản thu chi, phân loại và vị trí check-in.</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          <div className="grid grid-cols-2 rounded-md bg-slate-100 p-1">
            <button className={`rounded px-3 py-2 text-sm font-bold ${type === "expense" ? "bg-white shadow-sm" : ""}`} onClick={() => switchType("expense")}>Chi tiêu</button>
            <button className={`rounded px-3 py-2 text-sm font-bold ${type === "income" ? "bg-white shadow-sm" : ""}`} onClick={() => switchType("income")}>Thu nhập</button>
          </div>
          <Input placeholder="500000, 500k, 1.2m" value={amount} onChange={(event) => setAmount(event.target.value)} inputMode="decimal" />
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
            <label className="flex items-center gap-3 text-sm font-semibold">
              <input type="checkbox" checked={isShared} onChange={(event) => setIsShared(event.target.checked)} />
              Chi phí chung
            </label>
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

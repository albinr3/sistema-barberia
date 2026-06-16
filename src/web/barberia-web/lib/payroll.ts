import type { SupabaseClient } from "@supabase/supabase-js";

export type PayrollDevice = {
  id: string;
  name: string | null;
  last_sync_at: string | null;
  pending_outbox_count: number | null;
};

export type PayrollLine = {
  id: string;
  local_line_id: string;
  barber_id: string | null;
  barber_name: string;
  station_number: number | null;
  closed_services_count: number;
  sales_generated_cents: number;
  commission_cents: number;
  adjustments_cents: number;
  total_cents: number;
};

export type PayrollAdjustment = {
  id: string;
  local_adjustment_id: string;
  barber_id: string | null;
  amount_cents: number;
  reason: string;
  created_at: string;
};

export type PayrollPeriod = {
  id: string;
  local_period_id: string;
  start_date: string;
  end_date: string;
  state: "draft" | "paid";
  total_services: number;
  total_commission_cents: number;
  total_adjustments_cents: number;
  total_to_pay_cents: number;
  payment_method: string | null;
  payment_reference: string | null;
  notes: string | null;
  generated_at: string;
  paid_at: string | null;
  loaded_at: string;
  lines: PayrollLine[];
  adjustments: PayrollAdjustment[];
};

export type PayrollCommand = {
  id: string;
  command_type: "snapshot_requested" | "adjustment_added" | "pay_requested";
  status: "pending" | "applied" | "failed";
  error_message: string | null;
  created_at: string;
  applied_at: string | null;
};

export type PayrollBarber = {
  id: string;
  display_name: string | null;
  station_code: string | null;
};

export type PayrollDashboard = {
  device: PayrollDevice | null;
  range: PayrollWeekRange;
  period: PayrollPeriod | null;
  pendingCommand: PayrollCommand | null;
  lastCommand: PayrollCommand | null;
  barbers: PayrollBarber[];
  isSyncStale: boolean;
  hasPendingOutbox: boolean;
  isClosed: boolean;
  canRequestCommand: boolean;
  canPay: boolean;
  payBlockReason: string | null;
};

export type PayrollWeekRange = {
  referenceDate: string;
  startDate: string;
  endDate: string;
  label: string;
};

const STALE_SYNC_MINUTES = 15;
const TIME_ZONE = "America/New_York";

export async function getPayrollDashboard(
  supabase: SupabaseClient,
  referenceDate?: string,
): Promise<PayrollDashboard> {
  const range = getPayrollWeekRange(referenceDate);
  const [device, barbers] = await Promise.all([
    getLatestPayrollDevice(supabase),
    getPayrollBarbers(supabase),
  ]);

  if (!device) {
    return {
      device: null,
      range,
      period: null,
      pendingCommand: null,
      lastCommand: null,
      barbers,
      isSyncStale: true,
      hasPendingOutbox: false,
      isClosed: isPayrollRangeClosed(range),
      canRequestCommand: false,
      canPay: false,
      payBlockReason: "No active desktop sync device.",
    };
  }

  const [period, pendingCommand, lastCommand] = await Promise.all([
    getPayrollPeriod(supabase, device.id, range),
    getPendingPayrollCommand(supabase, device.id, range),
    getLastPayrollCommand(supabase, device.id, range),
  ]);

  const isSyncStale = isDeviceStale(device);
  const hasPendingOutbox = (device.pending_outbox_count ?? 0) > 0;
  const isClosed = isPayrollRangeClosed(range);
  const isPaid = period?.state === "paid";
  const canRequestCommand = !pendingCommand && !isPaid;
  const payBlockReason = getPayBlockReason({
    period,
    pendingCommand,
    isSyncStale,
    hasPendingOutbox,
    isClosed,
  });

  return {
    device,
    range,
    period,
    pendingCommand,
    lastCommand,
    barbers,
    isSyncStale,
    hasPendingOutbox,
    isClosed,
    canRequestCommand,
    canPay: payBlockReason === null,
    payBlockReason,
  };
}

export async function getPayrollHistory(supabase: SupabaseClient) {
  const device = await getLatestPayrollDevice(supabase);
  if (!device) {
    return { device: null, periods: [] as PayrollPeriod[] };
  }

  const { data, error } = await supabase
    .from("synced_payroll_periods")
    .select(
      `
        *,
        lines:synced_payroll_lines(*),
        adjustments:synced_payroll_adjustments(*)
      `,
    )
    .eq("source_device_id", device.id)
    .eq("state", "paid")
    .order("start_date", { ascending: false })
    .limit(25);

  if (error) throw new Error("Failed to load payroll history: " + error.message);

  return {
    device,
    periods: normalizePayrollPeriods(data ?? []),
  };
}

export function getPayrollWeekRange(referenceDate?: string): PayrollWeekRange {
  const reference = parseDateOnly(referenceDate || getTodayInTimeZone());
  const daysSinceFriday = (reference.getUTCDay() - 5 + 7) % 7;
  const start = addDays(reference, -daysSinceFriday);
  const end = addDays(start, 7);
  return {
    referenceDate: formatDateOnly(reference),
    startDate: formatDateOnly(start),
    endDate: formatDateOnly(end),
    label: `${formatDisplayDate(start)} - ${formatDisplayDate(addDays(end, -1))}`,
  };
}

export function formatCurrency(cents: number | null | undefined) {
  return new Intl.NumberFormat("en-US", { style: "currency", currency: "USD" }).format((cents ?? 0) / 100);
}

export function formatDate(isoOrDate: string | null | undefined) {
  if (!isoOrDate) return "-";
  const date = isoOrDate.includes("T") ? new Date(isoOrDate) : parseDateOnly(isoOrDate);
  return new Intl.DateTimeFormat("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    timeZone: isoOrDate.includes("T") ? undefined : "UTC",
  }).format(date);
}

export function payrollReference(period: PayrollPeriod | null, range: PayrollWeekRange) {
  if (!period) return "N/A";
  if (period.payment_reference) return period.payment_reference;
  const compactDate = range.startDate.slice(2).replaceAll("-", "");
  return `NOM-${compactDate}-${period.local_period_id.slice(0, 4).toUpperCase()}`;
}

function getPayBlockReason(input: {
  period: PayrollPeriod | null;
  pendingCommand: PayrollCommand | null;
  isSyncStale: boolean;
  hasPendingOutbox: boolean;
  isClosed: boolean;
}) {
  if (!input.period) return "Request a desktop recalculation first.";
  if (input.period.state === "paid") return "This payroll period is already paid.";
  if (input.pendingCommand) return "A payroll command is pending sync.";
  if (input.isSyncStale) return "Desktop sync is stale.";
  if (input.hasPendingOutbox) return "Desktop has pending sync events.";
  if (!input.isClosed) return "Payroll period has not closed yet.";
  return null;
}

async function getLatestPayrollDevice(supabase: SupabaseClient): Promise<PayrollDevice | null> {
  const { data, error } = await supabase
    .from("sync_devices")
    .select("id, name, last_sync_at, pending_outbox_count")
    .eq("is_active", true)
    .order("last_sync_at", { ascending: false, nullsFirst: false })
    .limit(1)
    .maybeSingle();

  if (error) throw new Error("Failed to load sync device: " + error.message);
  return data as PayrollDevice | null;
}

async function getPayrollBarbers(supabase: SupabaseClient): Promise<PayrollBarber[]> {
  const { data, error } = await supabase
    .from("barbers")
    .select("id, display_name, station_code")
    .eq("is_active", true)
    .order("display_name");

  if (error) throw new Error("Failed to load barbers: " + error.message);
  return (data ?? []) as PayrollBarber[];
}

async function getPayrollPeriod(
  supabase: SupabaseClient,
  sourceDeviceId: string,
  range: PayrollWeekRange,
): Promise<PayrollPeriod | null> {
  const { data, error } = await supabase
    .from("synced_payroll_periods")
    .select(
      `
        *,
        lines:synced_payroll_lines(*),
        adjustments:synced_payroll_adjustments(*)
      `,
    )
    .eq("source_device_id", sourceDeviceId)
    .eq("start_date", range.startDate)
    .eq("end_date", range.endDate)
    .maybeSingle();

  if (error) throw new Error("Failed to load payroll period: " + error.message);
  return data ? normalizePayrollPeriods([data])[0] : null;
}

async function getPendingPayrollCommand(
  supabase: SupabaseClient,
  sourceDeviceId: string,
  range: PayrollWeekRange,
): Promise<PayrollCommand | null> {
  const { data, error } = await supabase
    .from("payroll_admin_commands")
    .select("id, command_type, status, error_message, created_at, applied_at")
    .eq("source_device_id", sourceDeviceId)
    .eq("start_date", range.startDate)
    .eq("end_date", range.endDate)
    .eq("status", "pending")
    .order("created_at", { ascending: true })
    .limit(1)
    .maybeSingle();

  if (error) throw new Error("Failed to load pending payroll command: " + error.message);
  return data as PayrollCommand | null;
}

async function getLastPayrollCommand(
  supabase: SupabaseClient,
  sourceDeviceId: string,
  range: PayrollWeekRange,
): Promise<PayrollCommand | null> {
  const { data, error } = await supabase
    .from("payroll_admin_commands")
    .select("id, command_type, status, error_message, created_at, applied_at")
    .eq("source_device_id", sourceDeviceId)
    .eq("start_date", range.startDate)
    .eq("end_date", range.endDate)
    .order("created_at", { ascending: false })
    .limit(1)
    .maybeSingle();

  if (error) throw new Error("Failed to load last payroll command: " + error.message);
  return data as PayrollCommand | null;
}

function normalizePayrollPeriods(rows: unknown[]): PayrollPeriod[] {
  return rows.map((row) => {
    const typed = row as Record<string, unknown>;
    return {
      ...typed,
      lines: normalizeArray<PayrollLine>(typed.lines).sort((left, right) =>
        left.barber_name.localeCompare(right.barber_name),
      ),
      adjustments: normalizeArray<PayrollAdjustment>(typed.adjustments),
    } as PayrollPeriod;
  });
}

function normalizeArray<T>(value: unknown) {
  if (!value) return [] as T[];
  return (Array.isArray(value) ? value : [value]) as T[];
}

function isDeviceStale(device: PayrollDevice) {
  if (!device.last_sync_at) return true;
  return Date.now() - Date.parse(device.last_sync_at) > STALE_SYNC_MINUTES * 60 * 1000;
}

function isPayrollRangeClosed(range: PayrollWeekRange) {
  return range.endDate <= getTodayInTimeZone();
}

function getTodayInTimeZone() {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const year = parts.find((part) => part.type === "year")?.value;
  const month = parts.find((part) => part.type === "month")?.value;
  const day = parts.find((part) => part.type === "day")?.value;
  return `${year}-${month}-${day}`;
}

function parseDateOnly(value: string) {
  return new Date(`${value}T00:00:00.000Z`);
}

function addDays(date: Date, days: number) {
  const copy = new Date(date.getTime());
  copy.setUTCDate(copy.getUTCDate() + days);
  return copy;
}

function formatDateOnly(date: Date) {
  return date.toISOString().slice(0, 10);
}

function formatDisplayDate(date: Date) {
  return new Intl.DateTimeFormat("en-US", { month: "short", day: "numeric", year: "numeric", timeZone: "UTC" }).format(date);
}

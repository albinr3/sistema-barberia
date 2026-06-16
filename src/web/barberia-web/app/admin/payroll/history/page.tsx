import { AppShell } from "@/components/layout/app-shell";
import { requireAdmin } from "@/lib/auth/profile";
import { getPayrollHistory } from "@/lib/payroll";
import { createClient } from "@/lib/supabase/server";
import { PayrollHistoryClient } from "./payroll-history-client";

export const dynamic = "force-dynamic";

export default async function AdminPayrollHistoryPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { periods } = await getPayrollHistory(supabase);

  return (
    <AppShell title="Payroll History" variant="admin">
      <PayrollHistoryClient periods={periods} />
    </AppShell>
  );
}

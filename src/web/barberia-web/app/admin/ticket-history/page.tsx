import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { AppShell } from "@/components/layout/app-shell";
import { getTicketHistory } from "@/lib/ticket-history";
import { TicketHistoryClient } from "./ticket-history-client";

export const dynamic = "force-dynamic";

export default async function AdminTicketHistoryPage({
  searchParams,
}: {
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const resolvedSearchParams = await searchParams;

  const search = typeof resolvedSearchParams.search === "string" ? resolvedSearchParams.search : undefined;
  const startDate = typeof resolvedSearchParams.startDate === "string" ? resolvedSearchParams.startDate : undefined;
  const endDate = typeof resolvedSearchParams.endDate === "string" ? resolvedSearchParams.endDate : undefined;
  const barberId = typeof resolvedSearchParams.barberId === "string" ? resolvedSearchParams.barberId : undefined;
  const status = typeof resolvedSearchParams.status === "string" ? resolvedSearchParams.status : undefined;
  const page = typeof resolvedSearchParams.page === "string" ? parseInt(resolvedSearchParams.page, 10) : 1;

  const initialData = await getTicketHistory(supabase, {
    search,
    startDate,
    endDate,
    barberId,
    status,
    page: isNaN(page) ? 1 : page,
  });

  return (
    <AppShell title="Ticket History" variant="admin">
      <TicketHistoryClient initialData={initialData} />
    </AppShell>
  );
}

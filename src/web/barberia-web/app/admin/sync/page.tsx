import { AppShell } from "@/components/layout/app-shell";
import { requireAdmin } from "@/lib/auth/profile";
import { createClient } from "@/lib/supabase/server";
import { StatusBadge } from "@/components/ui/status-badge";
import { saveDesktopCatalogMapping } from "./actions";

export default async function AdminSyncPage() {
  const supabase = await createClient();
  await requireAdmin(supabase);

  const { data: events } = await supabase
    .from("sync_events")
    .select("*")
    .order("received_at", { ascending: false })
    .limit(10);

  const { data: conflicts } = await supabase
    .from("sync_conflicts")
    .select("*")
    .eq("status", "open")
    .order("created_at", { ascending: false });

  const [{ data: localCatalogItems }, { data: mappings }, { data: barbers }, { data: services }] = await Promise.all([
    supabase
      .from("synced_catalog_items")
      .select("*")
      .order("entity_type", { ascending: true })
      .order("display_name", { ascending: true }),
    supabase.from("desktop_catalog_mappings").select("*"),
    supabase.from("barbers").select("id, display_name, station_code").order("display_name", { ascending: true }),
    supabase.from("services").select("id, name, base_price_cents").order("sort_order", { ascending: true }).order("name", { ascending: true }),
  ]);

  const mappedKeys = new Set((mappings || []).map((mapping) => `${mapping.entity_type}:${mapping.local_id}`));
  const unmappedItems = (localCatalogItems || []).filter((item) => !mappedKeys.has(`${item.entity_type}:${item.local_id}`));

  return (
    <AppShell title="Sync Dashboard" variant="admin">
      <div className="space-y-8">
        <section>
          <h2 className="text-xl font-semibold mb-4 text-slate-800 dark:text-slate-200">Catalog Mappings</h2>
          {unmappedItems.length > 0 ? (
            <div className="grid gap-4">
              {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
              {unmappedItems.map((item: any) => {
                const options = item.entity_type === "barber" ? barbers || [] : services || [];
                return (
                  <form
                    action={saveDesktopCatalogMapping}
                    className="p-4 bg-white dark:bg-slate-900 shadow rounded-lg border border-slate-200 dark:border-slate-800 grid gap-3 md:grid-cols-[1fr_1fr_auto]"
                    key={`${item.source_device_id}-${item.entity_type}-${item.local_id}`}
                  >
                    <input name="entity_type" type="hidden" value={item.entity_type} />
                    <input name="local_id" type="hidden" value={item.local_id} />
                    <div>
                      <p className="font-medium text-slate-900 dark:text-slate-100">{item.display_name}</p>
                      <p className="text-xs text-slate-500">
                        {item.entity_type} · {item.station_code || item.local_id}
                      </p>
                    </div>
                    <select
                      className="border border-slate-300 dark:border-slate-700 rounded-md px-3 py-2 bg-white dark:bg-slate-950"
                      name="cloud_id"
                      required
                    >
                      <option value="">Select cloud {item.entity_type}</option>
                      {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
                      {options.map((option: any) => (
                        <option key={option.id} value={option.id}>
                          {item.entity_type === "barber"
                            ? `${option.display_name}${option.station_code ? ` (${option.station_code})` : ""}`
                            : `${option.name} - $${(option.base_price_cents / 100).toFixed(2)}`}
                        </option>
                      ))}
                    </select>
                    <button className="px-4 py-2 rounded-md bg-[#0020c2] text-white font-semibold" type="submit">
                      Map
                    </button>
                  </form>
                );
              })}
            </div>
          ) : (
            <p className="text-sm text-slate-500">No unmapped desktop catalog items.</p>
          )}
        </section>

        <section>
          <h2 className="text-xl font-semibold mb-4 text-slate-800 dark:text-slate-200">Open Conflicts</h2>
          {conflicts && conflicts.length > 0 ? (
            <div className="grid gap-4">
              {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
              {conflicts.map((conflict: any) => (
                <div key={conflict.id} className="p-4 bg-white dark:bg-slate-900 shadow rounded-lg border-l-4 border-red-500">
                  <div className="flex justify-between">
                    <div>
                      <p className="font-medium">Type: {conflict.conflict_type}</p>
                      <p className="text-sm text-slate-500">Aggregate: {conflict.aggregate_type} ({conflict.aggregate_id})</p>
                    </div>
                    <StatusBadge tone="danger">Cancelled</StatusBadge>
                  </div>
                  <pre className="mt-2 text-xs bg-slate-100 dark:bg-slate-800 p-2 rounded overflow-auto">
                    {JSON.stringify(conflict.local_payload, null, 2)}
                  </pre>
                </div>
              ))}
            </div>
          ) : (
            <p className="text-sm text-slate-500">No open conflicts detected.</p>
          )}
        </section>

        <section>
          <h2 className="text-xl font-semibold mb-4 text-slate-800 dark:text-slate-200">Recent Sync Events</h2>
          <div className="overflow-x-auto bg-white dark:bg-slate-900 shadow rounded-lg">
            <table className="w-full text-sm text-left">
              <thead className="bg-slate-50 dark:bg-slate-800">
                <tr>
                  <th className="p-3">Time</th>
                  <th className="p-3">Source</th>
                  <th className="p-3">Event Type</th>
                  <th className="p-3">Status</th>
                  <th className="p-3">Device ID</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-slate-800">
                {/* eslint-disable-next-line @typescript-eslint/no-explicit-any */}
                {events?.map((event: any) => (
                  <tr key={event.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/50">
                    <td className="p-3">{new Date(event.received_at).toLocaleString()}</td>
                    <td className="p-3 font-mono text-xs">{event.source}</td>
                    <td className="p-3 font-medium">{event.event_type}</td>
                    <td className="p-3">
                      <StatusBadge tone={event.status === "processed" ? "success" : "warning"}>
                        {event.status === "processed" ? "Completed" : "Pending"}
                      </StatusBadge>
                    </td>
                    <td className="p-3 text-xs text-slate-500">{event.source_device_id || "Unknown"}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </AppShell>
  );
}

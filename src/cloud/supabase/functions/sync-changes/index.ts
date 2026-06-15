import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.3";

serve(async (req: Request) => {
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }

  const deviceId = req.headers.get("x-device-id");
  const authHeader = req.headers.get("authorization");

  if (!deviceId || !authHeader || !authHeader.startsWith("Bearer ")) {
    return new Response("Unauthorized: Missing device credentials", { status: 401 });
  }

  const deviceSecret = authHeader.replace("Bearer ", "");
  const supabaseAdmin = createClient(
    Deno.env.get("SUPABASE_URL") ?? "",
    Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "",
  );

  const { data: device, error: deviceError } = await supabaseAdmin
    .from("sync_devices")
    .select("id, device_secret_hash, is_active")
    .eq("id", deviceId)
    .single();

  if (deviceError || !device || !device.is_active || device.device_secret_hash !== deviceSecret) {
    return new Response("Unauthorized: Invalid device credentials", { status: 401 });
  }

  let body: { cursor?: string };
  try {
    body = await req.json();
  } catch {
    return new Response("Bad request: Invalid JSON", { status: 400 });
  }

  const cursor = body.cursor || new Date(0).toISOString();
  const newCursor = new Date().toISOString();

  const [{ data: barbers }, { data: services }, { data: appointments }, { data: ticketCommands }] = await Promise.all([
    supabaseAdmin.from("barbers").select("*").gt("updated_at", cursor),
    supabaseAdmin.from("services").select("*").gt("updated_at", cursor),
    supabaseAdmin
      .from("appointments")
      .select(
        `
        *,
        customer:profiles(display_name, phone),
        barber:barbers(display_name, station_code),
        service:services(name, base_price_cents, duration_minutes)
      `,
      )
      .gt("updated_at", cursor),
    supabaseAdmin
      .from("ticket_admin_commands")
      .select("*")
      .eq("source_device_id", device.id)
      .eq("status", "pending"),
  ]);

  const changes: {
    catalog: Array<{ type: string; data: unknown }>;
    appointments: Array<{ type: string; data: unknown }>;
    ticket_commands: Array<{ type: string; data: unknown }>;
  } = {
    catalog: [],
    appointments: [],
    ticket_commands: [],
  };

  for (const barber of barbers || []) {
    changes.catalog.push({ type: "upsert_barber", data: barber });
  }

  for (const service of services || []) {
    changes.catalog.push({ type: "upsert_service", data: service });
  }

  for (const appointment of appointments || []) {
    if (appointment.status === "cancelled" || appointment.status === "no_show") {
      changes.appointments.push({ type: "cancel_appointment", data: appointment });
    } else {
      changes.appointments.push({ type: "upsert_appointment", data: appointment });
    }
  }

  for (const command of ticketCommands || []) {
    changes.ticket_commands.push({ type: "ticket.reassign", data: command });
  }

  return new Response(
    JSON.stringify({
      new_cursor: newCursor,
      changes,
    }),
    {
      headers: { "Content-Type": "application/json" },
    },
  );
});

import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.3";

const TIME_ZONE = "America/New_York";
const ACTIVE_APPOINTMENT_STATUSES = ["pending", "confirmed"];

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
  const operationalWindow = getOperationalAppointmentWindow(new Date());

  const [{ data: barbers }, { data: services }, { data: appointments }, { data: operationalAppointments }, { data: ticketCommands }, { data: payrollCommands }] = await Promise.all([
    supabaseAdmin.from("barbers").select("*").gt("updated_at", cursor),
    supabaseAdmin.from("services").select("*").gt("updated_at", cursor),
    supabaseAdmin
      .from("appointments")
      .select(
        `
        *,
        customer:profiles!appointments_customer_id_fkey(display_name, phone),
        barber:barbers(display_name, station_code),
        service:services(name, base_price_cents, duration_minutes)
      `,
      )
      .gt("updated_at", cursor),
    supabaseAdmin
      .from("appointments")
      .select(
        `
        *,
        customer:profiles!appointments_customer_id_fkey(display_name, phone),
        barber:barbers(display_name, station_code),
        service:services(name, base_price_cents, duration_minutes)
      `,
      )
      .in("status", ACTIVE_APPOINTMENT_STATUSES)
      .gte("starts_at", operationalWindow.startIso)
      .lt("starts_at", operationalWindow.endIso),
    supabaseAdmin
      .from("ticket_admin_commands")
      .select("*")
      .eq("source_device_id", device.id)
      .eq("status", "pending"),
    supabaseAdmin
      .from("payroll_admin_commands")
      .select("*")
      .eq("source_device_id", device.id)
      .eq("status", "pending")
      .neq("command_type", "adjustment_added")
      .order("created_at", { ascending: true }),
  ]);

  const changes: {
    catalog: Array<{ type: string; data: unknown }>;
    appointments: Array<{ type: string; data: unknown }>;
    ticket_commands: Array<{ type: string; data: unknown }>;
    payroll_commands: Array<{ type: string; data: unknown }>;
  } = {
    catalog: [],
    appointments: [],
    ticket_commands: [],
    payroll_commands: [],
  };

  for (const barber of barbers || []) {
    changes.catalog.push({ type: "upsert_barber", data: barber });
  }

  for (const service of services || []) {
    changes.catalog.push({ type: "upsert_service", data: service });
  }

  const appointmentsById = new Map<string, any>();
  for (const appointment of [...(appointments || []), ...(operationalAppointments || [])]) {
    if (appointment?.id) {
      appointmentsById.set(appointment.id, appointment);
    }
  }

  for (const appointment of appointmentsById.values()) {
    if (appointment.status === "cancelled" || appointment.status === "no_show") {
      changes.appointments.push({ type: "cancel_appointment", data: appointment });
    } else {
      changes.appointments.push({ type: "upsert_appointment", data: appointment });
    }
  }

  for (const command of ticketCommands || []) {
    const commandType = (command as any).command_type || "reassign";
    changes.ticket_commands.push({ type: `ticket.${commandType}`, data: command });
  }

  for (const command of payrollCommands || []) {
    const commandType = (command as any).command_type;
    changes.payroll_commands.push({ type: `payroll.${commandType}`, data: command });
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

function getOperationalAppointmentWindow(now: Date) {
  const startParts = getDatePartsInTimeZone(now);
  const nextDayProbe = new Date(now.getTime() + (24 * 60 * 60 * 1000));
  const endParts = getDatePartsInTimeZone(nextDayProbe);
  const startOffset = getTimeZoneOffset(now);
  const endOffset = getTimeZoneOffset(nextDayProbe);

  const startLocal = `${startParts.year}-${startParts.month}-${startParts.day}T00:00:00${startOffset}`;
  const endLocal = `${endParts.year}-${endParts.month}-${endParts.day}T00:00:00${endOffset}`;

  return {
    startIso: new Date(startLocal).toISOString(),
    endIso: new Date(endLocal).toISOString(),
  };
}

function getDatePartsInTimeZone(date: Date) {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(date);

  return {
    year: parts.find((part) => part.type === "year")?.value ?? "0000",
    month: parts.find((part) => part.type === "month")?.value ?? "01",
    day: parts.find((part) => part.type === "day")?.value ?? "01",
  };
}

function getTimeZoneOffset(date: Date) {
  const timeZoneName = new Intl.DateTimeFormat("en-US", {
    timeZone: TIME_ZONE,
    timeZoneName: "longOffset",
  }).formatToParts(date).find((part) => part.type === "timeZoneName")?.value;

  if (!timeZoneName) {
    return "-05:00";
  }

  return timeZoneName.replace("GMT", "") || "+00:00";
}

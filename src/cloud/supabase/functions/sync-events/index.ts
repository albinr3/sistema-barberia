import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.3";

type SyncEvent = {
  source_event_id: string;
  occurred_at?: string;
  event_type: string;
  aggregate_type: string;
  aggregate_id: string;
  payload: Record<string, unknown>;
};

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

  let body: { events?: SyncEvent[] };
  try {
    body = await req.json();
  } catch {
    return new Response("Bad request: Invalid JSON", { status: 400 });
  }

  const events = body.events || [];
  if (!Array.isArray(events)) {
    return new Response("Bad request: 'events' must be an array", { status: 400 });
  }

  const results = [];

  for (const event of events) {
    const { source_event_id, occurred_at, event_type, aggregate_type, aggregate_id, payload } = event;

    if (!source_event_id || !event_type || !aggregate_type || !aggregate_id || !payload) {
      results.push({ source_event_id, status: "error", message: "Missing required fields" });
      continue;
    }

    const { data: insertedEvent, error: insertError } = await supabaseAdmin
      .from("sync_events")
      .upsert(
        {
          source: "desktop",
          source_event_id,
          event_type,
          aggregate_type,
          aggregate_id,
          payload,
          source_device_id: device.id,
          status: "received",
        },
        { onConflict: "source, source_event_id" },
      )
      .select()
      .single();

    if (insertError) {
      results.push({ source_event_id, status: "error", message: insertError.message });
      continue;
    }

    try {
      if (event_type === "catalog.snapshot") {
        await materializeCatalogSnapshot(supabaseAdmin, device.id, payload);
      } else if (event_type === "sync.conflict") {
        await insertSyncConflict(supabaseAdmin, insertedEvent.id, aggregate_type, aggregate_id, payload);
      } else if (event_type.startsWith("appointment.")) {
        await materializeAppointmentEvent(supabaseAdmin, event_type, aggregate_id, payload, occurred_at);
      } else if (
        event_type === "ticket.created" ||
        event_type === "ticket.called" ||
        event_type === "ticket.started" ||
        event_type === "ticket.completed" ||
        event_type === "ticket.cancelled"
      ) {
        await materializeTicketEvent(supabaseAdmin, device.id, insertedEvent.id, event, occurred_at);
      } else if (event_type === "payment.collected") {
        await materializePaymentEvent(supabaseAdmin, device.id, insertedEvent.id, aggregate_id, payload);
      }

      await supabaseAdmin
        .from("sync_events")
        .update({ status: "processed", processed_at: new Date().toISOString() })
        .eq("id", insertedEvent.id);
      results.push({ source_event_id, status: "success" });
    } catch (error) {
      const message = error instanceof Error ? error.message : "Sync event failed";
      await supabaseAdmin
        .from("sync_events")
        .update({ status: "failed", error_message: message })
        .eq("id", insertedEvent.id);
      results.push({ source_event_id, status: "error", message });
    }
  }

  await supabaseAdmin
    .from("sync_devices")
    .update({ last_sync_at: new Date().toISOString() })
    .eq("id", device.id);

  return new Response(JSON.stringify({ results }), {
    headers: { "Content-Type": "application/json" },
  });
});

async function materializeCatalogSnapshot(
  supabaseAdmin: ReturnType<typeof createClient>,
  deviceId: string,
  payload: Record<string, unknown>,
) {
  const items = Array.isArray(payload.items) ? payload.items : [];
  for (const item of items) {
    if (!isRecord(item)) continue;

    const entityType = stringValue(item.entity_type);
    const localId = stringValue(item.local_id);
    const displayName = stringValue(item.display_name);
    const updatedAt = stringValue(item.updated_at);
    if (!entityType || !localId || !displayName || !updatedAt) continue;
    if (entityType !== "barber" && entityType !== "service") continue;

    if (entityType === "barber") {
      const stationCode = stringValue(item.station_code);
      const isAvailableLocally = booleanValue(item.is_available_locally, true);
      const { data: existing } = await supabaseAdmin
        .from("barbers")
        .select("updated_at, display_name, station_code, is_available_locally")
        .eq("id", localId)
        .maybeSingle();

      if (existing && new Date(existing.updated_at) >= new Date(updatedAt)) {
        console.warn(`[Sync] Catalog snapshot updated_at (${updatedAt}) is not newer than cloud (${existing.updated_at}) for ${localId}. Skipping stale snapshot item.`);
        continue;
      }

      if (
        existing &&
        existing.display_name === displayName &&
        nullableString(existing.station_code) === stationCode &&
        existing.is_available_locally === isAvailableLocally
      ) {
        continue;
      }

      const { error } = await supabaseAdmin.from("barbers").upsert({
        id: localId,
        display_name: displayName,
        station_code: stationCode,
        is_available_locally: isAvailableLocally,
        updated_at: updatedAt,
      });
      if (error) throw new Error("Barber upsert failed: " + error.message);
    } else if (entityType === "service") {
      const priceCents = numberValue(item.price_cents) || 0;
      const isActive = booleanValue(item.is_active, true);
      const { data: existing } = await supabaseAdmin
        .from("services")
        .select("updated_at, name, base_price_cents, is_active")
        .eq("id", localId)
        .maybeSingle();

      if (existing && new Date(existing.updated_at) >= new Date(updatedAt)) {
        console.warn(`[Sync] Catalog snapshot updated_at (${updatedAt}) is not newer than cloud (${existing.updated_at}) for ${localId}. Skipping stale snapshot item.`);
        continue;
      }

      if (
        existing &&
        existing.name === displayName &&
        existing.base_price_cents === priceCents &&
        existing.is_active === isActive
      ) {
        continue;
      }

      const { error } = await supabaseAdmin.from("services").upsert({
        id: localId,
        name: displayName,
        base_price_cents: priceCents,
        is_active: isActive,
        updated_at: updatedAt,
      });
      if (error) throw new Error("Service upsert failed: " + error.message);
    }
  }
}

async function materializeTicketEvent(
  supabaseAdmin: ReturnType<typeof createClient>,
  deviceId: string,
  syncEventId: string,
  event: SyncEvent,
  occurredAt?: string,
) {
  const payload = event.payload;
  const localBarberId = stringValue(payload.barber_id) || stringValue(payload.assigned_barber_id);
  const cloudBarberId = localBarberId;
  const appointmentId = stringValue(payload.appointment_id);
  const ticketRecord: Record<string, unknown> = {
    local_ticket_id: event.aggregate_id,
    source_device_id: deviceId,
    status: stringValue(payload.status) || statusFromTicketEvent(event.event_type),
  };

  const customerName = stringValue(payload.customer_name);
  if (customerName) {
    ticketRecord.customer_name = customerName;
  }

  if (cloudBarberId) {
    ticketRecord.barber_id = cloudBarberId;
  }

  if (appointmentId) {
    ticketRecord.appointment_id = appointmentId;
  }

  const startedAt = stringValue(payload.started_at) || (event.event_type === "ticket.started" ? occurredAt : null);
  if (startedAt) {
    ticketRecord.started_at = startedAt;
  }

  const completedAt = stringValue(payload.completed_at) || (event.event_type === "ticket.completed" ? occurredAt : null);
  if (completedAt) {
    ticketRecord.completed_at = completedAt;
  }

  const cancelledAt = stringValue(payload.cancelled_at) || (event.event_type === "ticket.cancelled" ? occurredAt : null);
  if (cancelledAt) {
    ticketRecord.cancelled_at = cancelledAt;
  }

  const displayTicketNumber = numberValue(payload.display_ticket_number);
  if (displayTicketNumber !== null) {
    ticketRecord.display_ticket_number = displayTicketNumber;
  }

  const ticketDate = stringValue(payload.ticket_date);
  if (ticketDate) {
    ticketRecord.ticket_date = ticketDate;
  }

  const checkedInAt = stringValue(payload.checked_in_at);
  if (checkedInAt) {
    ticketRecord.checked_in_at = checkedInAt;
  }

  await supabaseAdmin.from("synced_tickets").upsert(
    ticketRecord,
    { onConflict: "local_ticket_id" },
  );

  const items = Array.isArray(payload.items) ? payload.items : [];
  if (items.length > 0) {
    const { data: ticketRecord } = await supabaseAdmin
      .from("synced_tickets")
      .select("id")
      .eq("local_ticket_id", event.aggregate_id)
      .single();

    if (ticketRecord) {
      for (const item of items) {
        if (!isRecord(item)) continue;

        const localServiceId = stringValue(item.service_id);
        const cloudServiceId = localServiceId;

        await supabaseAdmin.from("synced_ticket_items").upsert(
          {
            synced_ticket_id: ticketRecord.id,
            local_item_id: stringValue(item.local_item_id) || stringValue(item.id) || crypto.randomUUID(),
            service_id: cloudServiceId,
            price_cents: centsValue(item.price_cents, item.price),
          },
          { onConflict: "synced_ticket_id, local_item_id" },
        );
      }
    }
  }

  if (event.event_type === "ticket.completed" && appointmentId) {
    await completeAppointment(supabaseAdmin, appointmentId, stringValue(payload.completed_at) || occurredAt);
  }
}

async function materializePaymentEvent(
  supabaseAdmin: ReturnType<typeof createClient>,
  deviceId: string,
  syncEventId: string,
  aggregateId: string,
  payload: Record<string, unknown>,
) {
  const localTicketId = stringValue(payload.ticket_id);
  const { data: ticketRecord } = await supabaseAdmin
    .from("synced_tickets")
    .select("id")
    .eq("local_ticket_id", localTicketId)
    .maybeSingle();

  if (!ticketRecord) {
    await insertSyncConflict(supabaseAdmin, syncEventId, "payment", aggregateId, {
      conflict_type: "missing_ticket_for_payment",
      local_payload: payload,
    });
    return;
  }

  await supabaseAdmin.from("synced_payments").upsert(
    {
      local_payment_id: aggregateId,
      synced_ticket_id: ticketRecord.id,
      source_device_id: deviceId,
      payment_method: stringValue(payload.payment_method) || "cash",
      amount_cents: centsValue(payload.amount_cents, payload.amount),
    },
    { onConflict: "local_payment_id" },
  );
}

async function materializeAppointmentEvent(
  supabaseAdmin: ReturnType<typeof createClient>,
  eventType: string,
  aggregateId: string,
  payload: Record<string, unknown>,
  occurredAt?: string,
) {
  if (eventType === "appointment.checked_in") {
    await supabaseAdmin
      .from("appointments")
      .update({ checked_in_at: stringValue(payload.checked_in_at) || occurredAt || new Date().toISOString() })
      .eq("id", aggregateId)
      .in("status", ["pending", "confirmed"]);
    return;
  }

  if (eventType === "appointment.no_show") {
    await supabaseAdmin
      .from("appointments")
      .update({
        status: "no_show",
        no_show_at: stringValue(payload.no_show_at) || occurredAt || new Date().toISOString(),
      })
      .eq("id", aggregateId)
      .in("status", ["pending", "confirmed"]);
    return;
  }

  if (eventType === "appointment.completed") {
    await completeAppointment(supabaseAdmin, aggregateId, stringValue(payload.completed_at) || occurredAt);
  }
}

async function completeAppointment(
  supabaseAdmin: ReturnType<typeof createClient>,
  appointmentId: string,
  completedAt?: string | null,
) {
  await supabaseAdmin
    .from("appointments")
    .update({
      status: "completed",
      completed_at: completedAt || new Date().toISOString(),
    })
    .eq("id", appointmentId)
    .in("status", ["pending", "confirmed"]);
}



async function insertSyncConflict(
  supabaseAdmin: ReturnType<typeof createClient>,
  syncEventId: string,
  aggregateType: string,
  aggregateId: string,
  payload: Record<string, unknown>,
) {
  await supabaseAdmin.from("sync_conflicts").insert({
    sync_event_id: syncEventId,
    conflict_type: stringValue(payload.conflict_type) || "desktop_sync_conflict",
    aggregate_type: aggregateType,
    aggregate_id: aggregateId,
    local_payload: isRecord(payload.local_payload) ? payload.local_payload : payload,
    cloud_payload: isRecord(payload.cloud_payload) ? payload.cloud_payload : {},
  });
}

function statusFromTicketEvent(eventType: string) {
  if (eventType === "ticket.called") return "called";
  if (eventType === "ticket.started") return "in_progress";
  if (eventType === "ticket.cancelled") return "cancelled";
  if (eventType === "ticket.completed") return "completed";
  return "waiting";
}

function centsValue(cents: unknown, amount: unknown) {
  if (typeof cents === "number" && Number.isFinite(cents)) return Math.round(cents);
  if (typeof amount === "number" && Number.isFinite(amount)) return Math.round(amount * 100);
  return 0;
}

function stringValue(value: unknown) {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

function numberValue(value: unknown) {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

function booleanValue(value: unknown, fallback: boolean) {
  return typeof value === "boolean" ? value : fallback;
}

function nullableString(value: unknown) {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

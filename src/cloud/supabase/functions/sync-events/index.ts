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
      } else if (event_type === "desktop.sync_heartbeat") {
        await materializeSyncHeartbeat(supabaseAdmin, device.id, payload);
      } else if (event_type === "desktop.restore_applied") {
        await materializeDesktopRestore(supabaseAdmin, device.id, insertedEvent.id, payload);
      } else if (event_type === "payroll.snapshot") {
        await materializePayrollSnapshot(supabaseAdmin, device.id, payload);
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
      } else if (event_type === "ticket_admin_command.applied" || event_type === "ticket_admin_command.failed") {
        await materializeTicketAdminCommandEvent(supabaseAdmin, event_type, aggregate_id, payload, occurred_at);
      } else if (event_type === "payroll_admin_command.applied" || event_type === "payroll_admin_command.failed") {
        await materializePayrollAdminCommandEvent(supabaseAdmin, event_type, aggregate_id, payload, occurred_at);
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

async function materializeDesktopRestore(
  supabaseAdmin: ReturnType<typeof createClient>,
  deviceId: string,
  syncEventId: string,
  payload: Record<string, unknown>,
) {
  const restoreId = stringValue(payload.restore_id);
  const restoredAt = stringValue(payload.restored_at) || new Date().toISOString();
  if (!restoreId) {
    throw new Error("Restore payload restore_id is required");
  }

  const snapshot = isRecord(payload.snapshot) ? payload.snapshot : {};
  const tickets = Array.isArray(snapshot.tickets) ? snapshot.tickets.filter(isRecord) : [];
  const ticketItems = Array.isArray(snapshot.ticket_items) ? snapshot.ticket_items.filter(isRecord) : [];
  const payments = Array.isArray(snapshot.payments) ? snapshot.payments.filter(isRecord) : [];
  const backup = isRecord(payload.backup) ? payload.backup : {};
  const existingBarberIds = await loadExistingIds(
    supabaseAdmin,
    "barbers",
    tickets.map((ticket) => stringValue(ticket.barber_id)),
  );
  const existingAppointmentIds = await loadExistingIds(
    supabaseAdmin,
    "appointments",
    tickets.map((ticket) => stringValue(ticket.appointment_id)),
  );
  const existingServiceIds = await loadExistingIds(
    supabaseAdmin,
    "services",
    ticketItems.map((item) => stringValue(item.service_id)),
  );

  await supabaseAdmin.from("desktop_restore_batches").upsert({
    id: restoreId,
    source_device_id: deviceId,
    restored_at: restoredAt,
    backup_file_name: stringValue(backup.file_name),
    backup_size_bytes: numberValue(backup.size_bytes) ?? 0,
    safety_backup_path: stringValue(backup.safety_backup_path),
    ticket_count: tickets.length,
    payment_count: payments.length,
    sync_event_id: syncEventId,
  });

  const ticketRows = tickets
    .map((ticket) => {
      const localTicketId = stringValue(ticket.local_ticket_id);
      const status = stringValue(ticket.status);
      const checkedInAt = stringValue(ticket.checked_in_at);
      if (!localTicketId || !status || !checkedInAt) return null;
      const barberId = stringValue(ticket.barber_id);
      const appointmentId = stringValue(ticket.appointment_id);
      return {
        local_ticket_id: localTicketId,
        source_device_id: deviceId,
        display_ticket_number: numberValue(ticket.display_ticket_number),
        ticket_date: dateOnlyValue(ticket.ticket_date),
        customer_name: stringValue(ticket.customer_name),
        status,
        checked_in_at: checkedInAt,
        barber_id: barberId && existingBarberIds.has(barberId) ? barberId : null,
        appointment_id: appointmentId && existingAppointmentIds.has(appointmentId) ? appointmentId : null,
        started_at: stringValue(ticket.started_at),
        completed_at: stringValue(ticket.completed_at),
        cancelled_at: stringValue(ticket.cancelled_at),
        restore_reverted_at: null,
        restore_reverted_by: null,
        restore_revert_reason: null,
      };
    })
    .filter((row): row is Record<string, unknown> => row !== null);

  if (ticketRows.length > 0) {
    const { error } = await supabaseAdmin
      .from("synced_tickets")
      .upsert(ticketRows, { onConflict: "local_ticket_id" });
    if (error) throw new Error("Restore ticket upsert failed: " + error.message);
  }

  const { data: cloudTickets, error: ticketSelectError } = await supabaseAdmin
    .from("synced_tickets")
    .select("id, local_ticket_id, restore_reverted_at")
    .eq("source_device_id", deviceId);
  if (ticketSelectError) throw new Error("Restore ticket lookup failed: " + ticketSelectError.message);

  const ticketIdsByLocalId = new Map<string, string>();
  for (const ticket of cloudTickets || []) {
    ticketIdsByLocalId.set(String(ticket.local_ticket_id), String(ticket.id));
  }

  const activeTicketIds = new Set(ticketRows.map((ticket) => String(ticket.local_ticket_id)));
  const ticketsToRevert = (cloudTickets || [])
    .filter((ticket) => !activeTicketIds.has(String(ticket.local_ticket_id)) && !ticket.restore_reverted_at)
    .map((ticket) => String(ticket.id));

  if (ticketsToRevert.length > 0) {
    const { error } = await supabaseAdmin
      .from("synced_tickets")
      .update({
        restore_reverted_at: restoredAt,
        restore_reverted_by: restoreId,
        restore_revert_reason: "Missing from restored desktop backup",
      })
      .in("id", ticketsToRevert);
    if (error) throw new Error("Restore ticket revert failed: " + error.message);
  }

  const itemRows = ticketItems
    .map((item) => {
      const localTicketId = stringValue(item.local_ticket_id);
      const localItemId = stringValue(item.local_item_id);
      const serviceId = stringValue(item.service_id);
      const ticketId = localTicketId ? ticketIdsByLocalId.get(localTicketId) : null;
      if (!ticketId || !localItemId) return null;
      return {
        synced_ticket_id: ticketId,
        local_item_id: localItemId,
        service_id: serviceId && existingServiceIds.has(serviceId) ? serviceId : null,
        price_cents: centsValue(item.price_cents, null),
        restore_reverted_at: null,
        restore_reverted_by: null,
        restore_revert_reason: null,
      };
    })
    .filter((row): row is Record<string, unknown> => row !== null);

  if (itemRows.length > 0) {
    const { error } = await supabaseAdmin
      .from("synced_ticket_items")
      .upsert(itemRows, { onConflict: "synced_ticket_id, local_item_id" });
    if (error) throw new Error("Restore ticket item upsert failed: " + error.message);
  }

  const allTicketIdsForDevice = Array.from(ticketIdsByLocalId.values());
  if (allTicketIdsForDevice.length > 0) {
    const activeItemKeys = new Set(itemRows.map((item) => `${item.synced_ticket_id}:${item.local_item_id}`));
    const { data: cloudItems, error: itemLookupError } = await supabaseAdmin
      .from("synced_ticket_items")
      .select("id, synced_ticket_id, local_item_id, restore_reverted_at")
      .in("synced_ticket_id", allTicketIdsForDevice);
    if (itemLookupError) throw new Error("Restore ticket item lookup failed: " + itemLookupError.message);

    const itemsToRevert = (cloudItems || [])
      .filter((item) => !activeItemKeys.has(`${item.synced_ticket_id}:${item.local_item_id}`) && !item.restore_reverted_at)
      .map((item) => String(item.id));
    if (itemsToRevert.length > 0) {
      const { error } = await supabaseAdmin
        .from("synced_ticket_items")
        .update({
          restore_reverted_at: restoredAt,
          restore_reverted_by: restoreId,
          restore_revert_reason: "Missing from restored desktop backup",
        })
        .in("id", itemsToRevert);
      if (error) throw new Error("Restore ticket item revert failed: " + error.message);
    }
  }

  const paymentRows = payments
    .map((payment) => {
      const localPaymentId = stringValue(payment.local_payment_id);
      const localTicketId = stringValue(payment.local_ticket_id);
      const ticketId = localTicketId ? ticketIdsByLocalId.get(localTicketId) : null;
      if (!localPaymentId || !ticketId) return null;
      return {
        local_payment_id: localPaymentId,
        synced_ticket_id: ticketId,
        source_device_id: deviceId,
        payment_method: stringValue(payment.payment_method) || "cash",
        amount_cents: centsValue(payment.amount_cents, null),
        receipt_number: stringValue(payment.receipt_number),
        payment_reference: stringValue(payment.payment_reference),
        collected_at: stringValue(payment.collected_at) || restoredAt,
        restore_reverted_at: null,
        restore_reverted_by: null,
        restore_revert_reason: null,
      };
    })
    .filter((row): row is Record<string, unknown> => row !== null);

  if (paymentRows.length > 0) {
    const { error } = await supabaseAdmin
      .from("synced_payments")
      .upsert(paymentRows, { onConflict: "local_payment_id" });
    if (error) throw new Error("Restore payment upsert failed: " + error.message);
  }

  const activePaymentIds = new Set(paymentRows.map((payment) => String(payment.local_payment_id)));
  const { data: cloudPayments, error: paymentLookupError } = await supabaseAdmin
    .from("synced_payments")
    .select("id, local_payment_id, restore_reverted_at")
    .eq("source_device_id", deviceId);
  if (paymentLookupError) throw new Error("Restore payment lookup failed: " + paymentLookupError.message);

  const paymentsToRevert = (cloudPayments || [])
    .filter((payment) => !activePaymentIds.has(String(payment.local_payment_id)) && !payment.restore_reverted_at)
    .map((payment) => String(payment.id));
  if (paymentsToRevert.length > 0) {
    const { error } = await supabaseAdmin
      .from("synced_payments")
      .update({
        restore_reverted_at: restoredAt,
        restore_reverted_by: restoreId,
        restore_revert_reason: "Missing from restored desktop backup",
      })
      .in("id", paymentsToRevert);
    if (error) throw new Error("Restore payment revert failed: " + error.message);
  }

  await supabaseAdmin
    .from("desktop_restore_batches")
    .update({
      reverted_ticket_count: ticketsToRevert.length,
      reverted_payment_count: paymentsToRevert.length,
    })
    .eq("id", restoreId);

  const { data: existingAudit } = await supabaseAdmin
    .from("audit_log")
    .select("id")
    .eq("action", "desktop_restore_applied")
    .eq("entity_id", restoreId)
    .maybeSingle();

  if (!existingAudit) {
    await supabaseAdmin.from("audit_log").insert({
      action: "desktop_restore_applied",
      entity_type: "desktop_restore",
      entity_id: restoreId,
      metadata: {
        source_device_id: deviceId,
        restored_at: restoredAt,
        ticket_count: ticketRows.length,
        payment_count: paymentRows.length,
        reverted_ticket_count: ticketsToRevert.length,
        reverted_payment_count: paymentsToRevert.length,
      },
    });
  }
}

async function materializeSyncHeartbeat(
  supabaseAdmin: ReturnType<typeof createClient>,
  deviceId: string,
  payload: Record<string, unknown>,
) {
  await supabaseAdmin
    .from("sync_devices")
    .update({
      last_sync_at: new Date().toISOString(),
      pending_outbox_count: numberValue(payload.pending_outbox_count) ?? 0,
    })
    .eq("id", deviceId);
}

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

      const staleCatalog = existing && new Date(existing.updated_at) > new Date(updatedAt);
      if (!staleCatalog) {
        const catalogChanged =
          !existing ||
          existing.display_name !== displayName ||
          nullableString(existing.station_code) !== stationCode ||
          existing.is_available_locally !== isAvailableLocally;

        if (catalogChanged) {
          const { error } = await supabaseAdmin.from("barbers").upsert({
            id: localId,
            display_name: displayName,
            station_code: stationCode,
            is_available_locally: isAvailableLocally,
            updated_at: updatedAt,
          });
          if (error) throw new Error("Barber upsert failed: " + error.message);
        }
      }

      await materializeBarberOperationalStatus(supabaseAdmin, deviceId, localId, item, updatedAt);
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

async function materializeBarberOperationalStatus(
  supabaseAdmin: ReturnType<typeof createClient>,
  deviceId: string,
  barberId: string,
  item: Record<string, unknown>,
  updatedAt: string,
) {
  const businessDate = dateOnlyValue(item.business_date);
  const state = stringValue(item.state);
  const clientsServedToday = numberValue(item.clients_served_today);
  if (!businessDate || !state || clientsServedToday === null) {
    return;
  }

  const { data: existing } = await supabaseAdmin
    .from("barber_operational_status")
    .select("updated_at")
    .eq("barber_id", barberId)
    .maybeSingle();
  if (existing && new Date(existing.updated_at) > new Date(updatedAt)) {
    return;
  }

  const { error } = await supabaseAdmin.from("barber_operational_status").upsert({
    barber_id: barberId,
    source_device_id: deviceId,
    business_date: businessDate,
    state,
    clients_served_today: clientsServedToday,
    checked_in_at: stringValue(item.checked_in_at),
    daily_queue_position: numberValue(item.daily_queue_position),
    daily_arrived_at: stringValue(item.daily_arrived_at),
    is_checked_in_today: booleanValue(item.is_checked_in_today, false),
    updated_at: updatedAt,
  });
  if (error) throw new Error("Barber operational status upsert failed: " + error.message);
}

async function materializePayrollSnapshot(
  supabaseAdmin: ReturnType<typeof createClient>,
  deviceId: string,
  payload: Record<string, unknown>,
) {
  if (!isRecord(payload.period)) {
    throw new Error("Payroll snapshot period is required");
  }

  const period = payload.period;
  const startDate = dateOnlyValue(period.start_date);
  const endDate = dateOnlyValue(period.end_date);
  const localPeriodId = stringValue(period.id);
  if (!startDate || !endDate || !localPeriodId) {
    throw new Error("Payroll snapshot period id, start_date and end_date are required");
  }

  const { data: periodRecord, error: periodError } = await supabaseAdmin
    .from("synced_payroll_periods")
    .upsert(
      {
        source_device_id: deviceId,
        local_period_id: localPeriodId,
        start_date: startDate,
        end_date: endDate,
        state: stringValue(period.state) || "draft",
        total_services: numberValue(period.total_services) ?? 0,
        total_commission_cents: numberValue(period.total_commission_cents) ?? 0,
        total_adjustments_cents: numberValue(period.total_adjustments_cents) ?? 0,
        total_to_pay_cents: numberValue(period.total_to_pay_cents) ?? 0,
        payment_method: stringValue(period.payment_method),
        payment_reference: stringValue(period.payment_reference),
        notes: stringValue(period.notes),
        generated_at: stringValue(period.generated_at) || new Date().toISOString(),
        paid_at: stringValue(period.paid_at),
        loaded_at: stringValue(payload.loaded_at) || new Date().toISOString(),
      },
      { onConflict: "source_device_id,start_date,end_date" },
    )
    .select("id")
    .single();

  if (periodError) throw new Error("Payroll period upsert failed: " + periodError.message);
  if (!periodRecord) throw new Error("Payroll period upsert returned no row");

  await supabaseAdmin.from("synced_payroll_lines").delete().eq("payroll_period_id", periodRecord.id);

  const lines = Array.isArray(payload.lines) ? payload.lines : [];
  const lineRows = lines
    .filter(isRecord)
    .map((line) => ({
      payroll_period_id: periodRecord.id,
      local_line_id: stringValue(line.id) || crypto.randomUUID(),
      barber_id: stringValue(line.barber_id),
      barber_name: stringValue(line.barber_name) || "Local barber",
      station_number: numberValue(line.station_number),
      closed_services_count: numberValue(line.closed_services_count) ?? 0,
      sales_generated_cents: numberValue(line.sales_generated_cents) ?? 0,
      commission_cents: numberValue(line.commission_cents) ?? 0,
      adjustments_cents: numberValue(line.adjustments_cents) ?? 0,
      total_cents: numberValue(line.total_cents) ?? 0,
    }));

  if (lineRows.length > 0) {
    const { error } = await supabaseAdmin.from("synced_payroll_lines").insert(lineRows);
    if (error) throw new Error("Payroll lines insert failed: " + error.message);
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

  if (payload.barber_id !== undefined) {
    ticketRecord.barber_id = stringValue(payload.barber_id);
  } else if (payload.assigned_barber_id !== undefined) {
    ticketRecord.barber_id = stringValue(payload.assigned_barber_id);
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
      receipt_number: stringValue(payload.receipt_number),
      payment_reference: stringValue(payload.payment_reference),
      collected_at: stringValue(payload.collected_at) || new Date().toISOString(),
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



async function materializePayrollAdminCommandEvent(
  supabaseAdmin: ReturnType<typeof createClient>,
  eventType: string,
  aggregateId: string,
  payload: Record<string, unknown>,
  occurredAt?: string,
) {
  const status = eventType === "payroll_admin_command.applied" ? "applied" : "failed";
  const errorMessage = stringValue(payload.error_message);

  const updateData: Record<string, unknown> = {
    status,
    applied_at: stringValue(payload.applied_at) || occurredAt || new Date().toISOString(),
  };

  if (errorMessage) {
    updateData.error_message = errorMessage;
  }

  await supabaseAdmin
    .from("payroll_admin_commands")
    .update(updateData)
    .eq("id", aggregateId)
    .eq("status", "pending");
}

async function materializeTicketAdminCommandEvent(
  supabaseAdmin: ReturnType<typeof createClient>,
  eventType: string,
  aggregateId: string,
  payload: Record<string, unknown>,
  occurredAt?: string,
) {
  const status = eventType === "ticket_admin_command.applied" ? "applied" : "failed";
  const errorMessage = stringValue(payload.error_message);
  
  const updateData: Record<string, unknown> = {
    status,
    applied_at: stringValue(payload.applied_at) || occurredAt || new Date().toISOString(),
  };

  if (errorMessage) {
    updateData.error_message = errorMessage;
  }

  await supabaseAdmin
    .from("ticket_admin_commands")
    .update(updateData)
    .eq("id", aggregateId)
    .eq("status", "pending");
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

function dateOnlyValue(value: unknown) {
  if (typeof value !== "string") return null;
  const trimmed = value.trim();
  if (!trimmed) return null;
  return trimmed.includes("T") ? trimmed.slice(0, 10) : trimmed;
}

function booleanValue(value: unknown, fallback: boolean) {
  return typeof value === "boolean" ? value : fallback;
}

function nullableString(value: unknown) {
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : null;
}

async function loadExistingIds(
  supabaseAdmin: ReturnType<typeof createClient>,
  tableName: string,
  values: Array<string | null>,
) {
  const ids = Array.from(new Set(values.filter((value): value is string => value !== null)));
  if (ids.length === 0) {
    return new Set<string>();
  }

  const { data, error } = await supabaseAdmin
    .from(tableName)
    .select("id")
    .in("id", ids);
  if (error) {
    throw new Error(`Restore ${tableName} lookup failed: ${error.message}`);
  }

  return new Set((data || []).map((row) => String(row.id)));
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}


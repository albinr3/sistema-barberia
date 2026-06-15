"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import type { Route } from "next";
import { z } from "zod";
import { requireAdmin } from "@/lib/auth/profile";
import { centsFromDollarInput } from "@/lib/catalog/filters";
import { createClient } from "@/lib/supabase/server";

const catalogPath = "/admin/admin-dashboard";

const optionalUuid = z.string().trim().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/i, "Invalid UUID").optional().or(z.literal(""));

const barberSchema = z.object({
  id: optionalUuid,
  display_name: z.string().trim().min(2, "Barber name is required."),
  station_code: z
    .string()
    .trim()
    .regex(/^B-[0-9]+$/, "Use station format B-#.")
    .optional()
    .or(z.literal("")),
  profile_image_path: z.string().trim().optional(),
  is_active: z.boolean(),
  is_available_locally: z.boolean(),
});

const serviceSchema = z.object({
  id: optionalUuid,
  name: z.string().trim().min(2, "Service name is required."),
  description: z.string().trim().optional(),
  base_price: z.string().trim().min(1, "Base price is required."),
  duration_minutes: z.coerce.number().int().positive("Duration must be greater than zero."),
  sort_order: z.coerce.number().int(),
  is_active: z.boolean(),
});

const ruleSchema = z.object({
  id: optionalUuid,
  barber_id: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/i, "Invalid Barber ID"),
  day_of_week: z.coerce.number().int().min(0).max(6),
  starts_at: z.string().regex(/^[0-9]{2}:[0-9]{2}$/),
  ends_at: z.string().regex(/^[0-9]{2}:[0-9]{2}$/),
  slot_minutes: z.coerce.number().int().positive(),
  is_active: z.boolean(),
});

const exceptionSchema = z.object({
  id: optionalUuid,
  barber_id: z.string().regex(/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/i, "Invalid Barber ID"),
  exception_date: z.string().regex(/^[0-9]{4}-[0-9]{2}-[0-9]{2}$/),
  starts_at: z.string().optional(),
  ends_at: z.string().optional(),
  is_closed: z.boolean(),
  reason: z.string().trim().optional(),
});

const deleteSchema = z.object({
  id: z.string().uuid(),
});

function checked(formData: FormData, key: string) {
  return formData.get(key) === "on";
}

function statusUrl(type: "success" | "error", message: string) {
  const params = new URLSearchParams({ [type]: message });
  return `${catalogPath}?${params.toString()}` as Route;
}

async function getAdminSupabase() {
  const supabase = await createClient();
  await requireAdmin(supabase);
  return supabase;
}

function handleValidationError(error: unknown) {
  if (error instanceof z.ZodError) {
    const message = error.issues[0]?.message ?? "Review the form values.";
    redirect(statusUrl("error", message));
  }
}

export async function saveBarber(formData: FormData) {
  const supabase = await getAdminSupabase();

  try {
    const values = barberSchema.parse({
      id: formData.get("id")?.toString() ?? "",
      display_name: formData.get("display_name")?.toString() ?? "",
      station_code: formData.get("station_code")?.toString() ?? "",
      profile_image_path: formData.get("profile_image_path")?.toString() ?? "",
      is_active: checked(formData, "is_active"),
      is_available_locally: checked(formData, "is_available_locally"),
    });

    const payload = {
      ...(values.id ? { id: values.id } : {}),
      display_name: values.display_name,
      station_code: values.station_code || null,
      profile_image_path: values.profile_image_path || null,
      is_active: values.is_active,
      is_available_locally: values.is_available_locally,
    };

    const { error } = await supabase.from("barbers").upsert(payload);

    if (error) {
      redirect(statusUrl("error", error.message));
    }
  } catch (error) {
    handleValidationError(error);
    throw error;
  }

  revalidatePath(catalogPath);
  redirect(statusUrl("success", "Barber saved."));
}

export async function saveService(formData: FormData) {
  const supabase = await getAdminSupabase();

  try {
    const values = serviceSchema.parse({
      id: formData.get("id")?.toString() ?? "",
      name: formData.get("name")?.toString() ?? "",
      description: formData.get("description")?.toString() ?? "",
      base_price: formData.get("base_price")?.toString() ?? "",
      duration_minutes: formData.get("duration_minutes")?.toString() ?? "",
      sort_order: formData.get("sort_order")?.toString() ?? "0",
      is_active: checked(formData, "is_active"),
    });
    const basePriceCents = centsFromDollarInput(values.base_price);

    if (!Number.isFinite(basePriceCents) || basePriceCents <= 0) {
      redirect(statusUrl("error", "Base price must be greater than zero."));
    }

    const payload = {
      ...(values.id ? { id: values.id } : {}),
      name: values.name,
      description: values.description || null,
      base_price_cents: basePriceCents,
      duration_minutes: values.duration_minutes,
      sort_order: values.sort_order,
      is_active: values.is_active,
    };

    const { error } = await supabase.from("services").upsert(payload);

    if (error) {
      redirect(statusUrl("error", error.message));
    }
  } catch (error) {
    handleValidationError(error);
    throw error;
  }

  revalidatePath(catalogPath);
  redirect(statusUrl("success", "Service saved."));
}

export async function saveAvailabilityRule(formData: FormData) {
  const supabase = await getAdminSupabase();

  try {
    const values = ruleSchema.parse({
      id: formData.get("id")?.toString() ?? "",
      barber_id: formData.get("barber_id")?.toString() ?? "",
      day_of_week: formData.get("day_of_week")?.toString() ?? "",
      starts_at: formData.get("starts_at")?.toString() ?? "",
      ends_at: formData.get("ends_at")?.toString() ?? "",
      slot_minutes: formData.get("slot_minutes")?.toString() ?? "30",
      is_active: checked(formData, "is_active"),
    });
    const payload = {
      ...(values.id ? { id: values.id } : {}),
      barber_id: values.barber_id,
      day_of_week: values.day_of_week,
      starts_at: values.starts_at,
      ends_at: values.ends_at,
      slot_minutes: values.slot_minutes,
      is_active: values.is_active,
    };
    const { error } = await supabase.from("availability_rules").upsert(payload);

    if (error) {
      redirect(statusUrl("error", error.message));
    }
  } catch (error) {
    handleValidationError(error);
    throw error;
  }

  revalidatePath(catalogPath);
  redirect(statusUrl("success", "Availability rule saved."));
}

export async function saveAvailabilityException(formData: FormData) {
  const supabase = await getAdminSupabase();

  try {
    const values = exceptionSchema.parse({
      id: formData.get("id")?.toString() ?? "",
      barber_id: formData.get("barber_id")?.toString() ?? "",
      exception_date: formData.get("exception_date")?.toString() ?? "",
      starts_at: formData.get("starts_at")?.toString() ?? "",
      ends_at: formData.get("ends_at")?.toString() ?? "",
      is_closed: checked(formData, "is_closed"),
      reason: formData.get("reason")?.toString() ?? "",
    });

    if (!values.is_closed && (!values.starts_at || !values.ends_at)) {
      redirect(statusUrl("error", "Open exceptions require start and end times."));
    }

    const payload = {
      ...(values.id ? { id: values.id } : {}),
      barber_id: values.barber_id,
      exception_date: values.exception_date,
      starts_at: values.is_closed ? null : values.starts_at,
      ends_at: values.is_closed ? null : values.ends_at,
      is_closed: values.is_closed,
      reason: values.reason || null,
    };
    const { error } = await supabase.from("availability_exceptions").upsert(payload);

    if (error) {
      redirect(statusUrl("error", error.message));
    }
  } catch (error) {
    handleValidationError(error);
    throw error;
  }

  revalidatePath(catalogPath);
  redirect(statusUrl("success", "Availability exception saved."));
}

export async function deleteAvailabilityRule(formData: FormData) {
  const supabase = await getAdminSupabase();
  const values = deleteSchema.parse({ id: formData.get("id")?.toString() ?? "" });
  const { error } = await supabase.from("availability_rules").delete().eq("id", values.id);

  if (error) {
    redirect(statusUrl("error", error.message));
  }

  revalidatePath(catalogPath);
  redirect(statusUrl("success", "Availability rule deleted."));
}

export async function deleteAvailabilityException(formData: FormData) {
  const supabase = await getAdminSupabase();
  const values = deleteSchema.parse({ id: formData.get("id")?.toString() ?? "" });
  const { error } = await supabase.from("availability_exceptions").delete().eq("id", values.id);

  if (error) {
    redirect(statusUrl("error", error.message));
  }

  revalidatePath(catalogPath);
  redirect(statusUrl("success", "Availability exception deleted."));
}

const weeklyScheduleSchema = z.object({
  barber_id: z.string({ message: "Invalid barber ID format. Expected string." }),
  schedule_json: z.string({ message: "Missing schedule data." }),
});

const dayScheduleSchema = z.array(z.object({
  day_of_week: z.coerce.number().int().min(0).max(6),
  is_open: z.boolean(),
  starts_at: z.string().regex(/^[0-9]{2}:[0-9]{2}$/),
  ends_at: z.string().regex(/^[0-9]{2}:[0-9]{2}$/),
  slot_minutes: z.coerce.number().int().positive(),
}));

export async function saveWeeklyAvailability(formData: FormData) {
  const supabase = await getAdminSupabase();

  try {
    const rawBarberId = formData.get("barber_id")?.toString() ?? "";
    const rawSchedule = formData.get("schedule_json")?.toString() ?? "";

    const values = weeklyScheduleSchema.parse({
      barber_id: rawBarberId,
      schedule_json: rawSchedule,
    });

    const parsedJson = JSON.parse(values.schedule_json);
    const schedule = dayScheduleSchema.parse(parsedJson);

    const { error: deleteError } = await supabase
      .from("availability_rules")
      .delete()
      .eq("barber_id", values.barber_id);

    if (deleteError) {
      redirect(statusUrl("error", deleteError.message));
    }

    const payload = schedule
      .filter((day) => day.is_open)
      .map((day) => ({
        barber_id: values.barber_id,
        day_of_week: day.day_of_week,
        starts_at: day.starts_at,
        ends_at: day.ends_at,
        slot_minutes: day.slot_minutes,
        is_active: true,
      }));

    if (payload.length > 0) {
      const { error: insertError } = await supabase.from("availability_rules").insert(payload);

      if (insertError) {
        redirect(statusUrl("error", insertError.message));
      }
    }
  } catch (error: unknown) {
    if (error instanceof Error && error.message?.startsWith("DEBUG_INVALID_UUID:")) {
      redirect(statusUrl("error", error.message));
    }
    if (error instanceof SyntaxError) {
      redirect(statusUrl("error", "Invalid schedule format."));
    }
    if (error instanceof z.ZodError) {
      const message = error.issues[0]?.message ?? "Review the form values.";
      redirect(statusUrl("error", `Validation error: ${message}`));
    }
    throw error;
  }

  revalidatePath(catalogPath);
  redirect(statusUrl("success", "Weekly schedule saved."));
}

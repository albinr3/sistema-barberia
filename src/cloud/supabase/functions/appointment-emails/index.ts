import { serve } from "https://deno.land/std@0.177.0/http/server.ts";
import { createClient } from "https://esm.sh/@supabase/supabase-js@2.39.3";

type EmailType = "created" | "reminder_1h" | "cancelled" | "no_show";
type EmailJob = {
  id: string;
  appointment_id: string;
  email_type: EmailType;
  scheduled_for: string;
  attempt_count: number;
  max_attempts: number;
};

type Appointment = {
  id: string;
  customer_id: string;
  appointment_code: string;
  starts_at: string;
  ends_at: string;
  status: string;
  cancellation_reason: string | null;
  cancelled_at: string | null;
  no_show_at: string | null;
  service?: { name?: string } | Array<{ name?: string }>;
  barber?: { display_name?: string } | Array<{ display_name?: string }>;
};

type EmailPayload = {
  subject: string;
  html: string;
  text: string;
};

const TIME_ZONE = "America/New_York";

serve(async (req: Request) => {
  if (req.method !== "POST") {
    return new Response("Method not allowed", { status: 405 });
  }

  const internalSecret = Deno.env.get("APPOINTMENT_EMAIL_INTERNAL_SECRET");
  const authorization = req.headers.get("authorization") ?? "";
  const bearerSecret = authorization.startsWith("Bearer ")
    ? authorization.replace("Bearer ", "").trim()
    : "";
  const headerSecret = req.headers.get("x-internal-secret") ?? "";

  if (!internalSecret || (bearerSecret !== internalSecret && headerSecret !== internalSecret)) {
    return new Response("Unauthorized", { status: 401 });
  }

  const supabaseUrl = Deno.env.get("SUPABASE_URL");
  const serviceRoleKey = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY");
  const resendApiKey = Deno.env.get("RESEND_API_KEY");
  const emailFrom = Deno.env.get("APPOINTMENT_EMAIL_FROM");
  const publicSiteUrl = Deno.env.get("PUBLIC_SITE_URL");

  if (!supabaseUrl || !serviceRoleKey || !resendApiKey || !emailFrom || !publicSiteUrl) {
    return json({ error: "Missing required appointment email environment variables" }, 500);
  }

  const supabaseAdmin = createClient(supabaseUrl, serviceRoleKey);
  const body = await readJson(req);
  const limit = Math.min(Math.max(Number(body?.limit ?? 10), 1), 50);
  const now = new Date();

  await releaseStaleProcessingJobs(supabaseAdmin, now);

  const { data: jobs, error: jobsError } = await supabaseAdmin
    .from("appointment_email_jobs")
    .select("id, appointment_id, email_type, scheduled_for, attempt_count, max_attempts")
    .eq("status", "pending")
    .lte("scheduled_for", now.toISOString())
    .order("scheduled_for", { ascending: true })
    .order("created_at", { ascending: true })
    .limit(limit);

  if (jobsError) {
    return json({ error: jobsError.message }, 500);
  }

  const results = [];

  for (const job of (jobs ?? []) as EmailJob[]) {
    const result = await processJob({
      supabaseAdmin,
      resendApiKey,
      emailFrom,
      publicSiteUrl,
      job,
    });
    results.push(result);
  }

  return json({ processed: results.length, results });
});

async function processJob({
  supabaseAdmin,
  resendApiKey,
  emailFrom,
  publicSiteUrl,
  job,
}: {
  supabaseAdmin: ReturnType<typeof createClient>;
  resendApiKey: string;
  emailFrom: string;
  publicSiteUrl: string;
  job: EmailJob;
}) {
  const claimTime = new Date().toISOString();
  const { data: claimed, error: claimError } = await supabaseAdmin
    .from("appointment_email_jobs")
    .update({ status: "processing", last_attempt_at: claimTime })
    .eq("id", job.id)
    .eq("status", "pending")
    .select("id, appointment_id, email_type, scheduled_for, attempt_count, max_attempts")
    .maybeSingle();

  if (claimError) {
    return { job_id: job.id, status: "error", message: claimError.message };
  }

  if (!claimed) {
    return { job_id: job.id, status: "skipped", message: "Job was already claimed" };
  }

  const claimedJob = claimed as EmailJob;

  try {
    const appointment = await loadAppointment(supabaseAdmin, claimedJob.appointment_id);
    const sendCheck = shouldSendEmail(claimedJob.email_type, appointment);

    if (!sendCheck.send) {
      await markJobCancelled(supabaseAdmin, claimedJob.id, sendCheck.reason);
      return { job_id: claimedJob.id, status: "cancelled", message: sendCheck.reason };
    }

    const userEmail = await loadCustomerEmail(supabaseAdmin, appointment.customer_id);
    const email = buildAppointmentEmail(claimedJob.email_type, appointment, publicSiteUrl);
    const providerMessageId = await sendWithResend(resendApiKey, emailFrom, userEmail, email);

    await supabaseAdmin
      .from("appointment_email_jobs")
      .update({
        status: "sent",
        attempt_count: claimedJob.attempt_count + 1,
        sent_at: new Date().toISOString(),
        provider_message_id: providerMessageId,
        error_message: null,
      })
      .eq("id", claimedJob.id);

    return { job_id: claimedJob.id, status: "sent", provider_message_id: providerMessageId };
  } catch (error) {
    const message = error instanceof Error ? error.message : "Appointment email failed";
    await markJobFailedOrPending(supabaseAdmin, claimedJob, message);
    return { job_id: claimedJob.id, status: "error", message };
  }
}

async function loadAppointment(
  supabaseAdmin: ReturnType<typeof createClient>,
  appointmentId: string,
): Promise<Appointment> {
  const { data, error } = await supabaseAdmin
    .from("appointments")
    .select(`
      id,
      customer_id,
      appointment_code,
      starts_at,
      ends_at,
      status,
      cancellation_reason,
      cancelled_at,
      no_show_at,
      service:services(name),
      barber:barbers(display_name)
    `)
    .eq("id", appointmentId)
    .single();

  if (error || !data) {
    throw new Error("Appointment not found: " + (error?.message ?? appointmentId));
  }

  return data as Appointment;
}

async function loadCustomerEmail(
  supabaseAdmin: ReturnType<typeof createClient>,
  customerId: string,
) {
  const { data, error } = await supabaseAdmin.auth.admin.getUserById(customerId);

  if (error || !data?.user?.email) {
    throw new Error("Customer email was not found for appointment");
  }

  return data.user.email;
}

function shouldSendEmail(emailType: EmailType, appointment: Appointment) {
  if (emailType === "created") {
    return {
      send: appointment.status === "pending" || appointment.status === "confirmed",
      reason: "Appointment is no longer active",
    };
  }

  if (emailType === "reminder_1h") {
    return {
      send:
        (appointment.status === "pending" || appointment.status === "confirmed") &&
        new Date(appointment.starts_at) > new Date(),
      reason: "Appointment reminder is no longer valid",
    };
  }

  if (emailType === "cancelled") {
    return {
      send: appointment.status === "cancelled",
      reason: "Appointment is not cancelled",
    };
  }

  return {
    send: appointment.status === "no_show",
    reason: "Appointment is not marked as no-show",
  };
}

function buildAppointmentEmail(
  emailType: EmailType,
  appointment: Appointment,
  publicSiteUrl: string,
): EmailPayload {
  const detailUrl = joinUrl(publicSiteUrl, "/app/appointments");
  const logoUrl = joinUrl(publicSiteUrl, "/email/master-clips-logo.png");
  const serviceName = relationText(appointment.service, "name", "Selected service");
  const barberName = relationText(appointment.barber, "display_name", "Master Clips barber");
  const appointmentTime = formatAppointmentTime(appointment.starts_at);
  const appointmentCode = appointment.appointment_code || "Not assigned";
  const cancellationReason = appointment.cancellation_reason || "No reason was provided.";
  const qrUrl = `https://api.qrserver.com/v1/create-qr-code/?size=150x150&data=${encodeURIComponent(appointmentCode)}`;

  const copy = getEmailCopy(emailType, cancellationReason);
  const preheader = copy.preheader.replace("{time}", appointmentTime);

  const html = `<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <title>${escapeHtml(copy.subject)}</title>
  </head>
  <body style="margin:0;padding:0;background:#f5f7fb;color:#101322;font-family:Arial,Helvetica,sans-serif;">
    <div style="display:none;max-height:0;overflow:hidden;opacity:0;color:transparent;">${escapeHtml(preheader)}</div>
    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f5f7fb;margin:0;padding:32px 16px;">
      <tr>
        <td align="center">
          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#ffffff;border:1px solid #e1e6f0;border-radius:18px;overflow:hidden;box-shadow:0 18px 45px rgba(16,19,34,0.10);">
            <tr>
              <td style="padding:26px 28px 18px;text-align:center;background:#ffffff;">
                <img src="${escapeAttr(logoUrl)}" width="560" alt="Master Clips Barber Shop" style="display:block;width:100%;max-width:560px;height:auto;margin:0 auto;border:0;">
              </td>
            </tr>
            <tr>
              <td style="padding:8px 34px 0;">
                <div style="height:5px;background:linear-gradient(90deg,#071caa 0%,#e5161f 100%);border-radius:999px;"></div>
              </td>
            </tr>
            <tr>
              <td style="padding:30px 34px 12px;">
                <p style="margin:0 0 10px;color:#071caa;font-size:13px;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;">Master Clips Barber Shop</p>
                <h1 style="margin:0;color:#101322;font-size:30px;line-height:1.16;font-weight:800;">${escapeHtml(copy.heading)}</h1>
                <p style="margin:16px 0 0;color:#4b5265;font-size:16px;line-height:1.65;">${escapeHtml(copy.body)}</p>
              </td>
            </tr>
            <tr>
              <td style="padding:18px 34px;">
                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:separate;border-spacing:0;background:#f8faff;border:1px solid #dce3f4;border-radius:14px;">
                  ${detailRow("Service", serviceName)}
                  ${detailRow("Barber", barberName)}
                  ${detailRow("Date and time", appointmentTime)}
                  ${detailRow("Appointment code", appointmentCode)}
                  ${emailType === "cancelled" ? detailRow("Cancellation reason", cancellationReason) : ""}
                </table>
              </td>
            </tr>
            ${emailType !== "cancelled" ? `<tr>
              <td style="padding:18px 34px;text-align:center;">
                <p style="margin:0 0 10px;color:#4b5265;font-size:14px;font-weight:700;">Show this QR Code at the barbershop</p>
                <img src="${escapeAttr(qrUrl)}" alt="Appointment QR Code" width="150" height="150" style="display:inline-block;border:none;border-radius:10px;">
              </td>
            </tr>` : ""}
            <tr>
              <td style="padding:8px 34px 34px;">
                <a href="${escapeAttr(detailUrl)}" style="display:inline-block;background:#071caa;color:#ffffff;text-decoration:none;font-size:16px;font-weight:700;border-radius:10px;padding:15px 22px;">View appointment</a>
                <p style="margin:20px 0 0;color:#6d7485;font-size:13px;line-height:1.55;">If the button does not work, open this link: <a href="${escapeAttr(detailUrl)}" style="color:#071caa;text-decoration:underline;">${escapeHtml(detailUrl)}</a></p>
              </td>
            </tr>
          </table>
          <p style="margin:18px 0 0;color:#858b99;font-size:12px;line-height:1.5;">This is a transactional appointment email from Master Clips Barber Shop.</p>
        </td>
      </tr>
    </table>
  </body>
</html>`;

  const text = [
    "Master Clips Barber Shop",
    "",
    copy.heading,
    copy.body,
    "",
    `Service: ${serviceName}`,
    `Barber: ${barberName}`,
    `Date and time: ${appointmentTime}`,
    `Appointment code: ${appointmentCode}`,
    ...(emailType === "cancelled" ? [`Cancellation reason: ${cancellationReason}`] : []),
    "",
    `View your appointment: ${detailUrl}`,
  ].join("\n");

  return { subject: copy.subject, html, text };
}

function getEmailCopy(emailType: EmailType, cancellationReason: string) {
  if (emailType === "created") {
    return {
      subject: "Your appointment is confirmed",
      heading: "Your appointment is confirmed",
      body: "We have your appointment reserved. Please arrive a few minutes early so we can check you in smoothly.",
      preheader: "Your Master Clips appointment is confirmed for {time}.",
    };
  }

  if (emailType === "reminder_1h") {
    return {
      subject: "Your appointment starts in 1 hour",
      heading: "Your appointment starts in 1 hour",
      body: "This is a friendly reminder that your appointment is coming up soon. We will be ready for you.",
      preheader: "Your Master Clips appointment starts at {time}.",
    };
  }

  if (emailType === "cancelled") {
    return {
      subject: "Your appointment was cancelled",
      heading: "Your appointment was cancelled",
      body: `Your appointment has been cancelled. Reason: ${cancellationReason}`,
      preheader: "Your Master Clips appointment was cancelled.",
    };
  }

  return {
    subject: "We missed you at your appointment",
    heading: "We missed you at your appointment",
    body: "Your appointment was marked as missed because we did not check you in during the appointment window.",
    preheader: "Your Master Clips appointment was marked as missed.",
  };
}

function detailRow(label: string, value: string) {
  return `<tr>
    <td style="padding:14px 16px;border-bottom:1px solid #e3e8f4;color:#6d7485;font-size:13px;font-weight:700;text-transform:uppercase;letter-spacing:0.04em;width:36%;">${escapeHtml(label)}</td>
    <td style="padding:14px 16px;border-bottom:1px solid #e3e8f4;color:#101322;font-size:15px;font-weight:700;">${escapeHtml(value)}</td>
  </tr>`;
}

async function sendWithResend(
  resendApiKey: string,
  emailFrom: string,
  to: string,
  email: EmailPayload,
) {
  const response = await fetch("https://api.resend.com/emails", {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${resendApiKey}`,
    },
    body: JSON.stringify({
      from: emailFrom,
      to: [to],
      subject: email.subject,
      html: email.html,
      text: email.text,
    }),
  });

  const responseText = await response.text();
  let responseJson: { id?: string; message?: string; error?: string } = {};
  try {
    responseJson = responseText ? JSON.parse(responseText) : {};
  } catch {
    responseJson = { message: responseText };
  }

  if (!response.ok) {
    throw new Error(responseJson.message || responseJson.error || "Resend email request failed");
  }

  return responseJson.id ?? null;
}

async function releaseStaleProcessingJobs(
  supabaseAdmin: ReturnType<typeof createClient>,
  now: Date,
) {
  const staleBefore = new Date(now.getTime() - 10 * 60 * 1000).toISOString();
  await supabaseAdmin
    .from("appointment_email_jobs")
    .update({ status: "pending", error_message: "Recovered stale processing job" })
    .eq("status", "processing")
    .lt("last_attempt_at", staleBefore);
}

async function markJobCancelled(
  supabaseAdmin: ReturnType<typeof createClient>,
  jobId: string,
  reason: string,
) {
  await supabaseAdmin
    .from("appointment_email_jobs")
    .update({ status: "cancelled", error_message: reason })
    .eq("id", jobId);
}

async function markJobFailedOrPending(
  supabaseAdmin: ReturnType<typeof createClient>,
  job: EmailJob,
  message: string,
) {
  const attemptCount = job.attempt_count + 1;
  const exhausted = attemptCount >= job.max_attempts;
  const retryDelaySeconds = Math.min(60 * 2 ** Math.max(attemptCount - 1, 0), 3600);
  const retryAt = new Date(Date.now() + retryDelaySeconds * 1000).toISOString();

  await supabaseAdmin
    .from("appointment_email_jobs")
    .update({
      status: exhausted ? "failed" : "pending",
      attempt_count: attemptCount,
      scheduled_for: exhausted ? job.scheduled_for : retryAt,
      error_message: message,
      last_attempt_at: new Date().toISOString(),
    })
    .eq("id", job.id);
}

function relationText(
  relation: Appointment["service"] | Appointment["barber"],
  property: string,
  fallback: string,
) {
  const record = Array.isArray(relation) ? relation[0] : relation;
  if (!record || typeof record !== "object") return fallback;
  const value = (record as Record<string, unknown>)[property];
  return typeof value === "string" && value.trim().length > 0 ? value.trim() : fallback;
}

function formatAppointmentTime(value: string) {
  return new Intl.DateTimeFormat("en-US", {
    timeZone: TIME_ZONE,
    weekday: "long",
    month: "long",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    timeZoneName: "short",
  }).format(new Date(value));
}

function joinUrl(baseUrl: string, path: string) {
  return `${baseUrl.replace(/\/+$/, "")}/${path.replace(/^\/+/, "")}`;
}

function escapeHtml(value: string) {
  return value
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function escapeAttr(value: string) {
  return escapeHtml(value);
}

async function readJson(req: Request) {
  try {
    const text = await req.text();
    return text ? JSON.parse(text) : null;
  } catch {
    return null;
  }
}

function json(body: Record<string, unknown>, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

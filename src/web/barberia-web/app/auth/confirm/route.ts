import { type EmailOtpType } from "@supabase/supabase-js";
import { NextResponse, type NextRequest } from "next/server";
import { createClient } from "@/lib/supabase/server";

export async function GET(request: NextRequest) {
  const url = new URL(request.url);
  const code = url.searchParams.get("code");
  const tokenHash = url.searchParams.get("token_hash");
  const type = (url.searchParams.get("type") ?? "email") as EmailOtpType;
  const next = safeNextPath(url.searchParams.get("next"));
  const redirectTo = new URL(next, url.origin);
  const supabase = await createClient();

  if (code) {
    const { error } = await supabase.auth.exchangeCodeForSession(code);

    if (!error) {
      if (next === "/") {
        redirectTo.search = "?confirmed=1";
      }
      return NextResponse.redirect(redirectTo);
    }
  }

  if (tokenHash) {
    const { error } = await supabase.auth.verifyOtp({
      token_hash: tokenHash,
      type,
    });

    if (!error) {
      if (next === "/") {
        redirectTo.search = "?confirmed=1";
      }
      return NextResponse.redirect(redirectTo);
    }
  }

  redirectTo.pathname = "/";
  redirectTo.search = "?confirmed=error";
  return NextResponse.redirect(redirectTo);
}

function safeNextPath(next: string | null) {
  if (!next || !next.startsWith("/") || next.startsWith("//")) {
    return "/";
  }

  return next;
}

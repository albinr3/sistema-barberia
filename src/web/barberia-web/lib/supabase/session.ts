import { NextResponse, type NextRequest } from "next/server";
import { createServerClient } from "@supabase/ssr";
import { appRoles, type AppRole } from "@/types/roles";
import { allowedRolesForPath, isProtectedPath, roleHomePath } from "@/lib/routes";
import { getSupabasePublishableKey, getSupabaseUrl, hasSupabaseConfig } from "@/lib/supabase/config";

export async function updateSession(request: NextRequest) {
  const isProtected = isProtectedPath(request.nextUrl.pathname);
  if (!hasSupabaseConfig()) {
    if (isProtected) {
      const redirectUrl = request.nextUrl.clone();
      redirectUrl.pathname = "/";
      redirectUrl.searchParams.set("next", request.nextUrl.pathname);
      return NextResponse.redirect(redirectUrl);
    }

    return NextResponse.next({ request });
  }

  let response = NextResponse.next({
    request,
  });

  const supabase = createServerClient(
    getSupabaseUrl()!,
    getSupabasePublishableKey()!,
    {
      cookies: {
        getAll() {
          return request.cookies.getAll();
        },
        setAll(cookiesToSet) {
          cookiesToSet.forEach(({ name, value }) => request.cookies.set(name, value));
          response = NextResponse.next({ request });
          cookiesToSet.forEach(({ name, value, options }) => {
            response.cookies.set(name, value, options);
          });
        },
      },
    },
  );

  const {
    data: { user },
  } = await supabase.auth.getUser();

  if (!user && isProtected) {
    const redirectUrl = request.nextUrl.clone();
    redirectUrl.pathname = "/";
    redirectUrl.searchParams.set("next", request.nextUrl.pathname);
    return NextResponse.redirect(redirectUrl);
  }

  const allowedRoles = allowedRolesForPath(request.nextUrl.pathname);

  if (user && allowedRoles) {
    const { data } = await supabase
      .from("profiles")
      .select("role")
      .eq("id", user.id)
      .maybeSingle<{ role: string }>();

    const role = appRoles.includes(data?.role as AppRole) ? (data?.role as AppRole) : "customer";

    if (!allowedRoles.includes(role)) {
      const redirectUrl = request.nextUrl.clone();
      redirectUrl.pathname = roleHomePath(role);
      redirectUrl.search = "";
      return NextResponse.redirect(redirectUrl);
    }
  }

  return response;
}

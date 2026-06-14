import type { AppRole } from "@/types/roles";

export const protectedRoutes = ["/app", "/barber", "/admin"] as const;
export const adminRoutes = ["/admin"] as const;
export const adminRoles: AppRole[] = ["admin", "owner"];
export const customerRoles: AppRole[] = ["customer"];
export const barberRoles: AppRole[] = ["barber"];

export function isProtectedPath(pathname: string) {
  return protectedRoutes.some((route) => pathname === route || pathname.startsWith(`${route}/`));
}

export function isAdminPath(pathname: string) {
  return adminRoutes.some((route) => pathname === route || pathname.startsWith(`${route}/`));
}

export function canUseAdmin(role: AppRole) {
  return adminRoles.includes(role);
}

export function allowedRolesForPath(pathname: string): AppRole[] | null {
  if (isAdminPath(pathname)) {
    return adminRoles;
  }

  if (pathname === "/barber" || pathname.startsWith("/barber/")) {
    return barberRoles;
  }

  if (pathname === "/app" || pathname.startsWith("/app/")) {
    return customerRoles;
  }

  return null;
}

export function roleHomePath(role: AppRole) {
  switch (role) {
    case "owner":
    case "admin":
      return "/admin";
    case "barber":
      return "/barber";
    case "customer":
    default:
      return "/app/book";
  }
}

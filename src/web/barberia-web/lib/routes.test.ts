import { describe, expect, it } from "vitest";
import { allowedRolesForPath, isProtectedPath, roleHomePath } from "./routes";

describe("web route access rules", () => {
  it("protects authenticated app surfaces", () => {
    expect(isProtectedPath("/app/book")).toBe(true);
    expect(isProtectedPath("/barber/settings")).toBe(true);
    expect(isProtectedPath("/admin/admin-dashboard")).toBe(true);
    expect(isProtectedPath("/tickets-dashboard")).toBe(true);
    expect(isProtectedPath("/auth/reset-password")).toBe(false);
  });

  it("maps internal surfaces to explicit role allowlists", () => {
    expect(allowedRolesForPath("/app/book")).toEqual(["customer"]);
    expect(allowedRolesForPath("/barber/settings")).toEqual(["barber"]);
    expect(allowedRolesForPath("/admin/sync")).toEqual(["admin", "owner"]);
    expect(allowedRolesForPath("/tickets-dashboard")).toEqual(["admin", "owner"]);
    expect(allowedRolesForPath("/auth/reset-password")).toBeNull();
  });

  it("redirects roles to their home surfaces", () => {
    expect(roleHomePath("customer")).toBe("/app/book");
    expect(roleHomePath("barber")).toBe("/barber");
    expect(roleHomePath("admin")).toBe("/admin");
    expect(roleHomePath("owner")).toBe("/admin");
  });
});

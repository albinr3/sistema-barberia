export const appRoles = ["customer", "barber", "admin", "owner"] as const;

export type AppRole = (typeof appRoles)[number];

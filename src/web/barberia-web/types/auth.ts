import type { AppRole } from "./roles";

export type SessionProfile = {
  id: string;
  role: AppRole;
  displayName: string | null;
};

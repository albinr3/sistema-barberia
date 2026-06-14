"use client";

import { LogOut } from "lucide-react";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { createClient } from "@/lib/supabase/browser";
import styles from "./app-shell.module.css";

export function LogoutButton() {
  const [isSigningOut, setIsSigningOut] = useState(false);

  async function handleLogout() {
    setIsSigningOut(true);
    const supabase = createClient();
    await supabase.auth.signOut();
    window.location.assign("/");
  }

  return (
    <Button
      className={styles.logoutButton}
      type="button"
      variant="secondary"
      onClick={handleLogout}
      disabled={isSigningOut}
    >
      <LogOut size={18} aria-hidden="true" />
      {isSigningOut ? "Logging out..." : "Logout"}
    </Button>
  );
}

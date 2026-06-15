"use client";

import { useState } from "react";
import { dismissSyncConflict } from "@/app/actions/admin-sync";
import { CheckCircle2 } from "lucide-react";

export function DismissConflictButton({ conflictId }: { conflictId: string }) {
  const [loading, setLoading] = useState(false);

  const handleDismiss = async () => {
    if (!confirm("Are you sure you want to dismiss this conflict?")) return;
    setLoading(true);
    const result = await dismissSyncConflict(conflictId);
    setLoading(false);
    if (result.error) {
      alert("Error: " + result.error);
    }
  };

  return (
    <button 
      onClick={handleDismiss} 
      disabled={loading}
      style={{
        display: "flex",
        alignItems: "center",
        gap: "0.25rem",
        background: "transparent",
        border: "none",
        color: "var(--success)",
        fontWeight: 600,
        fontSize: "0.875rem",
        cursor: "pointer",
        padding: "0.25rem 0.5rem",
        borderRadius: "var(--radius-sm)",
        transition: "background 0.2s"
      }}
      title="Clear conflict"
    >
      <CheckCircle2 size={16} /> {loading ? "Clearing..." : "Clear"}
    </button>
  );
}

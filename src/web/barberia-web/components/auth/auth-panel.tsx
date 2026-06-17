"use client";

import { useState } from "react";
import Image from "next/image";
import { LoginForm } from "./login-form";
import { RegisterForm } from "./register-form";
import styles from "./auth-panel.module.css";
import logo from "../../logo(2).png";

type AuthMode = "login" | "register";

type AuthPanelProps = {
  initialError?: string | null;
  initialNotice?: string | null;
};

export function AuthPanel({ initialError = null, initialNotice = null }: AuthPanelProps) {
  const [mode, setMode] = useState<AuthMode>("login");

  return (
    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', width: '100%' }}>
      <div style={{ marginBottom: "24px" }}>
        <Image src={logo} alt="Logo" style={{ height: "160px", width: "auto" }} priority />
      </div>
      <div className={styles.panel}>
        <div className={styles.header}>
          <p>Secure access</p>
          <h2>{mode === "login" ? "Welcome back" : "Create your account"}</h2>
        </div>
      <div className={styles.segmented} role="tablist" aria-label="Authentication mode">
        <button
          type="button"
          role="tab"
          aria-selected={mode === "login"}
          data-active={mode === "login"}
          onClick={() => setMode("login")}
        >
          Log in
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={mode === "register"}
          data-active={mode === "register"}
          onClick={() => setMode("register")}
        >
          Register
        </button>
      </div>
      {initialNotice ? <p className={styles.notice}>{initialNotice}</p> : null}
      {initialError ? <p className={styles.error}>{initialError}</p> : null}
      {mode === "login" ? <LoginForm /> : <RegisterForm />}
    </div>
    </div>
  );
}

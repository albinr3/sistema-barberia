import { AuthShell } from "@/components/auth/auth-shell";
import { PasswordResetRequestForm } from "@/components/auth/password-reset-request-form";
import styles from "./reset-password.module.css";

export default function ResetPasswordPage() {
  return (
    <AuthShell>
      <div className={styles.panel}>
        <p>Account recovery</p>
        <h2>Reset password</h2>
        <span>
          Enter your email and we will send a secure link to create a new password.
        </span>
        <PasswordResetRequestForm />
      </div>
    </AuthShell>
  );
}

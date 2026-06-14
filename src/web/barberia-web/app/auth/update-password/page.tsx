import { AuthShell } from "@/components/auth/auth-shell";
import { UpdatePasswordForm } from "@/components/auth/update-password-form";
import styles from "../reset-password/reset-password.module.css";

export default function UpdatePasswordPage() {
  return (
    <AuthShell>
      <div className={styles.panel}>
        <p>Account verified</p>
        <h2>New password</h2>
        <span>Save a new password to finish account recovery.</span>
        <UpdatePasswordForm />
      </div>
    </AuthShell>
  );
}

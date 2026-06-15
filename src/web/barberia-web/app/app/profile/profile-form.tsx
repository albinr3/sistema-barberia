"use client";

import { useState, useTransition } from "react";
import { updateProfile } from "@/app/actions/profile";
import styles from "./profile-form.module.css";

type ProfileFormProps = {
  initialData: {
    displayName: string | null;
    email: string | null;
    phone: string | null;
  };
};

const MailIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.inputIcon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect width="20" height="16" x="2" y="4" rx="2"/><path d="m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7"/></svg>
);

const UserIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.inputIcon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/></svg>
);

const PhoneIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.inputIcon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"/></svg>
);

const CheckIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.messageIcon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><polyline points="20 6 9 17 4 12"/></svg>
);

const AlertIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" className={styles.messageIcon} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" x2="12" y1="8" y2="12"/><line x1="12" x2="12.01" y1="16" y2="16"/></svg>
);

const SaveIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"/><polyline points="17 21 17 13 7 13 7 21"/><polyline points="7 3 7 8 15 8"/></svg>
);

const LoaderIcon = () => (
  <svg xmlns="http://www.w3.org/2000/svg" width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" style={{ animation: "spin 1s linear infinite" }}><path d="M21 12a9 9 0 1 1-6.219-8.56"/></svg>
);

export function ProfileForm({ initialData }: ProfileFormProps) {
  const [isPending, startTransition] = useTransition();
  const [message, setMessage] = useState<{ type: "success" | "error"; text: string } | null>(null);

  async function action(formData: FormData) {
    setMessage(null);
    startTransition(async () => {
      const result = await updateProfile(formData);
      if (result.error) {
        setMessage({ type: "error", text: result.error });
      } else {
        setMessage({ type: "success", text: "Perfil actualizado correctamente." });
      }
    });
  }

  // Get first letter of name for avatar
  const avatarLetter = (initialData.displayName || initialData.email || "U").charAt(0).toUpperCase();

  return (
    <div className={styles.wrapper}>
      <div className={styles.container}>
        
        <div className={styles.header}>
          <div className={styles.avatar}>
            {avatarLetter}
          </div>
          <div className={styles.headerText}>
            <h2 className={styles.title}>Perfil de Cuenta</h2>
            <p className={styles.subtitle}>Gestiona tu información personal y de contacto</p>
          </div>
        </div>

        <form action={action} className={styles.form}>
          <div className={styles.fieldsGrid}>
            <div className={`${styles.field} ${styles.fullWidth}`}>
              <label htmlFor="email" className={styles.label}>
                Correo electrónico
              </label>
              <div className={styles.inputWrapper}>
                <MailIcon />
                <input
                  id="email"
                  name="email"
                  type="email"
                  defaultValue={initialData.email || ""}
                  disabled
                  className={styles.input}
                  title="El correo no se puede modificar"
                />
              </div>
            </div>

            <div className={styles.field}>
              <label htmlFor="displayName" className={styles.label}>
                Nombre completo
              </label>
              <div className={styles.inputWrapper}>
                <UserIcon />
                <input
                  id="displayName"
                  name="displayName"
                  type="text"
                  required
                  defaultValue={initialData.displayName || ""}
                  className={styles.input}
                  placeholder="Tu nombre"
                />
              </div>
            </div>

            <div className={styles.field}>
              <label htmlFor="phone" className={styles.label}>
                Número de teléfono
              </label>
              <div className={styles.inputWrapper}>
                <PhoneIcon />
                <input
                  id="phone"
                  name="phone"
                  type="tel"
                  defaultValue={initialData.phone || ""}
                  className={styles.input}
                  placeholder="Tu número (ej. +12345678)"
                />
              </div>
            </div>
          </div>

          {message && (
            <div className={`${styles.message} ${styles[message.type]}`}>
              {message.type === "success" ? <CheckIcon /> : <AlertIcon />}
              <span>{message.text}</span>
            </div>
          )}

          <div className={styles.footer}>
            <button type="submit" disabled={isPending} className={styles.submitBtn}>
              {isPending ? (
                <>
                  <LoaderIcon /> Guardando...
                </>
              ) : (
                <>
                  <SaveIcon /> Guardar cambios
                </>
              )}
            </button>
          </div>
        </form>
      </div>
      
      <style dangerouslySetInnerHTML={{__html: `
        @keyframes spin { 100% { transform: rotate(360deg); } }
      `}} />
    </div>
  );
}

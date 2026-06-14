"use client";

import { KeyRound } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { createClient } from "@/lib/supabase/browser";
import styles from "./forms.module.css";

const schema = z.object({
  email: z.string().email("Enter a valid email."),
});

type PasswordResetRequestFields = z.infer<typeof schema>;

export function PasswordResetRequestForm() {
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<PasswordResetRequestFields>({
    resolver: zodResolver(schema),
  });

  async function onSubmit(values: PasswordResetRequestFields) {
    setMessage(null);
    setIsError(false);

    const supabase = createClient();
    const redirectTo = `${window.location.origin}/auth/confirm?next=/auth/update-password`;
    const { error } = await supabase.auth.resetPasswordForEmail(values.email, {
      redirectTo,
    });

    if (error) {
      setIsError(true);
      setMessage(error.message);
      return;
    }

    setMessage("Check your email to continue with the password change.");
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit(onSubmit)}>
      <label>
        Email
        <input autoComplete="email" type="email" {...register("email")} />
        {errors.email ? <span>{errors.email.message}</span> : null}
      </label>
      {message ? <p className={isError ? styles.error : styles.notice}>{message}</p> : null}
      <Button type="submit" disabled={isSubmitting}>
        <KeyRound size={18} aria-hidden="true" />
        {isSubmitting ? "Sending..." : "Send link"}
      </Button>
    </form>
  );
}

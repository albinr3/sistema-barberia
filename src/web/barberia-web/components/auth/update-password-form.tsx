"use client";

import { Save } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { createClient } from "@/lib/supabase/browser";
import styles from "./forms.module.css";

const schema = z
  .object({
    password: z.string().min(8, "Use at least 8 characters."),
    confirmPassword: z.string().min(8, "Confirm password."),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: "Passwords do not match.",
    path: ["confirmPassword"],
  });

type UpdatePasswordFields = z.infer<typeof schema>;

export function UpdatePasswordForm() {
  const [message, setMessage] = useState<string | null>(null);
  const [isError, setIsError] = useState(false);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<UpdatePasswordFields>({
    resolver: zodResolver(schema),
  });

  async function onSubmit(values: UpdatePasswordFields) {
    setMessage(null);
    setIsError(false);

    const supabase = createClient();
    const { error } = await supabase.auth.updateUser({
      password: values.password,
    });

    if (error) {
      setIsError(true);
      setMessage(error.message);
      return;
    }

    setMessage("Password updated. You can now log in.");
    window.setTimeout(() => window.location.assign("/"), 1200);
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit(onSubmit)}>
      <label>
        New password
        <input autoComplete="new-password" type="password" {...register("password")} />
        {errors.password ? <span>{errors.password.message}</span> : null}
      </label>
      <label>
        Confirm password
        <input autoComplete="new-password" type="password" {...register("confirmPassword")} />
        {errors.confirmPassword ? <span>{errors.confirmPassword.message}</span> : null}
      </label>
      {message ? <p className={isError ? styles.error : styles.notice}>{message}</p> : null}
      <Button type="submit" disabled={isSubmitting}>
        <Save size={18} aria-hidden="true" />
        {isSubmitting ? "Saving..." : "Save password"}
      </Button>
    </form>
  );
}

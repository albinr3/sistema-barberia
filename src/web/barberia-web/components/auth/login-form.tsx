"use client";

import { LogIn } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { createClient } from "@/lib/supabase/browser";
import styles from "./forms.module.css";

const schema = z.object({
  email: z.string().email("Enter a valid email."),
  password: z.string().min(1, "Password is required."),
});

type LoginFormFields = z.infer<typeof schema>;

export function LoginForm() {
  const [message, setMessage] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginFormFields>({
    resolver: zodResolver(schema),
  });

  async function onSubmit(values: LoginFormFields) {
    setMessage(null);
    const supabase = createClient();
    const { error } = await supabase.auth.signInWithPassword(values);

    if (error) {
      setMessage(error.message);
      return;
    }

    window.location.assign("/app");
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit(onSubmit)}>
      <label>
        Email
        <input autoComplete="email" type="email" {...register("email")} />
        {errors.email ? <span>{errors.email.message}</span> : null}
      </label>
      <label>
        Password
        <input autoComplete="current-password" type="password" {...register("password")} />
        {errors.password ? <span>{errors.password.message}</span> : null}
      </label>
      {message ? <p className={styles.error}>{message}</p> : null}
      <a className={styles.link} href="/auth/reset-password">
        Forgot my password
      </a>
      <Button type="submit" disabled={isSubmitting}>
        <LogIn size={18} aria-hidden="true" />
        {isSubmitting ? "Logging in..." : "Log in"}
      </Button>
    </form>
  );
}

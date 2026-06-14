"use client";

import { UserPlus } from "lucide-react";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { createClient } from "@/lib/supabase/browser";
import styles from "./forms.module.css";

const schema = z.object({
  displayName: z.string().min(2, "Enter your name."),
  email: z.string().email("Enter a valid email."),
  password: z.string().min(8, "Use at least 8 characters."),
});

type RegisterFormFields = z.infer<typeof schema>;

export function RegisterForm() {
  const [message, setMessage] = useState<string | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormFields>({
    resolver: zodResolver(schema),
  });

  async function onSubmit(values: RegisterFormFields) {
    setMessage(null);
    const supabase = createClient();
    const { error } = await supabase.auth.signUp({
      email: values.email,
      password: values.password,
      options: {
        emailRedirectTo: `${window.location.origin}/auth/confirm`,
        data: {
          display_name: values.displayName,
        },
      },
    });

    if (error) {
      setMessage(error.message);
      return;
    }

    setMessage("Check your email to verify your account.");
  }

  return (
    <form className={styles.form} onSubmit={handleSubmit(onSubmit)}>
      <label>
        Full Name
        <input autoComplete="name" type="text" {...register("displayName")} />
        {errors.displayName ? <span>{errors.displayName.message}</span> : null}
      </label>
      <label>
        Email
        <input autoComplete="email" type="email" {...register("email")} />
        {errors.email ? <span>{errors.email.message}</span> : null}
      </label>
      <label>
        Password
        <input autoComplete="new-password" type="password" {...register("password")} />
        {errors.password ? <span>{errors.password.message}</span> : null}
      </label>
      {message ? <p className={styles.notice}>{message}</p> : null}
      <Button type="submit" disabled={isSubmitting}>
        <UserPlus size={18} aria-hidden="true" />
        {isSubmitting ? "Creating..." : "Create account"}
      </Button>
    </form>
  );
}

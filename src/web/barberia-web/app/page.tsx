import { AuthPanel } from "@/components/auth/auth-panel";
import { AuthShell } from "@/components/auth/auth-shell";

type HomePageProps = {
  searchParams: Promise<{
    confirmed?: string;
  }>;
};

export default async function HomePage({ searchParams }: HomePageProps) {
  const { confirmed } = await searchParams;
  const initialNotice = confirmed === "1" ? "User confirmed. You can now log in." : null;
  const initialError =
    confirmed === "error" ? "Could not confirm user. Try opening the link again." : null;

  return (
    <AuthShell>
      <AuthPanel initialNotice={initialNotice} initialError={initialError} />
    </AuthShell>
  );
}

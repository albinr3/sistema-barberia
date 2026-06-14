import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Barbershop Web System",
  description: "Authenticated portal for bookings, barbers, and barbershop administration.",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>{children}</body>
    </html>
  );
}

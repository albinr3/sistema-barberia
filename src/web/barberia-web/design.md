---
name: Sistema Barberia Web
colors:
  surface: '#f9f9fc'
  surface-dim: '#dadadc'
  surface-bright: '#f9f9fc'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#f3f3f6'
  surface-container: '#eeeef0'
  surface-container-high: '#e8e8ea'
  surface-container-highest: '#e2e2e5'
  on-surface: '#1a1c1e'
  on-surface-variant: '#444655'
  inverse-surface: '#2f3133'
  inverse-on-surface: '#f0f0f3'
  outline: '#757687'
  outline-variant: '#c5c5d8'
  surface-tint: '#374be0'
  primary: '#001387'
  on-primary: '#ffffff'
  primary-container: '#0020c2'
  on-primary-container: '#98a3ff'
  inverse-primary: '#bcc2ff'
  secondary: '#bb001f'
  on-secondary: '#ffffff'
  secondary-container: '#e5182e'
  on-secondary-container: '#fffbff'
  tertiary: '#27292a'
  on-tertiary: '#ffffff'
  tertiary-container: '#3d3f40'
  on-tertiary-container: '#a8aaab'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#dfe0ff'
  primary-fixed-dim: '#bcc2ff'
  on-primary-fixed: '#000b62'
  on-primary-fixed-variant: '#152dc9'
  secondary-fixed: '#ffdad7'
  secondary-fixed-dim: '#ffb3ae'
  on-secondary-fixed: '#410005'
  on-secondary-fixed-variant: '#930016'
  tertiary-fixed: '#e1e3e4'
  tertiary-fixed-dim: '#c5c7c8'
  on-tertiary-fixed: '#191c1d'
  on-tertiary-fixed-variant: '#454748'
  background: '#f9f9fc'
  on-background: '#1a1c1e'
  surface-variant: '#e2e2e5'
typography:
  display-lg:
    fontFamily: Inter
    fontSize: 48px
    fontWeight: '700'
    lineHeight: 56px
    letterSpacing: -0.02em
  headline-lg:
    fontFamily: Inter
    fontSize: 32px
    fontWeight: '700'
    lineHeight: 40px
    letterSpacing: -0.01em
  headline-lg-mobile:
    fontFamily: Inter
    fontSize: 28px
    fontWeight: '700'
    lineHeight: 36px
  headline-md:
    fontFamily: Inter
    fontSize: 24px
    fontWeight: '600'
    lineHeight: 32px
  title-lg:
    fontFamily: Inter
    fontSize: 20px
    fontWeight: '600'
    lineHeight: 28px
  body-lg:
    fontFamily: Inter
    fontSize: 18px
    fontWeight: '400'
    lineHeight: 28px
  body-md:
    fontFamily: Inter
    fontSize: 16px
    fontWeight: '400'
    lineHeight: 24px
  label-md:
    fontFamily: Inter
    fontSize: 14px
    fontWeight: '500'
    lineHeight: 20px
    letterSpacing: 0.01em
  label-sm:
    fontFamily: Inter
    fontSize: 12px
    fontWeight: '600'
    lineHeight: 16px
    letterSpacing: 0.05em
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  base: 8px
  container-max: 1200px
  gutter: 24px
  margin-mobile: 16px
  margin-desktop: 48px
---

## Brand & Style

This design system establishes a high-end, approachable grooming identity tailored for **Sistema Barberia Web** (Fase 2). It blends the classic heritage of the barber trade with a clean, contemporary aesthetic. The goal is to evoke a sense of precision, cleanliness, and premium service across all web modules: Booking de Clientes, Panel de Barberos y Dashboard Administrativo.

The design style is **Corporate / Modern** with a slight editorial edge. It prioritizes clarity and utility, ensuring a frictionless booking experience while maintaining a "fresh" atmosphere through generous whitespace and crisp typography. By using the patriotic barber pole colors as functional accents rather than dominant washes, the interface remains light and airy, avoiding the visual heaviness often associated with traditional barber branding.

## Colors

The palette is derived directly from the classic barber pole iconography found in the logo. 

- **Primary (Royal Blue):** Used for primary actions (e.g. "Reservar", "Iniciar Servicio"), active states, and navigation highlights. It represents trust and professionalism.
- **Secondary (Crimson Red):** Used sparingly as an accent for urgent notifications, cancelaciones de tickets, or unique call-to-action details to provide visual "pop".
- **Backgrounds:** A tiered system of white (`#FFFFFF`) and light gray (`#F3F3F6`) ensures the interface feels spacious and clinical.
- **Typography:** Deep charcoal (`#1A1C1E`) is used for text to ensure high legibility while appearing softer and more modern than pure black.

## Typography

This design system utilizes **Inter** across all levels to maintain a systematic, utilitarian feel. The hierarchy relies on substantial weight differences and tight letter-spacing for headlines to create a "locked-in" professional look.

For marketing surfaces and el Booking Web, `display-lg` should be used with tight tracking to mimic editorial headlines. For the booking flow, service lists, and Data Tables in Admin Panel, `body-md` provides the foundation for readability. The `label-sm` in all-caps is reserved for metadata like "ESTIMATED TIME", "TICKET NUMBER", "STATION CODE" or "STATUS".

## Layout & Spacing

The design system uses a **Fixed Grid** model for desktop and a fluid model for mobile devices. Next.js components should strictly follow this responsiveness.

- **Grid:** A 12-column grid is used for desktop (1200px max width) to organize service menus, barber grids, and admin tables.
- **Rhythm:** An 8px linear scale governs all padding and margins. 
- **Desktop:** Generous 48px outer margins create a "framed" feel that highlights content (e.g. en el Dashboard Web).
- **Mobile:** Margins compress to 16px to maximize screen real estate, with 16px gutters between cards (ideal para clientes reservando desde su celular).

## Elevation & Depth

To maintain a clean and airy aesthetic, the design system utilizes **Tonal Layers** and **Low-Contrast Outlines** instead of heavy shadows.

- **Level 0 (Base):** Pure white background for the main canvas.
- **Level 1 (Cards/Surface):** Light gray (`#F9F9FC`) surfaces or containers with a 1px solid border (`#E8E8EA`).
- **Level 2 (Interactive):** When hovered or focused, elements receive a soft, ultra-diffused blue-tinted shadow (4px blur, 2% opacity) to suggest lift without breaking the "flat" modern aesthetic.
- **Dividers:** Use thin 1px lines in `#E2E2E5` to separate list items (e.g., ticket history, barber services list).

## Shapes

The design system employs a **Rounded (8px)** corner strategy. This provides a soft, welcoming feel that balances the sharp precision of the barber tools and UI accuracy.

- **Standard Elements:** Buttons, input fields, and small cards use the 8px (0.5rem) radius.
- **Large Elements:** Featured promo sections, modales or profile headers use 16px (1rem) radius.
- **Interactive States:** Toggle switches (ej. Barbero Activo/Inactivo) and selection chips should remain pill-shaped to distinguish them from structural components.

## Components

### Buttons
- **Primary:** Solid Royal Blue (`#0020C2`) with white text. High emphasis. Used for "Book", "Save", "Check-in".
- **Secondary:** White background with a 1.5px Royal Blue border and Blue text.
- **Ghost:** Transparent background with charcoal text; used for secondary actions like "Cancel".

### Input Fields
- Use a light gray background (`#F3F3F6`) with a bottom-only border that turns Royal Blue on focus. Labels should sit above the field in `label-md`.

### Cards
- Service cards feature a 1px border and 8px corner radius. On hover, the border color shifts from gray to Royal Blue. Prices (Base Prices) are highlighted in `title-lg` Bold.
- Barber cards show `station_number` or `station_code` prominently.

### Chips & Badges
- **Status Badges:** "Available" or "En Servicio" uses a light blue tint with darker blue text; "Waiting" or "Offline" adjust color respectively.
- **Tags:** Service tags (e.g., "Fade", "Hot Towel") use a light gray fill with `label-sm` typography.

### Specialized Components
- **Barber Selection / Profile:** Large circular avatars (64px) with a 2px Royal Blue border when selected or active. Shows `station_number` clearly.
- **Time Slot Picker:** A grid of 8px rounded buttons. Selected slots fill with Royal Blue; unavailable slots are greyed out with a strike-through.
- **Admin Data Tables:** Clean rows for CRUD de Barberos y Servicios, con 1px dividers, optimized to show Sync state with Supabase.
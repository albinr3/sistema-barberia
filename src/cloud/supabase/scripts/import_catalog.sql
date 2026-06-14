-- Auto-generated catalog export from SQLite
-- Source: C:\Users\Albin Rodriguez\AppData\Local\BarberiaSystem\barberia-local.db
-- Generado el: 2026-06-13 21:35:21

-- ==========================================
-- BARBERS
-- ==========================================
INSERT INTO public.barbers (id, display_name, station_code, is_active) VALUES ('10000000-0000-0000-0000-000000000001', 'Luis', 'B-22', true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.barbers (id, display_name, station_code, is_active) VALUES ('10000000-0000-0000-0000-000000000002', 'Ana', 'B-2', true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.barbers (id, display_name, station_code, is_active) VALUES ('10000000-0000-0000-0000-000000000003', 'Carlos', 'B-9', true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.barbers (id, display_name, station_code, is_active) VALUES ('1151fff2-b448-49fa-ae29-1fed3ad4e4ec', 'Marucs', 'B-4', true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.barbers (id, display_name, station_code, is_active) VALUES ('e1fe49bf-943a-48aa-9617-07f22f810841', 'Juan', 'B-20', true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.barbers (id, display_name, station_code, is_active) VALUES ('8b1e691e-cc6f-4fd8-bb3b-1e25b30216ad', 'Yano', 'B-23', true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.barbers (id, display_name, station_code, is_active) VALUES ('c6d42bec-8b1d-4725-aaeb-5e42af42b926', 'John Doe', 'B-7', true) ON CONFLICT(id) DO NOTHING;


-- ==========================================
-- SERVICES
-- ==========================================
INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES ('e240a7852ac985c357628bc94067c8bf', 'REGULAR HAIRCUT', 2500, 0, true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES ('694c6447ae14e8a415e98cd666c4e0d6', 'HAIR & BEARD', 3500, 1, true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES ('765e86e4bc413346b10a795866a47344', '(KIDS) 9 AND UNDER', 2000, 2, true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES ('10e7cb8435e8a4fa5e97812a782fc827', 'SKIN FADE & SHORT FADE', 2500, 3, true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES ('8dd36703ee8be978dd6a62df597e8cf4', 'HALF & CERO GUARD', 2500, 4, true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES ('7fd02ac346d86d7cd60eeed70761208a', 'SENIORS', 2000, 5, true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES ('72e221aa85d1e9df643b0b0586be227d', 'SHAPE UPS', 1500, 6, true) ON CONFLICT(id) DO NOTHING;
INSERT INTO public.services (id, name, base_price_cents, sort_order, is_active) VALUES ('b3c3eb807c1dde5aedb6bb5154436f21', 'SHAPE UPS & BEARD', 2000, 7, true) ON CONFLICT(id) DO NOTHING;


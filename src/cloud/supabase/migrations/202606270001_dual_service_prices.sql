ALTER TABLE public.services ADD COLUMN desktop_price_cents integer;
ALTER TABLE public.services ADD COLUMN web_price_cents integer;

UPDATE public.services
SET desktop_price_cents = base_price_cents,
    web_price_cents = base_price_cents
WHERE desktop_price_cents IS NULL;

ALTER TABLE public.services ALTER COLUMN desktop_price_cents SET NOT NULL;
ALTER TABLE public.services ADD CONSTRAINT services_desktop_price_cents_check CHECK (desktop_price_cents > 0);

ALTER TABLE public.services ALTER COLUMN web_price_cents SET NOT NULL;
ALTER TABLE public.services ADD CONSTRAINT services_web_price_cents_check CHECK (web_price_cents > 0);

ALTER TABLE public.services DROP COLUMN base_price_cents;

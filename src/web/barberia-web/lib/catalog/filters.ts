export type CatalogStatus = {
  is_active: boolean;
};

export function activeOnly<T extends CatalogStatus>(rows: T[]) {
  return rows.filter((row) => row.is_active);
}

export function formatPriceFromCents(cents: number) {
  return new Intl.NumberFormat("en-US", {
    style: "currency",
    currency: "USD",
  }).format(cents / 100);
}

export function centsFromDollarInput(value: string) {
  const normalized = value.trim().replace(/[$,]/g, "");
  const amount = Number(normalized);

  if (!Number.isFinite(amount)) {
    return Number.NaN;
  }

  return Math.round(amount * 100);
}

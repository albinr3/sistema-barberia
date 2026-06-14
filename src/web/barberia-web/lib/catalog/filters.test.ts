import { describe, expect, it } from "vitest";
import { activeOnly, centsFromDollarInput, formatPriceFromCents } from "./filters";

describe("catalog filters", () => {
  it("keeps only active catalog rows", () => {
    expect(
      activeOnly([
        { id: "one", is_active: true },
        { id: "two", is_active: false },
      ]),
    ).toEqual([{ id: "one", is_active: true }]);
  });

  it("formats prices from cents", () => {
    expect(formatPriceFromCents(2500)).toBe("$25.00");
  });

  it("normalizes dollar input to cents", () => {
    expect(centsFromDollarInput("$1,234.56")).toBe(123456);
  });
});

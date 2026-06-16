import { describe, expect, it } from "vitest";
import { getPayrollWeekRange, payrollReference, type PayrollPeriod } from "./payroll";

describe("payroll helpers", () => {
  it("uses Friday to Thursday payroll weeks", () => {
    expect(getPayrollWeekRange("2026-06-16")).toMatchObject({
      startDate: "2026-06-12",
      endDate: "2026-06-19",
    });

    expect(getPayrollWeekRange("2026-06-12")).toMatchObject({
      startDate: "2026-06-12",
      endDate: "2026-06-19",
    });
  });

  it("formats fallback payroll references like desktop", () => {
    const period = {
      local_period_id: "12345678-1111-2222-3333-444444444444",
      payment_reference: null,
    } as PayrollPeriod;

    expect(payrollReference(period, {
      referenceDate: "2026-06-12",
      startDate: "2026-06-12",
      endDate: "2026-06-19",
      label: "",
    })).toBe("NOM-260612-1234");
  });
});

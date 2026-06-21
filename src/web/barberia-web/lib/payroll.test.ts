import { describe, expect, it, vi } from "vitest";
import { getPayrollHistory, getPayrollWeekRange, payrollReference, type PayrollPeriod } from "./payroll";

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
  it("loads paid payroll history without pinning it to the latest sync device", async () => {
    const eq = vi.fn().mockReturnThis();
    const order = vi.fn().mockReturnThis();
    const limit = vi.fn().mockResolvedValue({
      data: [
        {
          id: "period-1",
          local_period_id: "11111111-1111-1111-1111-111111111111",
          start_date: "2026-06-12",
          end_date: "2026-06-19",
          state: "paid",
          lines: [],
        },
        {
          id: "period-2",
          local_period_id: "22222222-2222-2222-2222-222222222222",
          start_date: "2026-06-05",
          end_date: "2026-06-12",
          state: "paid",
          lines: [],
        },
      ],
      error: null,
    });
    const select = vi.fn().mockReturnValue({ eq, order, limit });
    const from = vi.fn().mockReturnValue({ select });

    const result = await getPayrollHistory({ from } as never);

    expect(from).toHaveBeenCalledWith("synced_payroll_periods");
    expect(eq).toHaveBeenCalledTimes(1);
    expect(eq).toHaveBeenCalledWith("state", "paid");
    expect(result.periods).toHaveLength(2);
  });
});

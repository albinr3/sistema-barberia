"use client";

import { Printer } from "lucide-react";
import styles from "./payroll.module.css";

export function PayrollPrintButton() {
  return (
    <button type="button" className={styles.printButton} onClick={() => window.print()}>
      <Printer size={16} aria-hidden="true" />
      Print Payroll
    </button>
  );
}

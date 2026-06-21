using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Barberia.Hardware.Pos;

public sealed class WindowsGraphicsCashBoxReceiptPrinter : ICashBoxReceiptPrinter
{
    private readonly string? _printerName;

    public WindowsGraphicsCashBoxReceiptPrinter()
    {
    }

    public WindowsGraphicsCashBoxReceiptPrinter(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new ArgumentException("Printer name is required.", nameof(printerName));
        }

        _printerName = printerName.Trim();
    }

    public HardwareOperationResult Print(CashReceiptPrintJob job)
    {
        var validation = ValidateReceipt(job);
        if (!validation.Succeeded)
        {
            return validation;
        }

        return PrintWindowsDocument($"Cash Box Receipt {job.ReceiptNumber}", canvas => DrawReceipt(canvas, job));
    }

    public HardwareOperationResult PrintDayReport(DayReportPrintJob job)
    {
        var validation = ValidateDayReport(job);
        if (!validation.Succeeded)
        {
            return validation;
        }

        return PrintWindowsDocument($"Cash Box Day Report {job.GeneratedAt:yyyy-MM-dd}", canvas => DrawDayReport(canvas, job));
    }

    private HardwareOperationResult PrintWindowsDocument(string documentName, Action<ReceiptCanvas> draw)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return HardwareOperationResult.Failure("Windows printing is only available on Windows.");
        }

        var printerName = _printerName ?? WindowsGraphicsPrinter.GetDefaultPrinterName();
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return HardwareOperationResult.Failure("No default Windows printer is configured for cash box printing.");
        }

        try
        {
            WindowsGraphicsPrinter.Send(printerName, documentName, draw);
            return HardwareOperationResult.Success();
        }
        catch (Win32Exception exception)
        {
            return HardwareOperationResult.Failure($"Windows printer error: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            return HardwareOperationResult.Failure(exception.Message);
        }
    }

    private static HardwareOperationResult ValidateReceipt(CashReceiptPrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (string.IsNullOrWhiteSpace(job.ReceiptNumber))
        {
            return HardwareOperationResult.Failure("Receipt number is required.");
        }

        if (job.DisplayTicketNumber <= 0)
        {
            return HardwareOperationResult.Failure("Ticket number is required.");
        }

        if (string.IsNullOrWhiteSpace(job.BarberName))
        {
            return HardwareOperationResult.Failure("Barber name is required.");
        }

        if (string.IsNullOrWhiteSpace(job.BarberStationCode))
        {
            return HardwareOperationResult.Failure("Barber station code is required.");
        }

        if (string.IsNullOrWhiteSpace(job.ServiceName))
        {
            return HardwareOperationResult.Failure("Service name is required.");
        }

        if (job.ServicePrice <= 0)
        {
            return HardwareOperationResult.Failure("Service price must be greater than zero.");
        }

        if (job.AdditionalAmount is not (0m or 2m or 3m or 5m))
        {
            return HardwareOperationResult.Failure("Service additional amount is invalid.");
        }

        if (job.Amount <= 0)
        {
            return HardwareOperationResult.Failure("Cash receipt amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(job.DeviceId))
        {
            return HardwareOperationResult.Failure("Device id is required to print the cash receipt.");
        }

        return HardwareOperationResult.Success();
    }

    private static HardwareOperationResult ValidateDayReport(DayReportPrintJob job)
    {
        ArgumentNullException.ThrowIfNull(job);

        if (string.IsNullOrWhiteSpace(job.DeviceId))
        {
            return HardwareOperationResult.Failure("Device id is required to print the day report.");
        }

        return HardwareOperationResult.Success();
    }

    private static void DrawReceipt(ReceiptCanvas canvas, CashReceiptPrintJob job)
    {
        canvas.Center(KioskTicketPrintText.BrandName, 16, bold: true, bottomSpacing: 14);
        canvas.Center("CASH RECEIPT", 13, bold: true, bottomSpacing: 10);
        canvas.Separator();
        canvas.Pair("Receipt", job.ReceiptNumber);
        canvas.Pair("Ticket", $"#{job.DisplayTicketNumber}");
        canvas.Pair("Barber", $"{job.BarberName} ({job.BarberStationCode})");
        canvas.Separator();
        canvas.WrapLeft(job.ServiceName, 11, bold: true, bottomSpacing: 4);
        canvas.Pair("Service", FormatMoney(job.ServicePrice, job.Currency));
        if (job.AdditionalAmount > 0)
        {
            canvas.Pair("Additional", FormatMoney(job.AdditionalAmount, job.Currency));
        }

        canvas.Separator();
        canvas.Pair("TOTAL", FormatMoney(job.Amount, job.Currency), 13, bold: true);
        canvas.Separator();
        canvas.Pair("Payment", job.PaymentMethod);
        canvas.Pair("Date", job.CollectedAt.ToString("yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture));
        canvas.Pair("Device", job.DeviceId);
        canvas.Blank(12);
        canvas.Center("Thank you for your visit.", 10, bold: false, bottomSpacing: 8);
    }

    private static void DrawDayReport(ReceiptCanvas canvas, DayReportPrintJob job)
    {
        canvas.Center(KioskTicketPrintText.BrandName, 16, bold: true, bottomSpacing: 14);
        canvas.Center("DAILY TICKETS REPORT", 13, bold: true, bottomSpacing: 10);
        canvas.Separator();
        canvas.Pair("Date", job.GeneratedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        canvas.Pair("Generated", job.GeneratedAt.ToString("hh:mm tt", CultureInfo.InvariantCulture));
        canvas.Pair("Device", job.DeviceId);
        canvas.Separator();
        canvas.Pair("Tickets", job.Barbers.Sum(barber => barber.ServicesClosed).ToString(CultureInfo.InvariantCulture));
        canvas.Pair("Total", FormatMoney(job.TotalCash, "USD"), 13, bold: true);
        canvas.Separator();

        if (job.Barbers.Count == 0)
        {
            canvas.Center("No tickets closed today.", 10, bold: false, bottomSpacing: 8);
            return;
        }

        foreach (var barber in job.Barbers)
        {
            canvas.WrapLeft(barber.BarberName, 10, bold: true, bottomSpacing: 2);
            canvas.Pair($"  {barber.ServicesClosed} tickets", FormatMoney(barber.CashCollected, "USD"));
        }
    }

    private static string FormatMoney(decimal amount, string currency)
    {
        var prefix = string.Equals(currency, "USD", StringComparison.OrdinalIgnoreCase) ? "$" : $"{currency} ";
        return string.Create(CultureInfo.InvariantCulture, $"{prefix}{amount:0.00}");
    }

    private sealed class ReceiptCanvas
    {
        private readonly IntPtr _hdc;
        private readonly int _dpiY;
        private readonly int _pageWidth;
        private readonly int _marginX;
        private int _y;

        public ReceiptCanvas(IntPtr hdc, int dpiX, int dpiY, int deviceWidth)
        {
            _hdc = hdc;
            _dpiY = dpiY;
            _pageWidth = Math.Min(deviceWidth, (int)(dpiX * 3.15));
            _marginX = Math.Max(dpiX / 10, 10);
            _y = Math.Max(dpiY / 10, 10);
        }

        public void Center(string text, int pointSize, bool bold, int bottomSpacing)
        {
            foreach (var line in text.Split('\n'))
            {
                using var font = WindowsGraphicsPrinter.GdiObjectHandle.CreateFont(_dpiY, pointSize, bold);
                DrawCenteredText(line.Trim(), font.Handle, Scale(bottomSpacing));
            }
        }

        public void WrapLeft(string text, int pointSize, bool bold, int bottomSpacing)
        {
            using var font = WindowsGraphicsPrinter.GdiObjectHandle.CreateFont(_dpiY, pointSize, bold);
            foreach (var line in WrapText(text, font.Handle, _pageWidth - (_marginX * 2)))
            {
                DrawLeftText(line, font.Handle, Scale(bottomSpacing));
            }
        }

        public void Pair(string label, string value, int pointSize = 10, bool bold = false)
        {
            using var font = WindowsGraphicsPrinter.GdiObjectHandle.CreateFont(_dpiY, pointSize, bold);
            var width = _pageWidth - (_marginX * 2);
            var left = label;
            var right = value;

            if (!TryGetTextSize($"{left} {right}", font.Handle, out var pairSize) || pairSize.cx > width)
            {
                DrawLeftText(left, font.Handle, 0);
                DrawRightText(right, font.Handle, Scale(5));
                return;
            }

            DrawLeftAndRightText(left, right, font.Handle, Scale(5));
        }

        public void Separator()
        {
            using var font = WindowsGraphicsPrinter.GdiObjectHandle.CreateFont(_dpiY, 9, bold: false);
            DrawCenteredText(new string('-', 32), font.Handle, Scale(5));
        }

        public void Blank(int pixels)
        {
            _y += Scale(pixels);
        }

        private IReadOnlyList<string> WrapText(string text, IntPtr font, int maxWidth)
        {
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                return [string.Empty];
            }

            var lines = new List<string>();
            var current = string.Empty;
            foreach (var word in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? word : $"{current} {word}";
                if (TryGetTextSize(candidate, font, out var size) && size.cx <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                }

                current = word;
            }

            if (!string.IsNullOrEmpty(current))
            {
                lines.Add(current);
            }

            return lines;
        }

        private void DrawCenteredText(string text, IntPtr font, int bottomSpacing)
        {
            var oldFont = WindowsGraphicsPrinter.SelectObject(_hdc, font);
            try
            {
                if (!WindowsGraphicsPrinter.GetTextExtentPoint32(_hdc, text, text.Length, out var size))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                var x = Math.Max(_marginX, (_pageWidth - size.cx) / 2);
                if (!WindowsGraphicsPrinter.TextOut(_hdc, x, _y, text, text.Length))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                _y += size.cy + bottomSpacing;
            }
            finally
            {
                WindowsGraphicsPrinter.SelectObject(_hdc, oldFont);
            }
        }

        private void DrawLeftText(string text, IntPtr font, int bottomSpacing)
        {
            var oldFont = WindowsGraphicsPrinter.SelectObject(_hdc, font);
            try
            {
                if (!WindowsGraphicsPrinter.GetTextExtentPoint32(_hdc, text, text.Length, out var size))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                if (!WindowsGraphicsPrinter.TextOut(_hdc, _marginX, _y, text, text.Length))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                _y += size.cy + bottomSpacing;
            }
            finally
            {
                WindowsGraphicsPrinter.SelectObject(_hdc, oldFont);
            }
        }

        private void DrawRightText(string text, IntPtr font, int bottomSpacing)
        {
            var oldFont = WindowsGraphicsPrinter.SelectObject(_hdc, font);
            try
            {
                if (!WindowsGraphicsPrinter.GetTextExtentPoint32(_hdc, text, text.Length, out var size))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                var x = Math.Max(_marginX, _pageWidth - _marginX - size.cx);
                if (!WindowsGraphicsPrinter.TextOut(_hdc, x, _y, text, text.Length))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                _y += size.cy + bottomSpacing;
            }
            finally
            {
                WindowsGraphicsPrinter.SelectObject(_hdc, oldFont);
            }
        }

        private void DrawLeftAndRightText(string left, string right, IntPtr font, int bottomSpacing)
        {
            var oldFont = WindowsGraphicsPrinter.SelectObject(_hdc, font);
            try
            {
                if (!WindowsGraphicsPrinter.GetTextExtentPoint32(_hdc, left, left.Length, out var leftSize)
                    || !WindowsGraphicsPrinter.GetTextExtentPoint32(_hdc, right, right.Length, out var rightSize))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                if (!WindowsGraphicsPrinter.TextOut(_hdc, _marginX, _y, left, left.Length))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                var rightX = Math.Max(_marginX + leftSize.cx + Scale(8), _pageWidth - _marginX - rightSize.cx);
                if (!WindowsGraphicsPrinter.TextOut(_hdc, rightX, _y, right, right.Length))
                {
                    throw WindowsGraphicsPrinter.CreateWin32Exception();
                }

                _y += Math.Max(leftSize.cy, rightSize.cy) + bottomSpacing;
            }
            finally
            {
                WindowsGraphicsPrinter.SelectObject(_hdc, oldFont);
            }
        }

        private bool TryGetTextSize(string text, IntPtr font, out WindowsGraphicsPrinter.Size size)
        {
            var oldFont = WindowsGraphicsPrinter.SelectObject(_hdc, font);
            try
            {
                return WindowsGraphicsPrinter.GetTextExtentPoint32(_hdc, text, text.Length, out size);
            }
            finally
            {
                WindowsGraphicsPrinter.SelectObject(_hdc, oldFont);
            }
        }

        private int Scale(int pixelsAt96Dpi)
        {
            return Math.Max(1, pixelsAt96Dpi * _dpiY / 96);
        }
    }

    private static class WindowsGraphicsPrinter
    {
        private const int HorzRes = 8;
        private const int LogPixelsX = 88;
        private const int LogPixelsY = 90;
        private const int Transparent = 1;
        private const int FontWeightNormal = 400;
        private const int FontWeightBold = 700;
        private const int MmText = 1;

        public static string? GetDefaultPrinterName()
        {
            var size = 0;
            _ = GetDefaultPrinter(null, ref size);
            if (size <= 0)
            {
                return null;
            }

            var buffer = new char[size];
            return GetDefaultPrinter(buffer, ref size) ? new string(buffer, 0, size - 1) : null;
        }

        public static void Send(string printerName, string documentName, Action<ReceiptCanvas> draw)
        {
            var hdc = CreateDC("WINSPOOL", printerName, null, IntPtr.Zero);
            if (hdc == IntPtr.Zero)
            {
                throw CreateWin32Exception();
            }

            try
            {
                var documentInfo = new DocInfo
                {
                    cbSize = Marshal.SizeOf<DocInfo>(),
                    lpszDocName = documentName
                };
                if (StartDoc(hdc, ref documentInfo) <= 0)
                {
                    throw CreateWin32Exception();
                }

                try
                {
                    if (StartPage(hdc) <= 0)
                    {
                        throw CreateWin32Exception();
                    }

                    try
                    {
                        SetMapMode(hdc, MmText);
                        SetBkMode(hdc, Transparent);
                        var dpiX = GetDeviceCaps(hdc, LogPixelsX);
                        var dpiY = GetDeviceCaps(hdc, LogPixelsY);
                        var pageWidth = GetDeviceCaps(hdc, HorzRes);
                        draw(new ReceiptCanvas(hdc, dpiX, dpiY, pageWidth));
                    }
                    finally
                    {
                        EndPage(hdc);
                    }
                }
                finally
                {
                    EndDoc(hdc);
                }
            }
            finally
            {
                DeleteDC(hdc);
            }
        }

        public static Win32Exception CreateWin32Exception()
        {
            var errorCode = Marshal.GetLastWin32Error();
            return new Win32Exception(errorCode);
        }

        [DllImport("winspool.drv", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetDefaultPrinter(char[]? printerName, ref int size);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDC(string driver, string device, string? output, IntPtr initData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int StartDoc(IntPtr hdc, [In] ref DocInfo documentInfo);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int EndDoc(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int StartPage(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int EndPage(IntPtr hdc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int SetMapMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int SetBkMode(IntPtr hdc, int mode);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDeviceCaps(IntPtr hdc, int index);

        [DllImport("gdi32.dll", SetLastError = true)]
        public static extern IntPtr SelectObject(IntPtr hdc, IntPtr gdiObject);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr gdiObject);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFont(
            int height,
            int width,
            int escapement,
            int orientation,
            int weight,
            uint italic,
            uint underline,
            uint strikeOut,
            uint charSet,
            uint outputPrecision,
            uint clipPrecision,
            uint quality,
            uint pitchAndFamily,
            string faceName);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool TextOut(IntPtr hdc, int x, int y, string text, int length);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool GetTextExtentPoint32(IntPtr hdc, string text, int length, out Size size);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DocInfo
        {
            public int cbSize;
            public string lpszDocName;
            public string? lpszOutput;
            public string? lpszDatatype;
            public int fwType;
        }

        [StructLayout(LayoutKind.Sequential)]
        public readonly struct Size
        {
            public readonly int cx;
            public readonly int cy;
        }

        public sealed class GdiObjectHandle : IDisposable
        {
            private GdiObjectHandle(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }

            public static GdiObjectHandle CreateFont(int dpiY, int pointSize, bool bold)
            {
                var height = -Math.Max(1, pointSize * dpiY / 72);
                var handle = WindowsGraphicsPrinter.CreateFont(
                    height,
                    0,
                    0,
                    0,
                    bold ? FontWeightBold : FontWeightNormal,
                    0,
                    0,
                    0,
                    1,
                    0,
                    0,
                    0,
                    0,
                    "Arial");
                if (handle == IntPtr.Zero)
                {
                    throw CreateWin32Exception();
                }

                return new GdiObjectHandle(handle);
            }

            public void Dispose()
            {
                if (Handle == IntPtr.Zero)
                {
                    return;
                }

                DeleteObject(Handle);
                Handle = IntPtr.Zero;
            }
        }
    }
}

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Barberia.Hardware.Pos;

public sealed class WindowsGraphicsKioskTicketPrinter : IKioskTicketPrinter
{
    private readonly string? _printerName;

    public WindowsGraphicsKioskTicketPrinter()
    {
    }

    public WindowsGraphicsKioskTicketPrinter(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new ArgumentException("Printer name is required.", nameof(printerName));
        }

        _printerName = printerName.Trim();
    }

    public HardwareOperationResult Print(KioskTicketPrintJob job)
    {
        var validation = KioskTicketPrintJobValidator.Validate(job);
        if (!validation.Succeeded)
        {
            return validation;
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return HardwareOperationResult.Failure("Windows printing is only available on Windows.");
        }

        var printerName = _printerName ?? WindowsGraphicsPrinter.GetDefaultPrinterName();
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return HardwareOperationResult.Failure("No default Windows printer is configured for kiosk ticket printing.");
        }

        try
        {
            WindowsGraphicsPrinter.Send(printerName, $"Kiosk Ticket {job.DisplayTicketNumber}", job);
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

    private static class WindowsGraphicsPrinter
    {
        private const int HorzRes = 8;
        private const int VertRes = 10;
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

        public static void Send(string printerName, string documentName, KioskTicketPrintJob job)
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
                        DrawTicketPage(hdc, job);
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

        private static void DrawTicketPage(IntPtr hdc, KioskTicketPrintJob job)
        {
            SetMapMode(hdc, MmText);
            SetBkMode(hdc, Transparent);

            var dpiX = GetDeviceCaps(hdc, LogPixelsX);
            var dpiY = GetDeviceCaps(hdc, LogPixelsY);
            var pageWidth = Math.Min(GetDeviceCaps(hdc, HorzRes), (int)(dpiX * 3.15));
            var marginX = Math.Max(dpiX / 8, 12);
            var y = Math.Max(dpiY / 8, 12);

            using var titleFont = GdiObjectHandle.CreateFont(dpiY, 15, bold: true);
            using var ticketFont = GdiObjectHandle.CreateFont(dpiY, 30, bold: true);
            using var bodyFont = GdiObjectHandle.CreateFont(dpiY, 11, bold: false);
            using var codeFont = GdiObjectHandle.CreateFont(dpiY, 9, bold: false);
            using var brush = GdiObjectHandle.CreateSolidBrush(0);

            y = DrawCenteredLines(hdc, KioskTicketPrintText.BrandName, titleFont.Handle, pageWidth, y, dpiY / 24, dpiY / 10);
            y = DrawCenteredText(hdc, $"TICKET #{job.DisplayTicketNumber}", ticketFont.Handle, pageWidth, y, dpiY / 10);

            foreach (var line in GetBarberPreferenceLines(job))
            {
                y = DrawCenteredText(hdc, line, bodyFont.Handle, pageWidth, y, dpiY / 20);
            }

            y += dpiY / 24;

            var qrCode = QrCodeMatrix.CreateAlphanumeric(job.QrPayload);
            var qrPhysicalInches = 1.15;
            var moduleSizeX = Math.Max((int)(dpiX * qrPhysicalInches) / (qrCode.Size + 8), 1);
            var moduleSizeY = Math.Max((int)(dpiY * qrPhysicalInches) / (qrCode.Size + 8), 1);
            var renderedQrWidth = moduleSizeX * (qrCode.Size + 8);
            var renderedQrHeight = moduleSizeY * (qrCode.Size + 8);
            var qrLeft = (pageWidth - renderedQrWidth) / 2;
            DrawQrCode(hdc, qrCode, qrLeft, y, moduleSizeX, moduleSizeY, brush.Handle);
            y += renderedQrHeight + (dpiY / 16);

            y = DrawCenteredText(hdc, $"{KioskTicketPrintText.CodeLabel}: {job.QrPayload}", codeFont.Handle, pageWidth, y, dpiY / 5);
            y = DrawCenteredText(hdc, job.CustomerName, bodyFont.Handle, pageWidth, y, dpiY / 12);
            y = DrawCenteredText(hdc, job.CheckedInAt.ToString("yyyy-MM-dd hh:mm tt"), bodyFont.Handle, pageWidth, y, dpiY / 10);

            y += dpiY / 24;
            y = DrawCenteredText(hdc, KioskTicketPrintText.PresentTicket, bodyFont.Handle, pageWidth, y, dpiY / 12);
            DrawCenteredText(hdc, KioskTicketPrintText.ThankYou, bodyFont.Handle, pageWidth, y, dpiY / 12);
        }

        private static IEnumerable<string> GetBarberPreferenceLines(KioskTicketPrintJob job)
        {
            if (job.AcceptsAnyBarber && job.RequestedBarberNames.Count == 0)
            {
                yield return KioskTicketPrintText.AnyAvailableBarber;
                yield break;
            }

            yield return KioskTicketPrintText.RequestedBarbers;
            for (var index = 0; index < job.RequestedBarberNames.Count; index++)
            {
                var station = job.RequestedBarberStationCodes[index];
                var stationText = string.IsNullOrWhiteSpace(station) ? string.Empty : $" ({station})";
                yield return $"{job.RequestedBarberNames[index]}{stationText}";
            }

            if (!string.IsNullOrWhiteSpace(job.AssignedBarberName))
            {
                var stationText = string.IsNullOrWhiteSpace(job.AssignedBarberStationCode)
                    ? string.Empty
                    : $" ({job.AssignedBarberStationCode})";
                yield return $"{KioskTicketPrintText.Assigned}: {job.AssignedBarberName}{stationText}";
            }
        }

        private static int DrawCenteredLines(
            IntPtr hdc,
            string text,
            IntPtr font,
            int pageWidth,
            int y,
            int lineSpacing,
            int bottomSpacing)
        {
            var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            for (var index = 0; index < lines.Length; index++)
            {
                var spacing = index == lines.Length - 1 ? bottomSpacing : lineSpacing;
                y = DrawCenteredText(hdc, lines[index].Trim(), font, pageWidth, y, spacing);
            }

            return y;
        }

        private static int DrawCenteredText(
            IntPtr hdc,
            string text,
            IntPtr font,
            int pageWidth,
            int y,
            int bottomSpacing)
        {
            var oldFont = SelectObject(hdc, font);
            try
            {
                if (!GetTextExtentPoint32(hdc, text, text.Length, out var size))
                {
                    throw CreateWin32Exception();
                }

                var x = Math.Max(0, (pageWidth - size.cx) / 2);
                if (!TextOut(hdc, x, y, text, text.Length))
                {
                    throw CreateWin32Exception();
                }

                return y + size.cy + bottomSpacing;
            }
            finally
            {
                SelectObject(hdc, oldFont);
            }
        }

        private static void DrawQrCode(
            IntPtr hdc,
            QrCodeMatrix qrCode,
            int left,
            int top,
            int moduleSizeX,
            int moduleSizeY,
            IntPtr brush)
        {
            const int quietZoneModules = 4;
            for (var y = 0; y < qrCode.Size; y++)
            {
                for (var x = 0; x < qrCode.Size; x++)
                {
                    if (!qrCode.IsDark(x, y))
                    {
                        continue;
                    }

                    var rect = new Rect(
                        left + ((x + quietZoneModules) * moduleSizeX),
                        top + ((y + quietZoneModules) * moduleSizeY),
                        left + ((x + quietZoneModules + 1) * moduleSizeX),
                        top + ((y + quietZoneModules + 1) * moduleSizeY));
                    FillRect(hdc, ref rect, brush);
                }
            }
        }

        private static Win32Exception CreateWin32Exception()
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
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr gdiObject);

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

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateSolidBrush(int color);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool TextOut(IntPtr hdc, int x, int y, string text, int length);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetTextExtentPoint32(IntPtr hdc, string text, int length, out Size size);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int FillRect(IntPtr hdc, [In] ref Rect rect, IntPtr brush);

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
        private readonly struct Size
        {
            public readonly int cx;
            public readonly int cy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public Rect(int left, int top, int right, int bottom)
            {
                Left = left;
                Top = top;
                Right = right;
                Bottom = bottom;
            }

            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private sealed class GdiObjectHandle : IDisposable
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

            public static GdiObjectHandle CreateSolidBrush(int color)
            {
                var handle = WindowsGraphicsPrinter.CreateSolidBrush(color);
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

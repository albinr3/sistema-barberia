namespace Barberia.Hardware.Pos;

internal sealed class QrCodeMatrix
{
    private const int Version = 1;
    private const int SizeForVersionOne = 21;
    private const int DataCodewordCount = 19;
    private const int ErrorCorrectionCodewordCount = 7;
    private const int MaskPattern = 0;
    private const string AlphanumericCharacters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

    private readonly bool[,] _modules;
    private readonly bool[,] _reserved;

    private QrCodeMatrix()
    {
        _modules = new bool[SizeForVersionOne, SizeForVersionOne];
        _reserved = new bool[SizeForVersionOne, SizeForVersionOne];
        Size = SizeForVersionOne;
    }

    public int Size { get; }

    public bool IsDark(int x, int y)
    {
        return _modules[x, y];
    }

    public static QrCodeMatrix CreateAlphanumeric(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new InvalidOperationException("QR payload is required.");
        }

        var normalized = payload.Trim().ToUpperInvariant();
        var matrix = new QrCodeMatrix();
        matrix.DrawFunctionPatterns();
        matrix.DrawFormatBits();
        matrix.DrawData(CreateCodewords(normalized));

        return matrix;
    }

    private static byte[] CreateCodewords(string payload)
    {
        var dataCodewords = CreateDataCodewords(payload);
        var errorCorrectionCodewords = ReedSolomon.CreateRemainder(dataCodewords, ErrorCorrectionCodewordCount);

        return dataCodewords
            .Concat(errorCorrectionCodewords)
            .ToArray();
    }

    private static byte[] CreateDataCodewords(string payload)
    {
        var bits = new List<int>();
        AppendBits(bits, 0b0010, 4);
        AppendBits(bits, payload.Length, 9);

        for (var index = 0; index < payload.Length; index += 2)
        {
            var first = GetAlphanumericValue(payload[index]);
            if (index + 1 < payload.Length)
            {
                var second = GetAlphanumericValue(payload[index + 1]);
                AppendBits(bits, (first * 45) + second, 11);
            }
            else
            {
                AppendBits(bits, first, 6);
            }
        }

        if (bits.Count > DataCodewordCount * 8)
        {
            throw new InvalidOperationException("QR payload is too long for the kiosk ticket.");
        }

        AppendBits(bits, 0, Math.Min(4, (DataCodewordCount * 8) - bits.Count));
        while (bits.Count % 8 != 0)
        {
            bits.Add(0);
        }

        var codewords = BitsToBytes(bits);
        var padded = codewords.ToList();
        for (var padIndex = 0; padded.Count < DataCodewordCount; padIndex++)
        {
            padded.Add((byte)(padIndex % 2 == 0 ? 0xEC : 0x11));
        }

        return padded.ToArray();
    }

    private void DrawFunctionPatterns()
    {
        DrawFinderPattern(0, 0);
        DrawFinderPattern(Size - 7, 0);
        DrawFinderPattern(0, Size - 7);

        for (var index = 8; index < Size - 8; index++)
        {
            var dark = index % 2 == 0;
            SetFunctionModule(index, 6, dark);
            SetFunctionModule(6, index, dark);
        }

        SetFunctionModule(8, (4 * Version) + 9, true);
    }

    private void DrawFinderPattern(int left, int top)
    {
        for (var dy = -1; dy <= 7; dy++)
        {
            for (var dx = -1; dx <= 7; dx++)
            {
                var x = left + dx;
                var y = top + dy;
                if (x < 0 || x >= Size || y < 0 || y >= Size)
                {
                    continue;
                }

                var dark = dx is >= 0 and <= 6 &&
                    dy is >= 0 and <= 6 &&
                    (dx is 0 or 6 || dy is 0 or 6 || dx is >= 2 and <= 4 && dy is >= 2 and <= 4);
                SetFunctionModule(x, y, dark);
            }
        }
    }

    private void DrawFormatBits()
    {
        const int errorCorrectionLowFormatBits = 1;
        var data = (errorCorrectionLowFormatBits << 3) | MaskPattern;
        var remainder = data;
        for (var index = 0; index < 10; index++)
        {
            remainder = (remainder << 1) ^ (((remainder >> 9) & 1) == 0 ? 0 : 0x537);
        }

        var bits = ((data << 10) | remainder) ^ 0x5412;
        for (var index = 0; index <= 5; index++)
        {
            SetFunctionModule(8, index, GetBit(bits, index));
        }

        SetFunctionModule(8, 7, GetBit(bits, 6));
        SetFunctionModule(8, 8, GetBit(bits, 7));
        SetFunctionModule(7, 8, GetBit(bits, 8));

        for (var index = 9; index < 15; index++)
        {
            SetFunctionModule(14 - index, 8, GetBit(bits, index));
        }

        for (var index = 0; index < 8; index++)
        {
            SetFunctionModule(Size - 1 - index, 8, GetBit(bits, index));
        }

        for (var index = 8; index < 15; index++)
        {
            SetFunctionModule(8, Size - 15 + index, GetBit(bits, index));
        }

        SetFunctionModule(8, Size - 8, true);
    }

    private void DrawData(byte[] codewords)
    {
        var bits = codewords
            .SelectMany(codeword => Enumerable.Range(0, 8).Select(index => ((codeword >> (7 - index)) & 1) != 0))
            .ToArray();
        var bitIndex = 0;
        var upward = true;

        for (var right = Size - 1; right >= 1; right -= 2)
        {
            if (right == 6)
            {
                right--;
            }

            for (var vertical = 0; vertical < Size; vertical++)
            {
                var y = upward ? Size - 1 - vertical : vertical;
                for (var dx = 0; dx < 2; dx++)
                {
                    var x = right - dx;
                    if (_reserved[x, y])
                    {
                        continue;
                    }

                    var bit = bitIndex < bits.Length && bits[bitIndex++];
                    var mask = (x + y) % 2 == 0;
                    _modules[x, y] = bit ^ mask;
                }
            }

            upward = !upward;
        }
    }

    private void SetFunctionModule(int x, int y, bool dark)
    {
        _modules[x, y] = dark;
        _reserved[x, y] = true;
    }

    private static int GetAlphanumericValue(char character)
    {
        var value = AlphanumericCharacters.IndexOf(character);
        if (value < 0)
        {
            throw new InvalidOperationException("QR payload contains unsupported characters.");
        }

        return value;
    }

    private static void AppendBits(List<int> bits, int value, int length)
    {
        for (var index = length - 1; index >= 0; index--)
        {
            bits.Add((value >> index) & 1);
        }
    }

    private static byte[] BitsToBytes(IReadOnlyList<int> bits)
    {
        var bytes = new byte[bits.Count / 8];
        for (var byteIndex = 0; byteIndex < bytes.Length; byteIndex++)
        {
            var value = 0;
            for (var bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                value = (value << 1) | bits[(byteIndex * 8) + bitIndex];
            }

            bytes[byteIndex] = (byte)value;
        }

        return bytes;
    }

    private static bool GetBit(int value, int index)
    {
        return ((value >> index) & 1) != 0;
    }

    private static class ReedSolomon
    {
        private static readonly int[] Exp = new int[512];
        private static readonly int[] Log = new int[256];

        static ReedSolomon()
        {
            var value = 1;
            for (var index = 0; index < 255; index++)
            {
                Exp[index] = value;
                Log[value] = index;
                value <<= 1;
                if (value >= 0x100)
                {
                    value ^= 0x11D;
                }
            }

            for (var index = 255; index < Exp.Length; index++)
            {
                Exp[index] = Exp[index - 255];
            }
        }

        public static byte[] CreateRemainder(byte[] data, int degree)
        {
            var generator = CreateGenerator(degree);
            var remainder = new int[degree];

            foreach (var codeword in data)
            {
                var factor = codeword ^ remainder[0];
                for (var index = 0; index < degree - 1; index++)
                {
                    remainder[index] = remainder[index + 1] ^ Multiply(generator[index + 1], factor);
                }

                remainder[^1] = Multiply(generator[^1], factor);
            }

            return remainder.Select(value => (byte)value).ToArray();
        }

        private static int[] CreateGenerator(int degree)
        {
            var generator = new[] { 1 };
            for (var degreeIndex = 0; degreeIndex < degree; degreeIndex++)
            {
                var next = new int[generator.Length + 1];
                for (var index = 0; index < generator.Length; index++)
                {
                    next[index] ^= generator[index];
                    next[index + 1] ^= Multiply(generator[index], Exp[degreeIndex]);
                }

                generator = next;
            }

            return generator;
        }

        private static int Multiply(int left, int right)
        {
            return left == 0 || right == 0 ? 0 : Exp[Log[left] + Log[right]];
        }
    }
}

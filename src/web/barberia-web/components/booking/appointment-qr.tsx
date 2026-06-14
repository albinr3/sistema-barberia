const QR_SIZE = 21;
const DATA_CODEWORDS = 19;
const ERROR_CODEWORDS = 7;
const MASK_PATTERN = 0;
const ALPHANUMERIC = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ $%*+-./:";

export function AppointmentQrCode({
  value,
  size = 116,
  className,
}: {
  value: string;
  size?: number;
  className?: string;
}) {
  const matrix = createQrMatrix(value);
  const viewBoxSize = QR_SIZE + 8;

  return (
    <svg
      aria-label={`Appointment QR ${value}`}
      className={className}
      height={size}
      role="img"
      viewBox={`0 0 ${viewBoxSize} ${viewBoxSize}`}
      width={size}
    >
      <rect fill="#ffffff" height={viewBoxSize} width={viewBoxSize} x="0" y="0" />
      {matrix.map((row, y) =>
        row.map((dark, x) =>
          dark ? <rect fill="#111827" height="1" key={`${x}-${y}`} width="1" x={x + 4} y={y + 4} /> : null,
        ),
      )}
    </svg>
  );
}

function createQrMatrix(payload: string) {
  const normalized = payload.trim().toUpperCase();
  const modules = Array.from({ length: QR_SIZE }, () => Array(QR_SIZE).fill(false) as boolean[]);
  const reserved = Array.from({ length: QR_SIZE }, () => Array(QR_SIZE).fill(false) as boolean[]);

  const setFunction = (x: number, y: number, dark: boolean) => {
    modules[y][x] = dark;
    reserved[y][x] = true;
  };

  const drawFinder = (left: number, top: number) => {
    for (let dy = -1; dy <= 7; dy++) {
      for (let dx = -1; dx <= 7; dx++) {
        const x = left + dx;
        const y = top + dy;
        if (x < 0 || x >= QR_SIZE || y < 0 || y >= QR_SIZE) continue;

        const dark =
          dx >= 0 &&
          dx <= 6 &&
          dy >= 0 &&
          dy <= 6 &&
          (dx === 0 || dx === 6 || dy === 0 || dy === 6 || (dx >= 2 && dx <= 4 && dy >= 2 && dy <= 4));
        setFunction(x, y, dark);
      }
    }
  };

  drawFinder(0, 0);
  drawFinder(QR_SIZE - 7, 0);
  drawFinder(0, QR_SIZE - 7);

  for (let index = 8; index < QR_SIZE - 8; index++) {
    const dark = index % 2 === 0;
    setFunction(index, 6, dark);
    setFunction(6, index, dark);
  }
  setFunction(8, 13, true);
  drawFormatBits(setFunction);

  const codewords = createCodewords(normalized);
  const bits = codewords.flatMap((codeword) =>
    Array.from({ length: 8 }, (_, index) => ((codeword >> (7 - index)) & 1) !== 0),
  );
  let bitIndex = 0;
  let upward = true;

  for (let right = QR_SIZE - 1; right >= 1; right -= 2) {
    if (right === 6) right--;

    for (let vertical = 0; vertical < QR_SIZE; vertical++) {
      const y = upward ? QR_SIZE - 1 - vertical : vertical;
      for (let dx = 0; dx < 2; dx++) {
        const x = right - dx;
        if (reserved[y][x]) continue;

        const bit = bitIndex < bits.length && bits[bitIndex++];
        const mask = (x + y) % 2 === 0;
        modules[y][x] = bit !== mask;
      }
    }

    upward = !upward;
  }

  return modules;
}

function createCodewords(payload: string) {
  const data = createDataCodewords(payload);
  return [...data, ...createReedSolomonRemainder(data, ERROR_CODEWORDS)];
}

function createDataCodewords(payload: string) {
  const bits: number[] = [];
  appendBits(bits, 0b0010, 4);
  appendBits(bits, payload.length, 9);

  for (let index = 0; index < payload.length; index += 2) {
    const first = getAlphanumericValue(payload[index]);
    if (index + 1 < payload.length) {
      const second = getAlphanumericValue(payload[index + 1]);
      appendBits(bits, first * 45 + second, 11);
    } else {
      appendBits(bits, first, 6);
    }
  }

  if (bits.length > DATA_CODEWORDS * 8) {
    throw new Error("Appointment QR payload is too long.");
  }

  appendBits(bits, 0, Math.min(4, DATA_CODEWORDS * 8 - bits.length));
  while (bits.length % 8 !== 0) bits.push(0);

  const codewords: number[] = [];
  for (let byteIndex = 0; byteIndex < bits.length / 8; byteIndex++) {
    let value = 0;
    for (let bitIndex = 0; bitIndex < 8; bitIndex++) {
      value = (value << 1) | bits[byteIndex * 8 + bitIndex];
    }
    codewords.push(value);
  }

  for (let padIndex = 0; codewords.length < DATA_CODEWORDS; padIndex++) {
    codewords.push(padIndex % 2 === 0 ? 0xec : 0x11);
  }

  return codewords;
}

function drawFormatBits(setFunction: (x: number, y: number, dark: boolean) => void) {
  const data = (1 << 3) | MASK_PATTERN;
  let remainder = data;
  for (let index = 0; index < 10; index++) {
    remainder = (remainder << 1) ^ (((remainder >> 9) & 1) === 0 ? 0 : 0x537);
  }

  const bits = ((data << 10) | remainder) ^ 0x5412;
  for (let index = 0; index <= 5; index++) setFunction(8, index, getBit(bits, index));
  setFunction(8, 7, getBit(bits, 6));
  setFunction(8, 8, getBit(bits, 7));
  setFunction(7, 8, getBit(bits, 8));

  for (let index = 9; index < 15; index++) setFunction(14 - index, 8, getBit(bits, index));
  for (let index = 0; index < 8; index++) setFunction(QR_SIZE - 1 - index, 8, getBit(bits, index));
  for (let index = 8; index < 15; index++) setFunction(8, QR_SIZE - 15 + index, getBit(bits, index));
  setFunction(8, QR_SIZE - 8, true);
}

function appendBits(bits: number[], value: number, length: number) {
  for (let index = length - 1; index >= 0; index--) {
    bits.push((value >> index) & 1);
  }
}

function getAlphanumericValue(character: string) {
  const value = ALPHANUMERIC.indexOf(character);
  if (value < 0) throw new Error("Appointment QR payload contains unsupported characters.");
  return value;
}

function getBit(value: number, index: number) {
  return ((value >> index) & 1) !== 0;
}

const EXP = new Array<number>(512);
const LOG = new Array<number>(256);
let value = 1;
for (let index = 0; index < 255; index++) {
  EXP[index] = value;
  LOG[value] = index;
  value <<= 1;
  if (value >= 0x100) value ^= 0x11d;
}
for (let index = 255; index < EXP.length; index++) EXP[index] = EXP[index - 255];

function createReedSolomonRemainder(data: number[], degree: number) {
  const generator = createGenerator(degree);
  const remainder = Array(degree).fill(0) as number[];

  for (const codeword of data) {
    const factor = codeword ^ remainder[0];
    for (let index = 0; index < degree - 1; index++) {
      remainder[index] = remainder[index + 1] ^ multiply(generator[index + 1], factor);
    }
    remainder[degree - 1] = multiply(generator[degree], factor);
  }

  return remainder;
}

function createGenerator(degree: number) {
  let generator = [1];
  for (let degreeIndex = 0; degreeIndex < degree; degreeIndex++) {
    const next = Array(generator.length + 1).fill(0) as number[];
    for (let index = 0; index < generator.length; index++) {
      next[index] ^= generator[index];
      next[index + 1] ^= multiply(generator[index], EXP[degreeIndex]);
    }
    generator = next;
  }
  return generator;
}

function multiply(left: number, right: number) {
  return left === 0 || right === 0 ? 0 : EXP[LOG[left] + LOG[right]];
}

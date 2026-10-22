const MONTHS = [
  "فروردین",
  "اردیبهشت",
  "خرداد",
  "تیر",
  "مرداد",
  "شهریور",
  "مهر",
  "آبان",
  "آذر",
  "دی",
  "بهمن",
  "اسفند",
];

function div(a: number, b: number) {
  return ~~(a / b);
}

export function gregorianToJalali(gy: number, gm: number, gd: number) {
  const gDayNo = [0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334];
  const gy2 = gm > 2 ? gy + 1 : gy;
  let days =
    355666 +
    365 * gy +
    div(gy2 + 3, 4) -
    div(gy2 + 99, 100) +
    div(gy2 + 399, 400) +
    gd +
    gDayNo[gm - 1];
  let jy = -1595 + 33 * div(days, 12053);
  days %= 12053;
  jy += 4 * div(days, 1461);
  days %= 1461;
  if (days > 365) {
    jy += div(days - 1, 365);
    days = (days - 1) % 365;
  }
  let jm: number;
  let jd: number;
  if (days < 186) {
    jm = 1 + div(days, 31);
    jd = 1 + (days % 31);
  } else {
    jm = 7 + div(days - 186, 30);
    jd = 1 + ((days - 186) % 30);
  }
  return { jy, jm, jd };
}

export function jalaliToGregorian(jy: number, jm: number, jd: number) {
  let year = jy + 1595;
  let days =
    -355668 +
    365 * year +
    div(year, 33) * 8 +
    div((year % 33) + 3, 4) +
    jd +
    (jm < 7 ? (jm - 1) * 31 : (jm - 7) * 30 + 186);
  let gy = 400 * div(days, 146097);
  days %= 146097;
  if (days > 36524) {
    gy += 100 * div(--days, 36524);
    days %= 36524;
    if (days >= 365) days++;
  }
  gy += 4 * div(days, 1461);
  days %= 1461;
  if (days > 365) {
    gy += div(days - 1, 365);
    days = (days - 1) % 365;
  }
  let gd = days + 1;
  const leap = (gy % 4 === 0 && gy % 100 !== 0) || gy % 400 === 0;
  const monthDays = [0, 31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  let gm = 1;
  for (; gm <= 12 && gd > monthDays[gm]; gm++) gd -= monthDays[gm];
  return { gy, gm, gd };
}

export function formatShamsi(
  value: Date | string | number | null | undefined,
): string {
  if (value == null || value === "") return "";
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return "";
  const { jy, jm, jd } = gregorianToJalali(
    date.getFullYear(),
    date.getMonth() + 1,
    date.getDate(),
  );
  const hh = String(date.getHours()).padStart(2, "0");
  const mm = String(date.getMinutes()).padStart(2, "0");
  return `${jd} ${MONTHS[jm - 1]} ${jy}، ${hh}:${mm}`;
}

export function shamsiParts(date: Date) {
  const { jy, jm, jd } = gregorianToJalali(
    date.getFullYear(),
    date.getMonth() + 1,
    date.getDate(),
  );
  return {
    y: String(jy),
    m: String(jm),
    d: String(jd),
    hh: String(date.getHours()).padStart(2, "0"),
    mm: String(date.getMinutes()).padStart(2, "0"),
  };
}

export function dateFromShamsi(parts: {
  y: string;
  m: string;
  d: string;
  hh: string;
  mm: string;
}): Date | null {
  const y = Number(parts.y);
  const m = Number(parts.m);
  const d = Number(parts.d);
  const hh = Number(parts.hh);
  const mm = Number(parts.mm);
  if (![y, m, d, hh, mm].every((n) => Number.isInteger(n))) return null;
  if (y < 1300 || y > 1500 || m < 1 || m > 12 || d < 1 || d > 31) return null;
  if (hh < 0 || hh > 23 || mm < 0 || mm > 59) return null;
  const g = jalaliToGregorian(y, m, d);
  const date = new Date(g.gy, g.gm - 1, g.gd, hh, mm, 0, 0);
  if (
    date.getFullYear() !== g.gy ||
    date.getMonth() !== g.gm - 1 ||
    date.getDate() !== g.gd
  ) {
    return null;
  }
  return date;
}

const CALENDAR_DATE = /^(\d{4})-(\d{2})-(\d{2})$/;

// Formatting in UTC from a UTC midnight means the browser's own time zone can never move the day.
const DISPLAY_FORMAT = new Intl.DateTimeFormat('en-US', {
  timeZone: 'UTC',
  year: 'numeric',
  month: 'short',
  day: 'numeric',
});

/**
 * Formats an API calendar date (`YYYY-MM-DD`) as "Jan 1, 2026" without any time-zone
 * conversion. Never use `new Date('YYYY-MM-DD')` with local formatting: that parses as UTC
 * midnight and shows the previous day west of UTC. Unparseable input is returned unchanged
 * rather than hidden.
 */
export function formatCalendarDate(value: string): string {
  const utc = parseUtc(value);
  return utc ? DISPLAY_FORMAT.format(utc) : value;
}

/**
 * The calendar day a datepicker `Date` represents, as `YYYY-MM-DD`. Datepickers produce local
 * midnight, so the local components are the chosen day. `toISOString()` converts to UTC first
 * and sends the previous day anywhere east of UTC.
 */
export function toCalendarDate(date: Date): string {
  const pad = (n: number, width = 2) => String(n).padStart(width, '0');
  return `${pad(date.getFullYear(), 4)}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

/** Local midnight of a `YYYY-MM-DD` day, for a datepicker's value or limits; null if invalid. */
export function fromCalendarDate(value: string): Date | null {
  const utc = parseUtc(value);
  if (!utc) return null;
  const local = new Date(utc.getUTCFullYear(), utc.getUTCMonth(), utc.getUTCDate());
  // Years 0–99 would otherwise be read as 1900–1999.
  local.setFullYear(utc.getUTCFullYear());
  return local;
}

/** Calendar arithmetic on `YYYY-MM-DD`, done in UTC so no daylight-saving change can skip a day. */
export function addCalendarDays(value: string, days: number): string {
  const utc = parseUtc(value);
  if (!utc) throw new Error(`Not a calendar date: ${value}`);
  utc.setUTCDate(utc.getUTCDate() + days);
  return utc.toISOString().slice(0, 10);
}

function parseUtc(value: string): Date | null {
  const match = CALENDAR_DATE.exec(value);
  if (!match) return null;
  const [, year, month, day] = match.map(Number);
  const utc = new Date(Date.UTC(year, month - 1, day));
  utc.setUTCFullYear(year);
  if (utc.getUTCMonth() !== month - 1 || utc.getUTCDate() !== day) return null;
  return utc;
}

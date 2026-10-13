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
  const match = CALENDAR_DATE.exec(value);
  if (!match) return value;
  const [, year, month, day] = match.map(Number);
  const utc = new Date(Date.UTC(year, month - 1, day));
  if (utc.getUTCMonth() !== month - 1 || utc.getUTCDate() !== day) return value;
  return DISPLAY_FORMAT.format(utc);
}

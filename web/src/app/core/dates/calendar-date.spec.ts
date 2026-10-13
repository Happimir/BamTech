import { formatCalendarDate } from './calendar-date';

describe('formatCalendarDate', () => {
  afterEach(() => vi.unstubAllEnvs());

  // Far west and far east of UTC: a timezone-shifting implementation shows the wrong day in one.
  it.each(['America/Los_Angeles', 'Pacific/Kiritimati', 'UTC'])(
    'shows the same calendar day in %s',
    (timeZone) => {
      vi.stubEnv('TZ', timeZone);
      expect(formatCalendarDate('2026-01-01')).toBe('Jan 1, 2026');
      expect(formatCalendarDate('2024-02-29')).toBe('Feb 29, 2024');
      expect(formatCalendarDate('2025-12-31')).toBe('Dec 31, 2025');
    },
  );

  it.each(['', 'not a date', '2026-1-1', '2026-02-30', '2025-02-29'])(
    'returns unparseable or impossible input unchanged: %j',
    (value) => expect(formatCalendarDate(value)).toBe(value),
  );
});

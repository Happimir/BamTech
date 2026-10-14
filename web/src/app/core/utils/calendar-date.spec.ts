import { addCalendarDays, formatCalendarDate, fromCalendarDate, toCalendarDate } from './calendar-date';

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

describe('calendar dates sent to the API', () => {
  afterEach(() => vi.unstubAllEnvs());

  // UTC+14 and UTC−11: a UTC-converting serializer sends the wrong day east of UTC.
  it.each(['Pacific/Kiritimati', 'Pacific/Pago_Pago', 'UTC'])(
    'a picked day round-trips unchanged in %s',
    (timeZone) => {
      vi.stubEnv('TZ', timeZone);
      // What a datepicker emits for a click on Jan 1, 2026: local midnight.
      expect(toCalendarDate(new Date(2026, 0, 1))).toBe('2026-01-01');
      for (const day of ['2026-01-01', '2024-02-29', '2025-12-31', '0099-03-01']) {
        expect(toCalendarDate(fromCalendarDate(day)!)).toBe(day);
      }
    },
  );

  it.each(['', '2026-02-30', '2026-1-1'])('rejects an impossible day: %j', (value) =>
    expect(fromCalendarDate(value)).toBeNull(),
  );

  it.each([
    ['2026-03-01', -1, '2026-02-28'],
    ['2024-03-01', -1, '2024-02-29'],
    ['2026-01-01', -1, '2025-12-31'],
    ['2025-12-31', 1, '2026-01-01'],
    // US daylight saving starts on 2026-03-08.
    ['2026-03-09', -1, '2026-03-08'],
    ['2026-03-08', 1, '2026-03-09'],
  ])('%s %i day(s) is %s', (value, days, expected) => {
    vi.stubEnv('TZ', 'America/New_York');
    expect(addCalendarDays(value, days)).toBe(expected);
  });
});

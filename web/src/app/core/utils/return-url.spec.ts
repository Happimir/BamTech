import { DEFAULT_HOME_URL, safeReturnUrl } from './return-url';

describe('safeReturnUrl', () => {
  it.each(['/people', '/people/0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b', '/people?name=Al&page=2'])(
    'keeps the local app path %s',
    (url) => expect(safeReturnUrl(url)).toBe(url),
  );

  it.each([
    ['protocol-relative host', '//evil.example/people'],
    ['backslash host (browsers treat /\\ as //)', '/\\evil.example'],
    ['absolute URL', 'https://evil.example/'],
    ['javascript URL', 'javascript:alert(1)'],
    ['relative path', 'people'],
    ['empty', ''],
    ['missing', null],
    ['sign-in loop', '/login?returnUrl=%2Fpeople'],
    ['unavailable loop', '/unavailable'],
  ])('falls back to home for %s', (_case, url) => {
    expect(safeReturnUrl(url)).toBe(DEFAULT_HOME_URL);
  });
});

import { directoryReturnUrl } from './directory-return';

describe('directoryReturnUrl', () => {
  it('returns to the directory search and page the user came from', () => {
    expect(directoryReturnUrl({ returnTo: '/people?name=Gra&page=2', navigationId: 3 })).toBe(
      '/people?name=Gra&page=2',
    );
  });

  it.each([
    ['no state (deep link)', undefined],
    ['state without returnTo', { navigationId: 1 }],
    ['non-string returnTo', { returnTo: 42 }],
    ['another app page', { returnTo: '/people/0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b' }],
    ['the sign-in page', { returnTo: '/login' }],
    ['another origin', { returnTo: '//evil.example/people' }],
    ['a path merely starting with people', { returnTo: '/peoplex' }],
  ])('falls back to the plain directory for %s', (_case, state) => {
    expect(directoryReturnUrl(state)).toBe('/people');
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ActivatedRouteSnapshot, provideRouter, Router, RouterStateSnapshot, UrlTree } from '@angular/router';
import { AuthMe, Capability } from '../api/api.models';
import { authenticatedGuard, capabilityGuard } from './auth.guards';
import { AuthStore } from './auth.store';

const route = {} as ActivatedRouteSnapshot;
const state = { url: '/people?page=2' } as RouterStateSnapshot;

describe('route guards', () => {
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
  });

  /** Signs the store in (or not) through its real bootstrap path. */
  async function sessionWith(capabilities: Capability[] | null): Promise<void> {
    const done = TestBed.inject(AuthStore).bootstrap();
    http.expectOne('/auth/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    const me: AuthMe = { id: 'u', userName: 'u', roles: [], capabilities: capabilities ?? [] };
    if (capabilities) http.expectOne('/auth/me').flush(me);
    else http.expectOne('/auth/me').flush(null, { status: 401, statusText: 'Unauthorized' });
    await done;
  }

  const run = (guard: typeof authenticatedGuard) =>
    TestBed.runInInjectionContext(() => guard(route, state));
  const url = (result: unknown) => TestBed.inject(Router).serializeUrl(result as UrlTree);

  it('sends an anonymous visitor to sign-in with the attempted URL', async () => {
    await sessionWith(null);
    expect(url(run(authenticatedGuard))).toBe('/login?returnUrl=%2Fpeople%3Fpage%3D2');
  });

  it('lets an account with the capability through', async () => {
    await sessionWith(['People.Read']);
    expect(run(capabilityGuard('People.Read'))).toBe(true);
  });

  it('sends an account without the capability to access denied, not to sign-in', async () => {
    await sessionWith(['Duties.Write']);
    expect(url(run(capabilityGuard('People.Read')))).toBe('/forbidden');
  });
});

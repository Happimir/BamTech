import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AuthMe } from '../data-access/api.models';
import { AuthStore } from './auth.store';

const admin: AuthMe = {
  id: '0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b',
  userName: 'admin',
  roles: ['Admin'],
  capabilities: ['People.Read', 'People.Write', 'Duties.Write'],
};

describe('AuthStore', () => {
  let store: InstanceType<typeof AuthStore>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(AuthStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  /** Runs bootstrap, answering /auth/me with the given status (antiforgery succeeds). */
  async function bootstrapWithMe(status: number, body: object | null = null): Promise<void> {
    const done = store.bootstrap();
    http.expectOne('/auth/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
    await Promise.resolve();
    http.expectOne('/auth/me').flush(body, { status, statusText: String(status) });
    await done;
  }

  describe('bootstrap', () => {
    it('is authenticated with the account when /auth/me succeeds', async () => {
      await bootstrapWithMe(200, admin);
      expect(store.status()).toBe('authenticated');
      expect(store.capabilities().has('Duties.Write')).toBe(true);
    });

    it('is anonymous only when the server says 401', async () => {
      await bootstrapWithMe(401, { code: 'unauthenticated' });
      expect(store.status()).toBe('anonymous');
    });

    it.each([500, 503])('is unavailable, not signed out, on a %s', async (status) => {
      await bootstrapWithMe(status, { code: 'service_unavailable' });
      expect(store.status()).toBe('unavailable');
    });

    it('is unavailable when the antiforgery call cannot reach the server', async () => {
      const done = store.bootstrap();
      http.expectOne('/auth/antiforgery').error(new ProgressEvent('error'));
      await done;
      expect(store.status()).toBe('unavailable');
    });
  });

  describe('login', () => {
    it('maps the generic credentials failure without changing state', async () => {
      await bootstrapWithMe(401);
      const outcome = store.login({ userName: 'admin', password: 'wrong' });
      http
        .expectOne('/auth/login')
        .flush({ code: 'invalid_credentials' }, { status: 401, statusText: 'Unauthorized' });
      expect(await outcome).toBe('invalid-credentials');
      expect(store.status()).toBe('anonymous');
    });

    it('refreshes the antiforgery token after csrf_failed and does not resubmit', async () => {
      await bootstrapWithMe(401);
      const outcome = store.login({ userName: 'admin', password: 'pw' });
      http
        .expectOne('/auth/login')
        .flush({ code: 'csrf_failed' }, { status: 400, statusText: 'Bad Request' });
      await Promise.resolve();
      http.expectOne('/auth/antiforgery').flush(null, { status: 204, statusText: 'No Content' });
      expect(await outcome).toBe('csrf-failed');
      http.expectNone('/auth/login');
    });
  });

  describe('logout', () => {
    it('stays signed in when the server did not confirm the revocation', async () => {
      await bootstrapWithMe(200, admin);
      const result = store.logout();
      http.expectOne('/auth/logout').flush(null, { status: 503, statusText: 'Unavailable' });
      expect(await result).toBe(false);
      expect(store.status()).toBe('authenticated');
      expect(store.user()).toEqual(admin);
    });

    it('clears the account after a confirmed revocation', async () => {
      await bootstrapWithMe(200, admin);
      const result = store.logout();
      http.expectOne('/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
      expect(await result).toBe(true);
      expect(store.status()).toBe('anonymous');
      expect(store.user()).toBeNull();
    });
  });
});

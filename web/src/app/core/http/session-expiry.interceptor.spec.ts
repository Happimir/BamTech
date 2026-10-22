import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AuthStore } from '../auth/auth.store';
import { sessionExpiryInterceptor } from './session-expiry.interceptor';

describe('sessionExpiryInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let navigate: ReturnType<typeof vi.spyOn>;
  let sessionExpired: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([sessionExpiryInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    sessionExpired = vi.spyOn(TestBed.inject(AuthStore), 'sessionExpired');
  });

  afterEach(() => backend.verify());

  async function respond(url: string, status: number, method: 'GET' | 'POST' = 'GET') {
    const request =
      method === 'GET' ? firstValueFrom(http.get(url)) : firstValueFrom(http.post(url, {}));
    backend.expectOne(url).flush({ code: 'x' }, { status, statusText: String(status) });
    return request.catch((error: unknown) => error);
  }

  it('signs out and returns to sign-in when a protected API call gets 401', async () => {
    await respond('/graphql', 401, 'POST');
    expect(sessionExpired).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login'], { queryParams: { returnUrl: '/' } });
  });

  it('leaves a failed sign-in as an inline error', async () => {
    await respond('/auth/login', 401, 'POST');
    expect(sessionExpired).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });

  it('does not treat 403 as a lost session', async () => {
    await respond('/graphql', 403, 'POST');
    expect(sessionExpired).not.toHaveBeenCalled();
    expect(navigate).not.toHaveBeenCalled();
  });
});

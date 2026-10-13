import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { AuthMe } from '../api/api.models';

export interface Credentials {
  userName: string;
  password: string;
}

/** Stateless transport for the /auth endpoints. Session state lives in AuthStore. */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  /** Sets the XSRF-TOKEN cookie that Angular echoes in the X-XSRF-TOKEN header. */
  antiforgery(): Promise<unknown> {
    return firstValueFrom(this.http.get('/auth/antiforgery'));
  }

  me(): Promise<AuthMe> {
    return firstValueFrom(this.http.get<AuthMe>('/auth/me'));
  }

  login(credentials: Credentials): Promise<AuthMe> {
    return firstValueFrom(this.http.post<AuthMe>('/auth/login', credentials));
  }

  logout(): Promise<unknown> {
    return firstValueFrom(this.http.post('/auth/logout', null));
  }
}

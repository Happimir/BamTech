import { HttpErrorResponse } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { AuthMe, Capability } from '../api/api.models';
import { isUnavailable, problemCode } from '../http/http-errors';
import { AuthApi, Credentials } from './auth-api';

/**
 * `unavailable` means the session could not be established because the service failed;
 * it is deliberately distinct from `anonymous` so an outage never looks like a sign-out.
 */
export type AuthStatus = 'unknown' | 'anonymous' | 'authenticated' | 'unavailable';

export type LoginOutcome =
  | 'signed-in'
  | 'invalid-credentials'
  | 'csrf-failed'
  | 'unavailable'
  | 'failed';

interface AuthState {
  status: AuthStatus;
  user: AuthMe | null;
}

const signedOut: AuthState = { status: 'anonymous', user: null };

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<AuthState>({ status: 'unknown', user: null }),
  withComputed(({ status, user }) => ({
    isAuthenticated: computed(() => status() === 'authenticated'),
    capabilities: computed(() => new Set<Capability>(user()?.capabilities ?? [])),
  })),
  withMethods((store, api = inject(AuthApi)) => ({
    /** Establishes session state at startup. Never throws. */
    async bootstrap(): Promise<void> {
      try {
        await api.antiforgery();
        patchState(store, { status: 'authenticated', user: await api.me() });
      } catch (error) {
        const anonymous = error instanceof HttpErrorResponse && error.status === 401;
        patchState(store, anonymous ? signedOut : { status: 'unavailable', user: null });
      }
    },

    async login(credentials: Credentials): Promise<LoginOutcome> {
      try {
        patchState(store, { status: 'authenticated', user: await api.login(credentials) });
        return 'signed-in';
      } catch (error) {
        switch (problemCode(error)) {
          case 'invalid_credentials':
            return 'invalid-credentials';
          case 'csrf_failed':
            // Fetch a fresh token so the user's explicit resubmission can succeed.
            await api.antiforgery().catch(() => undefined);
            return 'csrf-failed';
        }
        return isUnavailable(error) ? 'unavailable' : 'failed';
      }
    },

    /** Resolves false when the server did not confirm the revocation; state is then unchanged. */
    async logout(): Promise<boolean> {
      try {
        await api.logout();
      } catch {
        return false;
      }
      patchState(store, signedOut);
      return true;
    },

    /** Called when a protected request is rejected with 401. */
    sessionExpired(): void {
      patchState(store, signedOut);
    },
  })),
);

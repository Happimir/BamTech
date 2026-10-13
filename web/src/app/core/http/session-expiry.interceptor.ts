import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';
import { AuthStore } from '../auth/auth.store';

/**
 * A 401 from a protected API call means the session is gone (expired, revoked or signed out
 * elsewhere): clear auth state and return to sign-in. /auth endpoints are excluded so a failed
 * sign-in stays an inline error and the startup /auth/me probe is handled by AuthStore.
 */
export const sessionExpiryInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  return next(req).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !req.url.startsWith('/auth/')
      ) {
        auth.sessionExpired();
        void router.navigate(['/login'], { queryParams: { returnUrl: router.url } });
      }
      return throwError(() => error);
    }),
  );
};

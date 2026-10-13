import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { DEFAULT_HOME_URL } from '../navigation/return-url';
import { AuthStore } from './auth.store';

/** Navigation UX only; the API enforces every policy. */
export const authenticatedGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  if (auth.isAuthenticated()) return true;
  const target = auth.status() === 'unavailable' ? '/unavailable' : '/login';
  return router.createUrlTree([target], { queryParams: { returnUrl: state.url } });
};

export const anonymousOnlyGuard: CanActivateFn = () =>
  inject(AuthStore).isAuthenticated() ? inject(Router).parseUrl(DEFAULT_HOME_URL) : true;

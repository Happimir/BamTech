import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Capability } from '../data-access/api.models';
import { DEFAULT_HOME_URL } from '../utils/return-url';
import { AuthStore } from './auth.store';

/** Navigation UX only; the API enforces every policy. */
export const authenticatedGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  const router = inject(Router);
  if (auth.isAuthenticated()) return true;
  const target = auth.status() === 'unavailable' ? '/unavailable' : '/login';
  return router.createUrlTree([target], { queryParams: { returnUrl: state.url } });
};

/**
 * Sends a signed-in account without the capability to the access-denied page instead of
 * letting every request fail with 403. Use after `authenticatedGuard`. UX only, like all guards.
 */
export function capabilityGuard(capability: Capability): CanActivateFn {
  return () => {
    const auth = inject(AuthStore);
    return auth.capabilities().has(capability) || inject(Router).parseUrl('/forbidden');
  };
}

export const anonymousOnlyGuard: CanActivateFn = () =>
  inject(AuthStore).isAuthenticated() ? inject(Router).parseUrl(DEFAULT_HOME_URL) : true;

import { Routes } from '@angular/router';
import { anonymousOnlyGuard, authenticatedGuard, capabilityGuard } from './core/auth/auth.guards';

/** Guards are navigation UX; the API authorizes every request. */
const canReadPeople = [authenticatedGuard, capabilityGuard('People.Read')];

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    title: 'ACTS · Astronaut Career Tracking System',
    // Full-width page with its own header; signed-in visitors go straight to the directory.
    data: { fullBleed: true },
    canActivate: [anonymousOnlyGuard],
    loadComponent: () => import('./features/landing/landing-page').then((m) => m.LandingPage),
  },
  {
    path: 'login',
    title: 'Sign in · ACTS',
    canActivate: [anonymousOnlyGuard],
    loadComponent: () => import('./features/login/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'unavailable',
    title: 'Unavailable · ACTS',
    loadComponent: () =>
      import('./features/status-pages/unavailable-page/unavailable-page').then((m) => m.UnavailablePage),
  },
  {
    path: 'people',
    title: 'People · ACTS',
    canActivate: canReadPeople,
    loadComponent: () => import('./features/people/people-page/people-page').then((m) => m.PeoplePage),
  },
  {
    path: 'people/:id',
    title: 'Person · ACTS',
    canActivate: canReadPeople,
    loadComponent: () =>
      import('./features/person-detail/person-detail-page/person-detail-page').then((m) => m.PersonDetailPage),
  },
  {
    path: 'forbidden',
    title: 'Access denied · ACTS',
    canActivate: [authenticatedGuard],
    loadComponent: () =>
      import('./features/status-pages/forbidden-page/forbidden-page').then((m) => m.ForbiddenPage),
  },
  {
    path: '**',
    title: 'Page not found · ACTS',
    loadComponent: () =>
      import('./features/status-pages/not-found-page/not-found-page').then((m) => m.NotFoundPage),
  },
];

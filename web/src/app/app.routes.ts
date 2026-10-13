import { Routes } from '@angular/router';
import { anonymousOnlyGuard, authenticatedGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'people' },
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
      import('./features/unavailable/unavailable-page').then((m) => m.UnavailablePage),
  },
  {
    path: 'people',
    title: 'People · ACTS',
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./features/people/people-page').then((m) => m.PeoplePage),
  },
  { path: '**', redirectTo: 'people' },
];

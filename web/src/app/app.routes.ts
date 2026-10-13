import { Routes } from '@angular/router';
import { anonymousOnlyGuard, authenticatedGuard } from './core/auth/auth.guards';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'people' },
  {
    path: 'login',
    canActivate: [anonymousOnlyGuard],
    loadComponent: () => import('./features/login/login-page').then((m) => m.LoginPage),
  },
  {
    path: 'unavailable',
    loadComponent: () =>
      import('./features/unavailable/unavailable-page').then((m) => m.UnavailablePage),
  },
  {
    path: 'people',
    canActivate: [authenticatedGuard],
    loadComponent: () => import('./features/people/people-page').then((m) => m.PeoplePage),
  },
  { path: '**', redirectTo: 'people' },
];

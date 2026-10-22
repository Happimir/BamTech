import { Routes } from '@angular/router';
import { provideActsGraphQL } from '../core/data-access/graphql/apollo';

/**
 * The directory and person records, loaded on first visit. Their GraphQL client is provided
 * here, so the public pages (landing, sign-in) never load it.
 */
export const peopleRoutes: Routes = [
  {
    path: '',
    providers: [provideActsGraphQL()],
    children: [
      {
        path: '',
        title: 'People · ACTS',
        loadComponent: () => import('./people/people-page/people-page').then((m) => m.PeoplePage),
      },
      {
        path: ':id',
        title: 'Person · ACTS',
        loadComponent: () =>
          import('./person-detail/person-detail-page/person-detail-page').then((m) => m.PersonDetailPage),
      },
    ],
  },
];

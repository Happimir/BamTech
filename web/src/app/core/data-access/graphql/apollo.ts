import { inject, Provider } from '@angular/core';
import { ApolloClient, InMemoryCache } from '@apollo/client';
import { provideApollo } from 'apollo-angular';
import { HttpLink } from 'apollo-angular/http';
import { PeopleApi } from '../people-api';
import possibleTypes from './possible-types';

/**
 * The cache. People and duties are normalized by `id`, so a write's payload updates the
 * person wherever they are cached. `possibleTypes` lets it match fragments on the Error
 * interface and the result unions.
 */
export function createActsCache(): InMemoryCache {
  return new InMemoryCache({
    possibleTypes: possibleTypes.possibleTypes,
    typePolicies: {
      // Has no ID of its own: always the current summary of the person it belongs to.
      AstronautSummary: { merge: true },
    },
  });
}

/**
 * The GraphQL client and the write transport, for the signed-in routes only (people.routes.ts):
 * the landing and sign-in pages never load Apollo, which keeps it out of the initial bundle.
 *
 * Apollo runs over Angular's HttpClient (HttpLink), so every operation passes through the app's
 * interceptors: the XSRF header on each POST, and a 401 ending the session. Reads always go to
 * the server: the feature stores own their state, and "current" must mean current, as the
 * concurrency checks depend on the version the user saw. Any GraphQL error fails the operation
 * (Apollo's default error policy); business errors are data in the payload instead.
 */
export function provideActsGraphQL(): Provider[] {
  return [provideActsApollo(), PeopleApi];
}

function provideActsApollo(): Provider {
  return provideApollo(
    (): ApolloClient.Options => ({
      link: inject(HttpLink).create({ uri: '/graphql' }),
      cache: createActsCache(),
      defaultOptions: {
        query: { fetchPolicy: 'network-only' },
      },
    }),
  );
}

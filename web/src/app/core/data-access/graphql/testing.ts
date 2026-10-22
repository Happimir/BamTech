// Test support only (imported by specs): Apollo's testing backend with the app's cache, and
// response fixtures shaped like the API's, including the __typename the cache needs.
import { importProvidersFrom, Provider, EnvironmentProviders } from '@angular/core';
import { APOLLO_TESTING_CACHE, ApolloTestingModule } from 'apollo-angular/testing';
import { AstronautSummary, Duty, PersonDetail } from '../api.models';
import { createActsCache } from './apollo';

export function provideApolloTesting(): (Provider | EnvironmentProviders)[] {
  return [
    importProvidersFrom(ApolloTestingModule),
    { provide: APOLLO_TESTING_CACHE, useFactory: createActsCache },
  ];
}

export function personFixture(overrides: Partial<PersonDetail> = {}): PersonDetail {
  return {
    __typename: 'Person',
    id: '019a2b3c-4d5e-7f60-8a71-b2c3d4e5f607',
    name: 'Ada Lovelace',
    version: 'v1',
    firstName: null,
    middleName: null,
    lastName: null,
    astronaut: null,
    ...overrides,
  } as PersonDetail;
}

export function astronautFixture(overrides: Partial<AstronautSummary> = {}): AstronautSummary {
  return {
    __typename: 'AstronautSummary',
    currentRank: 'Captain',
    currentDutyTitle: 'Commander',
    currentDutyStartDate: '2024-01-01',
    careerStartDate: '2020-01-01',
    careerEndDate: null,
    status: 'ACTIVE',
    ...overrides,
  } as AstronautSummary;
}

export function dutyFixture(overrides: Partial<Duty> = {}): Duty {
  return {
    __typename: 'Duty',
    id: 'd1',
    rank: 'Captain',
    dutyTitle: 'Commander',
    dutyStartDate: '2024-01-01',
    dutyEndDate: null,
    isRetirement: false,
    ...overrides,
  } as Duty;
}

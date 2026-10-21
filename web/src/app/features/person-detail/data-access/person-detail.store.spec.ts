import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ApolloTestingController, TestOperation } from 'apollo-angular/testing';
import { PersonDetailDocument } from '../../../core/data-access/graphql/generated';
import {
  astronautFixture,
  dutyFixture,
  personFixture,
  provideApolloTesting,
} from '../../../core/data-access/graphql/testing';
import { PersonDetailStore } from './person-detail.store';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PersonDetailStore],
  template: '',
})
class StoreHost {
  readonly store = inject(PersonDetailStore);
}

const ID = '019a2b3c-4d5e-7f60-8a71-b2c3d4e5f607';

const retiredPerson = {
  person: {
    ...personFixture({
      id: ID,
      name: 'Harold Jensen',
      astronaut: astronautFixture({
        currentRank: 'Lieutenant',
        currentDutyTitle: 'RETIRED',
        currentDutyStartDate: '2023-01-01',
        careerStartDate: '2012-03-01',
        careerEndDate: '2022-12-31',
        status: 'RETIRED',
      }),
    }),
    duties: [
      dutyFixture({ id: 'd2', rank: 'Lieutenant', dutyTitle: 'RETIRED', dutyStartDate: '2023-01-01', isRetirement: true }),
      dutyFixture({ id: 'd1', rank: 'Ensign', dutyTitle: 'Pilot', dutyStartDate: '2012-03-01', dutyEndDate: '2022-12-31' }),
    ],
  },
};

describe('PersonDetailStore', () => {
  let apollo: ApolloTestingController;
  let harness: RouterTestingHarness;
  let store: InstanceType<typeof PersonDetailStore>;

  async function open(url: string): Promise<void> {
    harness = await RouterTestingHarness.create();
    store = (await harness.navigateByUrl(url, StoreHost)).store;
    TestBed.tick();
  }

  async function respond(answer: (operation: TestOperation) => void): Promise<void> {
    const operation = apollo.expectOne(PersonDetailDocument);
    expect(operation.operation.variables).toEqual({ id: ID });
    answer(operation);
    TestBed.tick();
    await harness.fixture.whenStable();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'people/:id', component: StoreHost }]), provideApolloTesting()],
    });
    apollo = TestBed.inject(ApolloTestingController);
  });

  afterEach(() => apollo.verify());

  it('treats a malformed ID as not found without making a request', async () => {
    await open('/people/not-a-uuid');
    expect(store.view()).toBe('not-found');
    // apollo.verify() in afterEach fails if any operation was sent.
  });

  it('derives summary, status and timeline from the one person-and-duties snapshot', async () => {
    await open(`/people/${ID}`);
    expect(store.view()).toBe('loading');
    await respond((op) => op.flushData(retiredPerson));

    expect(store.view()).toBe('ready');
    expect(store.person()?.name).toBe('Harold Jensen');
    expect(store.status()).toBe('retired');
    expect(store.timeline().map((item) => item.id)).toEqual(['d2', 'd1']);
    expect(store.latestDutyStart()).toBe('2023-01-01');
  });

  it('shows not found for an unknown ID (a null person)', async () => {
    await open(`/people/${ID}`);
    await respond((op) => op.flushData({ person: null }));
    expect(store.view()).toBe('not-found');
  });

  it.each([
    ['forbidden', 'forbidden'],
    ['internal_error', 'error'],
  ] as const)('maps a %s error to the %s view', async (code, view) => {
    await open(`/people/${ID}`);
    await respond((op) => op.graphqlErrors([{ message: code, extensions: { code } }]));
    expect(store.view()).toBe(view);
  });

  it('retries the same person after a failure', async () => {
    await open(`/people/${ID}`);
    await respond((op) => op.graphqlErrors([{ message: 'busy', extensions: { code: 'service_unavailable' } }]));
    expect(store.isUnavailable()).toBe(true);

    store.reload();
    TestBed.tick();
    await respond((op) => op.flushData(retiredPerson));
    expect(store.view()).toBe('ready');
  });
});

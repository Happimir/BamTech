import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { ApolloTestingController, TestOperation } from 'apollo-angular/testing';
import { PeoplePage } from '../../../core/data-access/api.models';
import { PeopleDocument } from '../../../core/data-access/graphql/generated';
import { personFixture, provideApolloTesting } from '../../../core/data-access/graphql/testing';
import { PeopleStore } from './people.store';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PeopleStore],
  template: '',
})
class StoreHost {
  readonly store = inject(PeopleStore);
}

function page(names: string[], totalCount = names.length, pageNumber = 1): { people: PeoplePage } {
  return {
    people: {
      __typename: 'PersonPage',
      items: names.map((name, i) => personFixture({ id: `id-${pageNumber}-${i}`, name })),
      page: pageNumber,
      pageSize: 25,
      totalCount,
      totalPages: Math.ceil(totalCount / 25),
    } as PeoplePage,
  };
}

describe('PeopleStore', () => {
  let apollo: ApolloTestingController;
  let harness: RouterTestingHarness;
  let store: InstanceType<typeof PeopleStore>;

  async function open(url: string): Promise<void> {
    harness = await RouterTestingHarness.create();
    const host = await harness.navigateByUrl(url, StoreHost);
    store = host.store;
    TestBed.tick();
  }

  /** Lets the resource apply a flushed response (it settles asynchronously). */
  async function settle(): Promise<void> {
    TestBed.tick();
    await harness.fixture.whenStable();
  }

  /** The single outstanding People query (fails if there are zero or several). */
  function listRequest(): TestOperation {
    TestBed.tick();
    return apollo.expectOne(PeopleDocument);
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'people', component: StoreHost }]), provideApolloTesting()],
    });
    apollo = TestBed.inject(ApolloTestingController);
  });

  afterEach(() => apollo.verify());

  it('requests the first page with the default size and no name filter', async () => {
    await open('/people');
    const request = listRequest();
    expect(request.operation.variables).toEqual({ page: 1, pageSize: 25, name: null });
    expect(store.view()).toBe('loading');
  });

  it('applies a search from the first page with exactly one request', async () => {
    await open('/people?page=3');
    listRequest().flushData(page(['Ada'], 80, 3));
    await settle();

    await store.search('  Ada ');
    expect(TestBed.inject(Router).url).toBe('/people?name=Ada');
    expect(listRequest().operation.variables).toEqual({ page: 1, pageSize: 25, name: 'Ada' });
  });

  it('starts from the first page when the page size changes', async () => {
    await open('/people?page=2');
    listRequest().flushData(page(['Ada'], 60, 2));
    await settle();

    await store.changePage(1, 50);
    expect(TestBed.inject(Router).url).toBe('/people?pageSize=50');
    listRequest().flushData(page(['Ada'], 60));
    await settle();
  });

  it('keeps the previous rows marked as refreshing until the next page arrives', async () => {
    await open('/people');
    listRequest().flushData(page(['Ada', 'Ben'], 30));
    await settle();
    // The page renders the settled rows before the user can page (that read is what is kept).
    expect(store.result()?.items.map((p) => p.name)).toEqual(['Ada', 'Ben']);

    await store.changePage(1, 25);
    const next = listRequest();
    expect(store.isRefreshing()).toBe(true);
    expect(store.result()?.items.map((p) => p.name)).toEqual(['Ada', 'Ben']);

    next.flushData(page(['Cy'], 30, 2));
    await settle();
    expect(store.isRefreshing()).toBe(false);
    expect(store.result()?.items.map((p) => p.name)).toEqual(['Cy']);
  });

  it('ignores a superseded response so a slow one cannot overwrite newer results', async () => {
    await open('/people');
    listRequest().flushData(page(['Ada'], 30));
    await settle();

    await store.search('B');
    TestBed.tick();
    const slow = apollo.expectOne((op) => op.variables['name'] === 'B');
    await store.search('C');
    TestBed.tick();
    apollo.expectOne((op) => op.variables['name'] === 'C').flushData(page(['Cy']));
    await settle();
    slow.flushData(page(['Bea']));
    await settle();
    expect(store.result()?.items.map((p) => p.name)).toEqual(['Cy']);
  });

  it('shows an error, not stale rows, when a request fails', async () => {
    await open('/people');
    listRequest().flushData(page(['Ada'], 30));
    await settle();

    await store.changePage(1, 25);
    listRequest().graphqlErrors([{ message: 'busy', extensions: { code: 'service_unavailable' } }]);
    await settle();
    expect(store.view()).toBe('error');
    expect(store.isUnavailable()).toBe(true);
    expect(store.result()).toBeUndefined();
  });

  it('distinguishes an empty directory from a search with no matches', async () => {
    await open('/people');
    listRequest().flushData(page([], 0));
    await settle();
    expect(store.view()).toBe('empty');

    await store.search('Zed');
    listRequest().flushData(page([], 0));
    await settle();
    expect(store.view()).toBe('no-matches');
  });

  it('reports a page past the end instead of looking empty', async () => {
    await open('/people?page=9');
    listRequest().flushData(page([], 30, 9));
    await settle();
    expect(store.view()).toBe('past-end');

    await store.goToLastPage();
    expect(TestBed.inject(Router).url).toBe('/people?page=2');
    listRequest().flushData(page(['Zoe'], 30, 2));
    await settle();
  });
});

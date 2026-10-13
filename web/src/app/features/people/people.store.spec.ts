import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting, TestRequest } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PagedResponse, PersonSummary } from '../../core/api/api.models';
import { PeopleStore } from './people.store';

@Component({
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PeopleStore],
  template: '',
})
class StoreHost {
  readonly store = inject(PeopleStore);
}

function page(names: string[], totalCount = names.length, pageNumber = 1): PagedResponse<PersonSummary> {
  return {
    items: names.map((name, i) => ({ id: `id-${i}`, name, version: 'v', astronaut: null })),
    page: pageNumber,
    pageSize: 25,
    totalCount,
    totalPages: Math.ceil(totalCount / 25),
  };
}

describe('PeopleStore', () => {
  let http: HttpTestingController;
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

  /** The single outstanding list request (fails if there are zero or several). */
  function listRequest(): TestRequest {
    TestBed.tick();
    return http.expectOne((r) => r.url === '/api/people');
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'people', component: StoreHost }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests the first page with the default size and no name filter', async () => {
    await open('/people');
    const request = listRequest();
    expect(request.request.params.keys().sort()).toEqual(['page', 'pageSize']);
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('25');
    expect(store.view()).toBe('loading');
  });

  it('applies a search from the first page with exactly one request', async () => {
    await open('/people?page=3');
    listRequest().flush(page(['Ada'], 80, 3));
    await settle();

    await store.search('  Ada ');
    expect(TestBed.inject(Router).url).toBe('/people?name=Ada');
    const request = listRequest();
    expect(request.request.params.get('name')).toBe('Ada');
    expect(request.request.params.get('page')).toBe('1');
  });

  it('starts from the first page when the page size changes', async () => {
    await open('/people?page=2');
    listRequest().flush(page(['Ada'], 60, 2));
    await settle();

    await store.changePage(1, 50);
    expect(TestBed.inject(Router).url).toBe('/people?pageSize=50');
    listRequest().flush(page(['Ada'], 60));
    await settle();
  });

  it('keeps the previous rows marked as refreshing until the next page arrives', async () => {
    await open('/people');
    listRequest().flush(page(['Ada', 'Ben'], 30));
    await settle();
    // The page renders the settled rows before the user can page (that read is what is kept).
    expect(store.result()?.items.map((p) => p.name)).toEqual(['Ada', 'Ben']);

    await store.changePage(1, 25);
    const next = listRequest();
    expect(store.isRefreshing()).toBe(true);
    expect(store.result()?.items.map((p) => p.name)).toEqual(['Ada', 'Ben']);

    next.flush(page(['Cy'], 30, 2));
    await settle();
    expect(store.isRefreshing()).toBe(false);
    expect(store.result()?.items.map((p) => p.name)).toEqual(['Cy']);
  });

  it('cancels a superseded request so a slow response cannot overwrite newer results', async () => {
    await open('/people');
    listRequest().flush(page(['Ada'], 30));
    await settle();

    await store.search('B');
    TestBed.tick();
    const slow = http.expectOne((r) => r.params.get('name') === 'B');
    await store.search('C');
    TestBed.tick();
    expect(slow.cancelled).toBe(true);
    http.expectOne((r) => r.params.get('name') === 'C').flush(page(['Cy']));
    await settle();
    expect(store.result()?.items.map((p) => p.name)).toEqual(['Cy']);
  });

  it('shows an error, not stale rows, when a request fails', async () => {
    await open('/people');
    listRequest().flush(page(['Ada'], 30));
    await settle();

    await store.changePage(1, 25);
    listRequest().flush({ code: 'service_unavailable' }, { status: 503, statusText: 'Unavailable' });
    await settle();
    expect(store.view()).toBe('error');
    expect(store.isUnavailable()).toBe(true);
    expect(store.result()).toBeUndefined();
  });

  it('distinguishes an empty directory from a search with no matches', async () => {
    await open('/people');
    listRequest().flush(page([], 0));
    await settle();
    expect(store.view()).toBe('empty');

    await store.search('Zed');
    listRequest().flush(page([], 0));
    await settle();
    expect(store.view()).toBe('no-matches');
  });

  it('reports a page past the end instead of looking empty', async () => {
    await open('/people?page=9');
    listRequest().flush(page([], 30, 9));
    await settle();
    expect(store.view()).toBe('past-end');

    await store.goToLastPage();
    expect(TestBed.inject(Router).url).toBe('/people?page=2');
    listRequest().flush(page(['Zoe'], 30, 2));
    await settle();
  });
});

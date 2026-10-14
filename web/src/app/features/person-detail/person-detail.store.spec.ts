import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { PersonDuties } from '../../core/api/api.models';
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

const retiredSnapshot: PersonDuties = {
  person: {
    id: ID,
    name: 'Harold Jensen',
    version: 'v',
    firstName: null,
    middleName: null,
    lastName: null,
    astronaut: {
      currentRank: 'Lieutenant',
      currentDutyTitle: 'RETIRED',
      currentDutyStartDate: '2023-01-01',
      careerStartDate: '2012-03-01',
      careerEndDate: '2022-12-31',
      status: 'retired',
    },
  },
  duties: [
    { id: 'd2', rank: 'Lieutenant', dutyTitle: 'RETIRED', dutyStartDate: '2023-01-01', dutyEndDate: null, isRetirement: true },
    { id: 'd1', rank: 'Ensign', dutyTitle: 'Pilot', dutyStartDate: '2012-03-01', dutyEndDate: '2022-12-31', isRetirement: false },
  ],
};

describe('PersonDetailStore', () => {
  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  let store: InstanceType<typeof PersonDetailStore>;

  async function open(url: string): Promise<void> {
    harness = await RouterTestingHarness.create();
    store = (await harness.navigateByUrl(url, StoreHost)).store;
    TestBed.tick();
  }

  async function respond(status: number, body: object | null): Promise<void> {
    http.expectOne(`/api/people/${ID}/duties`).flush(body, { status, statusText: String(status) });
    TestBed.tick();
    await harness.fixture.whenStable();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'people/:id', component: StoreHost }]),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('treats a malformed ID as not found without making a request', async () => {
    await open('/people/not-a-uuid');
    expect(store.view()).toBe('not-found');
    // http.verify() in afterEach fails if any request was made.
  });

  it('derives summary, status and timeline from the one duties snapshot', async () => {
    await open(`/people/${ID}`);
    expect(store.view()).toBe('loading');
    await respond(200, retiredSnapshot);

    expect(store.view()).toBe('ready');
    expect(store.person()?.name).toBe('Harold Jensen');
    expect(store.status()).toBe('retired');
    expect(store.timeline().map((item) => item.id)).toEqual(['d2', 'd1']);
  });

  it.each([
    [404, 'not-found'],
    [403, 'forbidden'],
    [500, 'error'],
  ] as const)('maps a %s response to the %s view', async (status, view) => {
    await open(`/people/${ID}`);
    await respond(status, { code: 'x' });
    expect(store.view()).toBe(view);
  });

  it('retries the same person after a failure', async () => {
    await open(`/people/${ID}`);
    await respond(503, { code: 'service_unavailable' });
    expect(store.isUnavailable()).toBe(true);

    store.reload();
    TestBed.tick();
    await respond(200, retiredSnapshot);
    expect(store.view()).toBe('ready');
  });
});

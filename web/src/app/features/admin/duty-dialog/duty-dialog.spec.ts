import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, computed, Injector, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { PersonDetail } from '../../../core/data-access/api.models';
import { personStatus } from '../../../shared/utils/person-status';
import { PersonDetailStore } from '../../person-detail/data-access/person-detail.store';
import { DutyDialog, DutySaved } from './duty-dialog';
import { ADMIN_DIALOG_CONFIG } from '../shared/write-outcome';

const ID = '019a2b3c-4d5e-7f60-8a71-b2c3d4e5f607';
const DUTIES_URL = `/api/people/${ID}/duties`;

const active: PersonDetail = {
  id: ID,
  name: 'Grace Okonkwo',
  version: 'version-1',
  firstName: null,
  middleName: null,
  lastName: null,
  astronaut: {
    currentRank: 'Captain',
    currentDutyTitle: 'Commander',
    currentDutyStartDate: '2024-06-01',
    careerStartDate: '2015-01-01',
    careerEndDate: null,
    status: 'active',
  },
};

const retired: PersonDetail = {
  ...active,
  astronaut: { ...active.astronaut!, currentDutyTitle: 'RETIRED', careerEndDate: '2024-05-31', status: 'retired' },
};

const unassigned: PersonDetail = { ...active, astronaut: null };

/** Stands in for the detail page's store: the dialog reads the person and version live from it. */
function fakeStore(person: PersonDetail, latestDutyStart: string | null) {
  const current = signal(person);
  return {
    person: current.asReadonly(),
    status: computed(() => personStatus(current().astronaut)),
    latestDutyStart: signal(latestDutyStart).asReadonly(),
    reload: vi.fn(),
    current,
  };
}

describe('DutyDialog', () => {
  let http: HttpTestingController;
  let ref: MatDialogRef<DutyDialog, DutySaved>;
  let dialog: DutyDialog;
  let closedWith: DutySaved | undefined | 'open';

  // The submission action resolves after HTTP, outside Angular's pending tasks: drain a macrotask too.
  const settle = async () => {
    TestBed.tick();
    await TestBed.inject(ApplicationRef).whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    await TestBed.inject(ApplicationRef).whenStable();
  };
  const form = () => dialog['dutyForm'];
  const overlay = () => document.querySelector('.cdk-overlay-container') as HTMLElement;
  const saveButton = () => overlay().querySelector<HTMLButtonElement>('button[type="submit"]')!;
  const notice = () => overlay().querySelector('.notice')?.textContent?.trim() ?? '';

  async function open(
    store: ReturnType<typeof fakeStore>,
    metadata: { businessDate: string; businessTimeZone: string } | 'fails' = {
      businessDate: '2026-10-14',
      businessTimeZone: 'UTC',
    },
  ): Promise<void> {
    const injector = Injector.create({
      providers: [{ provide: PersonDetailStore, useValue: store }],
      parent: TestBed.inject(Injector),
    });
    ref = TestBed.inject(MatDialog).open(DutyDialog, { ...ADMIN_DIALOG_CONFIG, injector });
    closedWith = 'open';
    ref.afterClosed().subscribe((result) => (closedWith = result));
    dialog = ref.componentInstance;
    await settle();
    const request = http.expectOne('/api/metadata');
    if (metadata === 'fails') request.flush(null, { status: 503, statusText: 'Unavailable' });
    else request.flush(metadata);
    await settle();
  }

  async function fill(values: { rank?: string; kind?: 'duty' | 'retire'; title?: string; start?: Date | null }) {
    if (values.rank !== undefined) form().rank().value.set(values.rank);
    if (values.kind) form().kind().value.set(values.kind);
    if (values.title !== undefined) form().dutyTitle().value.set(values.title);
    if (values.start !== undefined) form().dutyStartDate().value.set(values.start);
    await settle();
  }

  async function save(): Promise<void> {
    saveButton().click();
    await settle();
  }

  async function respond(status: number, body: object | null): Promise<void> {
    const request = http.expectOne(DUTIES_URL);
    if (status === 0) request.error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });
    else request.flush(body, { status, statusText: String(status) });
    await settle();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    TestBed.inject(MatDialog).closeAll();
    http.verify();
    vi.unstubAllEnvs();
  });

  it('sends the picked calendar day, the canonical retirement title and the version read', async () => {
    // Far east of UTC: toISOString() would send 2025-12-31 for this pick.
    vi.stubEnv('TZ', 'Pacific/Kiritimati');
    await open(fakeStore(active, '2024-06-01'));
    await fill({ kind: 'retire', start: new Date(2026, 0, 1) });
    await save();

    const request = http.expectOne(DUTIES_URL);
    expect(request.request.body).toEqual({
      rank: 'Captain',
      dutyTitle: 'RETIRED',
      dutyStartDate: '2026-01-01',
      expectedVersion: 'version-1',
    });
    request.flush({ id: 'd', personId: ID, version: 'version-2' }, { status: 201, statusText: 'Created' });
    await settle();
    expect(closedWith).toBe('Retirement recorded');
  });

  it('previews the derived dates instead of asking for them', async () => {
    await open(fakeStore(active, '2024-06-01'));
    await fill({ kind: 'retire', start: new Date(2026, 2, 1) });
    expect(dialog['previews']()).toEqual(['Commander ends Feb 28, 2026.', 'Career ends Feb 28, 2026.']);
  });

  it('explains reactivation for a retired person and offers no second retirement', async () => {
    await open(fakeStore(retired, '2024-06-01'));
    await fill({ title: 'Pilot', start: new Date(2025, 0, 1) });

    expect(overlay().querySelectorAll('mat-radio-button')).toHaveLength(0);
    expect(dialog['previews']()).toContain(
      'Returns them to active duty: the career end clears, and the retirement stays in the history.',
    );
  });

  it.each([
    [retired, 'This person is already retired. Enter an active duty to return them to service.'],
    [unassigned, "A first duty can't be a retirement."],
    [active, 'To record a retirement, choose Retire.'],
  ])('blocks a typed retirement title without a request (%#)', async (person, message) => {
    await open(fakeStore(person, person.astronaut ? '2024-06-01' : null));
    await fill({ title: ' retired ', start: new Date(2025, 0, 1) });
    await save();

    expect(form().dutyTitle().errors().map((e) => e.message)).toEqual([message]);
    // afterEach: http.verify() fails if the duty was sent.
  });

  it('rejects, before sending, a start that is not after the current duty or is after the business date', async () => {
    await open(fakeStore(active, '2024-06-01'));
    await fill({ title: 'Pilot', start: new Date(2024, 5, 1) });
    await save();
    // The datepicker's own min error is also present, but has no message to show.
    expect(form().dutyStartDate().errors().map((e) => e.kind)).toContain('matDatepickerMin');
    expect(dialog['startDateMessages']()).toEqual(['Must be after Jun 1, 2024, when the current duty started.']);

    await fill({ start: new Date(2026, 9, 15) });
    await save();
    expect(dialog['startDateMessages']()).toEqual(["Can't be after today, Oct 14, 2026 (UTC)."]);
  });

  it('explains unreadable date text instead of showing an empty error', async () => {
    await open(fakeStore(active, '2024-06-01'));
    const input = overlay().querySelector<HTMLInputElement>('input[name$="dutyStartDate"]')!;
    input.value = 'next tuesday';
    input.dispatchEvent(new Event('input'));
    input.dispatchEvent(new Event('blur'));
    await settle();

    expect(dialog['startDateMessages']()).toEqual(['Enter a start date, such as 3/14/2026.']);
  });

  it('never substitutes the browser date when the business date is unavailable', async () => {
    await open(fakeStore(active, '2024-06-01'), 'fails');
    expect(overlay().textContent).toContain("Today's business date is unavailable");

    // Far in the future, but only the server may refuse it.
    await fill({ title: 'Pilot', start: new Date(2099, 0, 1) });
    await save();
    await respond(400, { code: 'validation', errors: { dutyStartDate: ['Duty start date cannot be in the future.'] } });

    expect(dialog['startDateMessages']()).toEqual(['Duty start date cannot be in the future.']);
    expect(closedWith).toBe('open');
  });

  it('puts a server chronology conflict on the date and keeps the draft', async () => {
    await open(fakeStore(active, '2024-06-01'));
    await fill({ title: 'Pilot', start: new Date(2025, 0, 1) });
    await save();
    await respond(409, { code: 'chronology_conflict', detail: 'A new duty must start after 2025-02-01.' });

    expect(dialog['startDateMessages']()).toEqual(['A new duty must start after 2025-02-01.']);
    expect(form().dutyTitle().value()).toBe('Pilot');
    expect(closedWith).toBe('open');
  });

  it('on a stale version, keeps the draft and resubmits only when asked, with the reloaded version', async () => {
    const store = fakeStore(active, '2024-06-01');
    await open(store);
    await fill({ title: 'Pilot', start: new Date(2025, 0, 1) });
    await save();
    await respond(409, { code: 'concurrency_conflict' });

    expect(notice()).toContain('Someone else changed this person');
    overlay().querySelector<HTMLButtonElement>('.notice button')!.click();
    expect(store.reload).toHaveBeenCalledTimes(1);
    store.current.set({ ...active, version: 'version-2' });
    await settle();
    // Nothing was replayed automatically.
    http.verify();

    await save();
    expect(http.expectOne(DUTIES_URL).request.body.expectedVersion).toBe('version-2');
  });

  it('after an unknown outcome, reloads the record for review and does not retry', async () => {
    const store = fakeStore(active, '2024-06-01');
    await open(store);
    await fill({ title: 'Pilot', start: new Date(2025, 0, 1) });
    await save();
    await respond(0, null);

    expect(store.reload).toHaveBeenCalledTimes(1);
    expect(notice()).toContain("The save couldn't be confirmed");
    expect(closedWith).toBe('open');
  });

  it('cannot be closed or resubmitted while a save is in flight', async () => {
    await open(fakeStore(active, '2024-06-01'));
    await fill({ title: 'Pilot', start: new Date(2025, 0, 1) });
    await save();

    expect(saveButton().disabled).toBe(true);
    expect(saveButton().textContent?.trim()).toBe('Saving…');
    expect(ref.disableClose).toBe(true);
    await respond(503, { code: 'service_unavailable' });
    expect(saveButton().disabled).toBe(false);
    expect(ref.disableClose).toBe(false);
    expect(notice()).toContain('Nothing was saved');
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationRef, Injector, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { PersonDetail } from '../../core/api/api.models';
import { PersonDetailStore } from '../person-detail/person-detail.store';
import { PersonDialog, PersonDialogMode, PersonDialogResult } from './person-dialog';
import { ADMIN_DIALOG_CONFIG } from './write-outcome';

const ID = '019a2b3c-4d5e-7f60-8a71-b2c3d4e5f607';

const person: PersonDetail = {
  id: ID,
  name: 'Grace Okonkwo',
  version: 'version-1',
  firstName: 'Grace',
  middleName: null,
  lastName: 'Okonkwo',
  astronaut: null,
};

describe('PersonDialog', () => {
  let http: HttpTestingController;
  let ref: MatDialogRef<PersonDialog, PersonDialogResult>;
  let dialog: PersonDialog;
  let closedWith: PersonDialogResult | undefined | 'open';

  // The submission action resolves after HTTP, outside Angular's pending tasks: drain a macrotask too.
  const settle = async () => {
    TestBed.tick();
    await TestBed.inject(ApplicationRef).whenStable();
    await new Promise((resolve) => setTimeout(resolve));
    TestBed.tick();
    await TestBed.inject(ApplicationRef).whenStable();
  };
  const form = () => dialog['nameForm'];
  const overlay = () => document.querySelector('.cdk-overlay-container') as HTMLElement;
  const saveButton = () => overlay().querySelector<HTMLButtonElement>('button[type="submit"]')!;
  const notice = () => overlay().querySelector('.notice')?.textContent?.trim() ?? '';

  async function open(mode: PersonDialogMode, store?: object): Promise<void> {
    const injector = Injector.create({
      providers: store ? [{ provide: PersonDetailStore, useValue: store }] : [],
      parent: TestBed.inject(Injector),
    });
    ref = TestBed.inject(MatDialog).open(PersonDialog, { ...ADMIN_DIALOG_CONFIG, data: mode, injector });
    closedWith = 'open';
    ref.afterClosed().subscribe((result) => (closedWith = result));
    dialog = ref.componentInstance;
    await settle();
  }

  async function fill(values: Partial<Record<'name' | 'firstName' | 'middleName' | 'lastName', string>>) {
    for (const [field, value] of Object.entries(values)) {
      form()[field as keyof typeof values]().value.set(value);
    }
    await settle();
  }

  async function save(): Promise<void> {
    saveButton().click();
    await settle();
  }

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    TestBed.inject(MatDialog).closeAll();
    http.verify();
  });

  it('creates with the display name as typed, trimmed, and absent parts as null', async () => {
    await open('create');
    await fill({ name: '  Ada  Lovelace ', firstName: ' Ada ', middleName: '   ' });
    await save();

    const request = http.expectOne('/api/people');
    expect(request.request.body).toEqual({ name: 'Ada  Lovelace', firstName: 'Ada', middleName: null, lastName: null });
    request.flush({ ...person, name: 'Ada  Lovelace' }, { status: 201, statusText: 'Created' });
    await settle();
    expect(closedWith).toEqual({ kind: 'created', person: { ...person, name: 'Ada  Lovelace' } });
  });

  it('offers a name built from the parts but never applies it by itself', async () => {
    await open('create');
    await fill({ name: 'Okonkwo Grace', firstName: 'Grace', lastName: 'Okonkwo' });

    expect(dialog['suggestion']()).toBe('Grace Okonkwo');
    expect(form().name().value()).toBe('Okonkwo Grace');

    overlay().querySelector<HTMLButtonElement>('.suggestion button')!.click();
    await settle();
    expect(form().name().value()).toBe('Grace Okonkwo');
    expect(dialog['suggestion']()).toBeNull();
  });

  it('puts server field errors on their inputs and keeps the dialog open', async () => {
    await open('create');
    await fill({ name: 'Ada', lastName: 'Lovelace' });
    await save();
    http
      .expectOne('/api/people')
      .flush({ code: 'validation', errors: { lastName: ['Must be at most 100 characters.'] } }, { status: 400, statusText: 'Bad Request' });
    await settle();

    expect(form().lastName().errors().map((e) => e.message)).toEqual(['Must be at most 100 characters.']);
    expect(closedWith).toBe('open');
  });

  it('after an unconfirmed create, requires an explicit decision before creating again', async () => {
    await open('create');
    await fill({ name: 'Ada Lovelace' });
    await save();
    http.expectOne('/api/people').error(new ProgressEvent('timeout'), { status: 0, statusText: 'Request timeout' });
    await settle();

    expect(notice()).toContain('Creation may have succeeded. Check the directory for "Ada Lovelace"');
    expect(overlay().querySelector('.notice a')?.getAttribute('href')).toBe('/people?name=Ada%20Lovelace');
    expect(saveButton().textContent?.trim()).toBe('Create anyway');
    // Nothing is retried on its own.
    http.verify();

    await save();
    http.expectOne('/api/people').flush(person, { status: 201, statusText: 'Created' });
    await settle();
    expect(closedWith).toEqual({ kind: 'created', person });
  });

  it('renames with the version read; on a conflict it reloads only when asked and resubmits the new version', async () => {
    const current = signal(person);
    const store = { person: current.asReadonly(), reload: vi.fn() };
    await open('rename', store);
    expect(form().name().value()).toBe('Grace Okonkwo');
    await fill({ name: 'Grace A. Okonkwo' });
    await save();

    const first = http.expectOne(`/api/people/${ID}`);
    expect(first.request.body).toEqual({
      name: 'Grace A. Okonkwo',
      firstName: 'Grace',
      middleName: null,
      lastName: 'Okonkwo',
      version: 'version-1',
    });
    first.flush({ code: 'concurrency_conflict' }, { status: 409, statusText: 'Conflict' });
    await settle();
    expect(notice()).toContain('Someone else changed this person');
    expect(store.reload).not.toHaveBeenCalled();

    overlay().querySelector<HTMLButtonElement>('.notice button')!.click();
    expect(store.reload).toHaveBeenCalledTimes(1);
    current.set({ ...person, name: 'Grace Okonkwo-Bell', version: 'version-2' });
    await settle();
    expect(overlay().textContent).toContain('Saved as Grace Okonkwo-Bell');
    // The draft is kept, and nothing was replayed.
    expect(form().name().value()).toBe('Grace A. Okonkwo');
    http.verify();

    await save();
    const second = http.expectOne(`/api/people/${ID}`);
    expect(second.request.body.version).toBe('version-2');
    second.flush(person);
    await settle();
    expect(closedWith).toEqual({ kind: 'renamed' });
  });
});

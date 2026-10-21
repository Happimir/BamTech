import { ApplicationRef, Injector, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { provideRouter } from '@angular/router';
import { ApolloTestingController } from 'apollo-angular/testing';
import { PersonDetail } from '../../../core/data-access/api.models';
import { PeopleApi } from '../../../core/data-access/people-api';
import { CreatePersonDocument, UpdatePersonDocument } from '../../../core/data-access/graphql/generated';
import { personFixture, provideApolloTesting } from '../../../core/data-access/graphql/testing';
import { PersonDetailStore } from '../../person-detail/data-access/person-detail.store';
import { PersonDialog, PersonDialogMode, PersonDialogResult } from './person-dialog';
import { ADMIN_DIALOG_CONFIG } from '../shared/write-outcome';

const ID = '019a2b3c-4d5e-7f60-8a71-b2c3d4e5f607';

const person: PersonDetail = personFixture({
  id: ID,
  name: 'Grace Okonkwo',
  version: 'version-1',
  firstName: 'Grace',
  lastName: 'Okonkwo',
});

const created = (value: PersonDetail) => ({
  createPerson: { __typename: 'CreatePersonPayload', person: value, errors: null },
});

describe('PersonDialog', () => {
  let apollo: ApolloTestingController;
  let ref: MatDialogRef<PersonDialog, PersonDialogResult>;
  let dialog: PersonDialog;
  let closedWith: PersonDialogResult | undefined | 'open';

  // The submission action resolves after the operation, outside Angular's pending tasks: drain a macrotask too.
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
        provideApolloTesting(),
        PeopleApi,
        provideRouter([]),
        { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      ],
    });
    apollo = TestBed.inject(ApolloTestingController);
  });

  afterEach(() => {
    TestBed.inject(MatDialog).closeAll();
    apollo.verify();
  });

  it('creates with the display name as typed, trimmed, and absent parts as null', async () => {
    await open('create');
    await fill({ name: '  Ada  Lovelace ', firstName: ' Ada ', middleName: '   ' });
    await save();

    const request = apollo.expectOne(CreatePersonDocument);
    expect(request.operation.variables['input']).toEqual({
      name: 'Ada  Lovelace',
      firstName: 'Ada',
      middleName: null,
      lastName: null,
    });
    request.flushData(created({ ...person, name: 'Ada  Lovelace' }));
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
    apollo.expectOne(CreatePersonDocument).flushData({
      createPerson: {
        __typename: 'CreatePersonPayload',
        person: null,
        errors: [
          {
            __typename: 'ValidationError',
            code: 'validation',
            message: 'One or more fields are invalid.',
            fields: [{ __typename: 'InvalidField', field: 'lastName', messages: ['Must be at most 100 characters.'] }],
          },
        ],
      },
    });
    await settle();

    expect(form().lastName().errors().map((e) => e.message)).toEqual(['Must be at most 100 characters.']);
    expect(closedWith).toBe('open');
  });

  it('after an unconfirmed create, requires an explicit decision before creating again', async () => {
    await open('create');
    await fill({ name: 'Ada Lovelace' });
    await save();
    apollo.expectOne(CreatePersonDocument).networkError(new Error('Http failure response for /graphql: 0 Unknown Error'));
    await settle();

    expect(notice()).toContain('Creation may have succeeded. Check the directory for "Ada Lovelace"');
    expect(overlay().querySelector('.notice a')?.getAttribute('href')).toBe('/people?name=Ada%20Lovelace');
    expect(saveButton().textContent?.trim()).toBe('Create anyway');
    // Nothing is retried on its own.
    apollo.verify();

    await save();
    apollo.expectOne(CreatePersonDocument).flushData(created(person));
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

    const first = apollo.expectOne(UpdatePersonDocument);
    expect(first.operation.variables['input']).toEqual({
      id: ID,
      name: 'Grace A. Okonkwo',
      firstName: 'Grace',
      middleName: null,
      lastName: 'Okonkwo',
      version: 'version-1',
    });
    first.flushData({
      updatePerson: {
        __typename: 'UpdatePersonPayload',
        person: null,
        errors: [{ __typename: 'ConcurrencyConflictError', code: 'concurrency_conflict', message: 'Changed.' }],
      },
    });
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
    apollo.verify();

    await save();
    const second = apollo.expectOne(UpdatePersonDocument);
    expect(second.operation.variables['input'].version).toBe('version-2');
    second.flushData({ updatePerson: { __typename: 'UpdatePersonPayload', person, errors: null } });
    await settle();
    expect(closedWith).toEqual({ kind: 'renamed' });
  });
});

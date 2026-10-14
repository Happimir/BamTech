import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { form, FormField, FormRoot, maxLength, TreeValidationResult, validate } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { LIMITS, PersonDetail, PersonNameFields } from '../../../core/data-access/api.models';
import { PeopleApi } from '../../../core/data-access/people-api';
import { AuthApi } from '../../../core/data-access/auth-api';
import { PersonDetailStore } from '../../person-detail/data-access/person-detail.store';
import { classifyWriteFailure, DialogNotice, toSubmissionErrors } from '../shared/write-outcome';

export type PersonDialogMode = 'create' | 'rename';

export type PersonDialogResult =
  | { kind: 'created'; person: PersonDetail }
  | { kind: 'renamed' };

interface NameDraft {
  name: string;
  firstName: string;
  middleName: string;
  lastName: string;
}

/**
 * Creates a person, or renames the person in the surrounding PersonDetailStore (opened with the
 * detail page's injector). The display name is required and stored as typed; the optional parts
 * are never split from it, and a name built from the parts is only ever offered, never applied.
 * Namesakes are allowed, so there is no duplicate-name check.
 */
@Component({
  selector: 'acts-person-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormRoot, FormField, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, RouterLink],
  templateUrl: './person-dialog.html',
  styleUrl: '../shared/admin-dialog.scss',
})
export class PersonDialog {
  protected readonly mode = inject<PersonDialogMode>(MAT_DIALOG_DATA);
  /** Present when opened from the detail page (rename); absent on the directory (create). */
  private readonly store = inject(PersonDetailStore, { optional: true });
  private readonly api = inject(PeopleApi);
  private readonly authApi = inject(AuthApi);
  private readonly dialogRef = inject<MatDialogRef<PersonDialog, PersonDialogResult>>(MatDialogRef);

  protected readonly limits = LIMITS;
  /** The person being renamed, read live so Reload shows what is saved now. */
  protected readonly person = computed(() => this.store?.person());
  protected readonly notice = signal<DialogNotice | null>(null);

  private readonly draft = signal<NameDraft>(toDraft(this.person()));

  protected readonly nameForm = form(
    this.draft,
    (path) => {
      validate(path.name, ({ value }) =>
        value().trim() ? undefined : { kind: 'required', message: 'Enter the full name to display.' },
      );
      maxLength(path.name, LIMITS.name, { message: `Use at most ${LIMITS.name} characters.` });
      for (const part of [path.firstName, path.middleName, path.lastName]) {
        maxLength(part, LIMITS.namePart, { message: `Use at most ${LIMITS.namePart} characters.` });
      }
    },
    { submission: { action: () => this.save() } },
  );

  /** The parts in the common given-middle-family order, offered when they differ from the name. */
  protected readonly suggestion = computed(() => {
    const { name, firstName, middleName, lastName } = this.draft();
    const joined = [firstName, middleName, lastName]
      .map((part) => part.trim())
      .filter(Boolean)
      .join(' ');
    return joined && joined !== name.trim() ? joined : null;
  });

  protected readonly title = this.mode === 'create' ? 'Add person' : 'Rename person';

  protected readonly saveLabel = computed(() => {
    if (this.mode === 'rename') return 'Save name';
    return this.notice()?.kind === 'uncertain-create' ? 'Create anyway' : 'Create person';
  });

  /** For "check the directory" after an unconfirmed create: a search by the submitted name. */
  protected readonly directoryQuery = signal<{ name: string } | null>(null);

  protected useSuggestion(name: string): void {
    this.nameForm.name().value.set(name);
  }

  protected reload(): void {
    this.store?.reload();
    this.notice.set({
      kind: 'reloaded',
      message: 'The latest record is loaded. Its saved name is shown above; review your entry and save again.',
    });
  }

  protected close(): void {
    this.dialogRef.close();
  }

  private async save(): Promise<TreeValidationResult> {
    const fields = toFields(this.draft());
    this.notice.set(null);
    this.dialogRef.disableClose = true;
    try {
      const person = this.person();
      if (this.mode === 'rename' && person) {
        await this.api.renamePerson(person.id, fields, person.version);
        this.dialogRef.close({ kind: 'renamed' });
      } else {
        this.dialogRef.close({ kind: 'created', person: await this.api.createPerson(fields) });
      }
      return undefined;
    } catch (error) {
      return this.failed(error, fields.name);
    } finally {
      this.dialogRef.disableClose = false;
    }
  }

  private async failed(error: unknown, submittedName: string): Promise<TreeValidationResult> {
    const failure = classifyWriteFailure(error);
    switch (failure.kind) {
      case 'fields': {
        const { mapped, unmapped } = toSubmissionErrors(failure.errors, {
          name: this.nameForm.name,
          firstName: this.nameForm.firstName,
          middleName: this.nameForm.middleName,
          lastName: this.nameForm.lastName,
        });
        if (unmapped.length) this.notice.set({ kind: 'rejected', message: unmapped.join(' ') });
        return mapped;
      }
      case 'stale':
        this.notice.set({ kind: 'stale', message: `${failure.message} Reload to see the saved name.` });
        return undefined;
      case 'uncertain':
        if (this.mode === 'create') {
          // A namesake can't prove this request created anyone, so only the user can decide.
          this.directoryQuery.set({ name: submittedName });
          this.notice.set({
            kind: 'uncertain-create',
            message: `Creation may have succeeded. Check the directory for "${submittedName}" before creating them again.`,
          });
        } else {
          this.store?.reload();
          this.notice.set({
            kind: 'uncertain',
            message: "The rename couldn't be confirmed. The latest record is reloaded: check the name before saving again.",
          });
        }
        return undefined;
      case 'rejected':
        if (failure.code === 'csrf_failed') await this.authApi.antiforgery().catch(() => undefined);
        this.notice.set({ kind: 'rejected', message: failure.message });
        return undefined;
    }
  }
}

function toDraft(person: PersonDetail | undefined): NameDraft {
  return {
    name: person?.name ?? '',
    firstName: person?.firstName ?? '',
    middleName: person?.middleName ?? '',
    lastName: person?.lastName ?? '',
  };
}

/** Trimmed, with empty parts sent as null (the API stores absent parts as null). */
function toFields(draft: NameDraft): PersonNameFields {
  const part = (value: string) => value.trim() || null;
  return {
    name: draft.name.trim(),
    firstName: part(draft.firstName),
    middleName: part(draft.middleName),
    lastName: part(draft.lastName),
  };
}

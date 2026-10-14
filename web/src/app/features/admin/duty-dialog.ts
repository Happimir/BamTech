import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import {
  form,
  FormField,
  FormRoot,
  hidden,
  maxLength,
  required,
  TreeValidationResult,
  validate,
} from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { LIMITS } from '../../core/api/api.models';
import { PeopleApi } from '../../core/api/people-api';
import { AuthApi } from '../../core/auth/auth-api';
import {
  addCalendarDays,
  formatCalendarDate,
  fromCalendarDate,
  toCalendarDate,
} from '../../core/dates/calendar-date';
import { RETIRED_DUTY_TITLE } from '../people/person-status';
import { PersonDetailStore } from '../person-detail/person-detail.store';
import { classifyWriteFailure, DialogNotice, toSubmissionErrors } from './write-outcome';

export type DutyKind = 'duty' | 'retire';

interface DutyDraft {
  rank: string;
  kind: DutyKind;
  dutyTitle: string;
  /** Local midnight of the picked day, as the datepicker produces it. */
  dutyStartDate: Date | null;
}

/** The snackbar text for a saved duty; the dialog closes with it only after the API confirms. */
export type DutySaved = 'Duty added' | 'Retirement recorded';

/**
 * Adds a duty to the person shown by the surrounding PersonDetailStore (opened with the detail
 * page's injector). The person, version and previews are read live from that store, so Reload
 * after a conflict updates everything the user reviews. End and career dates are never entered:
 * the server derives them, and the previews only explain what it will do.
 */
@Component({
  selector: 'acts-duty-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    FormRoot,
    FormField,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
  ],
  templateUrl: './duty-dialog.html',
  styleUrl: './admin-dialog.scss',
})
export class DutyDialog {
  private readonly store = inject(PersonDetailStore);
  private readonly api = inject(PeopleApi);
  private readonly authApi = inject(AuthApi);
  private readonly dialogRef = inject<MatDialogRef<DutyDialog, DutySaved>>(MatDialogRef);

  protected readonly limits = LIMITS;
  protected readonly person = this.store.person;
  private readonly current = computed(() => this.person()?.astronaut ?? null);

  /** Retirement needs an active career: never as a first duty, never twice in a row. */
  protected readonly canRetire = computed(() => this.store.status() === 'active');

  protected readonly businessDate = signal<string | null>(null);
  protected readonly businessTimeZone = signal('UTC');
  protected readonly metadataFailed = signal(false);
  protected readonly businessDateLabel = computed(() => {
    const today = this.businessDate();
    return today ? formatCalendarDate(today) : null;
  });
  protected readonly maxDate = computed(() => fromCalendarDate(this.businessDate() ?? ''));
  /** A hint only: the server checks chronology against the state at the moment of saving. */
  protected readonly earliestStart = computed(() => {
    const latest = this.store.latestDutyStart();
    return latest ? addCalendarDays(latest, 1) : null;
  });
  protected readonly minDate = computed(() => fromCalendarDate(this.earliestStart() ?? ''));

  protected readonly notice = signal<DialogNotice | null>(null);

  private readonly draft = signal<DutyDraft>({
    rank: this.current()?.currentRank ?? '',
    kind: 'duty',
    dutyTitle: '',
    dutyStartDate: null,
  });

  protected readonly dutyForm = form(
    this.draft,
    (path) => {
      validate(path.rank, ({ value }) =>
        value().trim() ? undefined : { kind: 'required', message: 'Enter a rank.' },
      );
      maxLength(path.rank, LIMITS.rank, { message: `Use at most ${LIMITS.rank} characters.` });

      validate(path.kind, ({ value }) =>
        value() === 'retire' && !this.canRetire()
          ? { kind: 'cannot-retire', message: 'This person cannot retire now. Choose Duty.' }
          : undefined,
      );

      // Retire sends the canonical title itself; a hidden field is not validated.
      hidden(path.dutyTitle, { when: ({ valueOf }) => valueOf(path.kind) === 'retire' });
      validate(path.dutyTitle, ({ value }) => {
        const title = value().trim();
        if (!title) return { kind: 'required', message: 'Enter a duty title.' };
        if (title.toUpperCase() === RETIRED_DUTY_TITLE) {
          return { kind: 'retired-title', message: this.retiredTitleMessage() };
        }
        return undefined;
      });
      maxLength(path.dutyTitle, LIMITS.dutyTitle, { message: `Use at most ${LIMITS.dutyTitle} characters.` });

      required(path.dutyStartDate, { message: 'Enter a start date, such as 3/14/2026.' });
      validate(path.dutyStartDate, ({ value }) => {
        const picked = value();
        if (!picked) return undefined;
        const day = toCalendarDate(picked);
        const latest = this.store.latestDutyStart();
        if (latest && day <= latest) {
          return {
            kind: 'chronology',
            message: `Must be after ${formatCalendarDate(latest)}, when the current duty started.`,
          };
        }
        const today = this.businessDate();
        if (today && day > today) {
          return { kind: 'future', message: `Can't be after today, ${formatCalendarDate(today)} (${this.businessTimeZone()}).` };
        }
        return undefined;
      });
    },
    { submission: { action: () => this.save() } },
  );

  /**
   * Start-date messages to show. The datepicker's own min/max validators also report errors
   * through the forms interop, without messages; ours cover the same limits and explain them.
   * (Unreadable text leaves the value null, so `required` explains that case.)
   */
  protected readonly startDateMessages = computed(() =>
    this.dutyForm
      .dutyStartDate()
      .errors()
      .filter((error) => !error.kind.startsWith('matDatepicker'))
      .map((error) => error.message ?? 'Check this date.'),
  );

  /** What the server will do with this entry, derived from the same snapshot the page shows. */
  protected readonly previews = computed(() => {
    const person = this.person();
    const draft = this.draft();
    const current = this.current();
    const lines: string[] = [];
    if (!person) return lines;
    const start = draft.dutyStartDate ? toCalendarDate(draft.dutyStartDate) : null;
    const dayBefore = start ? formatCalendarDate(addCalendarDays(start, -1)) : 'the day before it starts';
    const latest = this.store.latestDutyStart();
    const inOrder = !start || !latest || start > latest;

    if (current && inOrder) {
      const title = current.status === 'retired' ? 'The retirement' : current.currentDutyTitle;
      lines.push(`${title} ends ${dayBefore}.`);
    }
    if (draft.kind === 'retire') {
      lines.push(`Career ends ${dayBefore}.`);
    } else if (this.store.status() === 'retired') {
      lines.push('Returns them to active duty: the career end clears, and the retirement stays in the history.');
    } else if (!current && start) {
      lines.push(`First service date: ${formatCalendarDate(start)}.`);
    }
    return lines;
  });

  protected readonly saveLabel = computed(() =>
    this.draft().kind === 'retire' ? 'Record retirement' : 'Add duty',
  );

  constructor() {
    this.api
      .metadata()
      .then((metadata) => {
        this.businessDate.set(metadata.businessDate);
        this.businessTimeZone.set(metadata.businessTimeZone);
      })
      // Never fall back to the browser's own date: the server checks the date on save.
      .catch(() => this.metadataFailed.set(true));
  }

  protected reload(): void {
    this.store.reload();
    this.notice.set({
      kind: 'reloaded',
      message: 'The latest record is loaded. Check the preview, then save again.',
    });
  }

  private retiredTitleMessage(): string {
    switch (this.store.status()) {
      case 'retired':
        return 'This person is already retired. Enter an active duty to return them to service.';
      case 'not-assigned':
        return "A first duty can't be a retirement.";
      default:
        return 'To record a retirement, choose Retire.';
    }
  }

  private async save(): Promise<TreeValidationResult> {
    const person = this.person();
    const draft = this.draft();
    if (!person || !draft.dutyStartDate) return undefined;
    this.notice.set(null);
    this.dialogRef.disableClose = true;
    try {
      await this.api.assignDuty(person.id, {
        rank: draft.rank.trim(),
        dutyTitle: draft.kind === 'retire' ? RETIRED_DUTY_TITLE : draft.dutyTitle.trim(),
        dutyStartDate: toCalendarDate(draft.dutyStartDate),
        expectedVersion: person.version,
      });
      this.dialogRef.close(draft.kind === 'retire' ? 'Retirement recorded' : 'Duty added');
      return undefined;
    } catch (error) {
      return this.failed(error);
    } finally {
      this.dialogRef.disableClose = false;
    }
  }

  private async failed(error: unknown): Promise<TreeValidationResult> {
    const failure = classifyWriteFailure(error);
    switch (failure.kind) {
      case 'fields': {
        const { mapped, unmapped } = toSubmissionErrors(failure.errors, {
          rank: this.dutyForm.rank,
          dutyTitle: this.dutyForm.dutyTitle,
          dutyStartDate: this.dutyForm.dutyStartDate,
        });
        if (unmapped.length) this.notice.set({ kind: 'rejected', message: unmapped.join(' ') });
        return mapped;
      }
      case 'stale':
        this.notice.set({ kind: 'stale', message: `${failure.message} Reload to see the latest record.` });
        return undefined;
      case 'uncertain':
        // It may have been saved: show the latest state rather than guessing.
        this.store.reload();
        this.notice.set({
          kind: 'uncertain',
          message:
            "The save couldn't be confirmed. The latest record is reloaded behind this form: check the duty history before saving again.",
        });
        return undefined;
      case 'rejected':
        if (failure.code === 'chronology_conflict') {
          return { kind: 'server', message: failure.message, fieldTree: this.dutyForm.dutyStartDate };
        }
        if (failure.code === 'csrf_failed') await this.authApi.antiforgery().catch(() => undefined);
        this.notice.set({ kind: 'rejected', message: failure.message });
        return undefined;
    }
  }
}

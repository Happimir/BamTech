import { HttpErrorResponse } from '@angular/common/http';
import { MatDialogConfig } from '@angular/material/dialog';
import { ReadonlyFieldTree, ValidationError } from '@angular/forms/signals';
import { fieldErrors, isUncertainOutcome, problemCode, problemDetail } from '../../core/http/http-errors';

/** Shared by the admin dialogs: near full width on a phone, focus on the first field, focus returns. */
export const ADMIN_DIALOG_CONFIG: MatDialogConfig = {
  width: '560px',
  maxWidth: 'calc(100vw - 32px)',
  autoFocus: 'first-tabbable',
  restoreFocus: true,
};

/** A message shown above a dialog's actions, outside the form's own validation state. */
export interface DialogNotice {
  kind: 'stale' | 'reloaded' | 'uncertain' | 'uncertain-create' | 'rejected';
  message: string;
}

/**
 * Server field errors as Signal Forms submission errors on the matching inputs. Messages for
 * fields the dialog does not show are returned separately so they still reach the user.
 */
export function toSubmissionErrors(
  errors: Record<string, string[]>,
  fields: Partial<Record<string, ReadonlyFieldTree<unknown>>>,
): { mapped: ValidationError.WithOptionalFieldTree[]; unmapped: string[] } {
  const mapped: ValidationError.WithOptionalFieldTree[] = [];
  const unmapped: string[] = [];
  for (const [field, messages] of Object.entries(errors)) {
    const fieldTree = fields[field];
    for (const message of messages) {
      if (fieldTree) mapped.push({ kind: 'server', message, fieldTree });
      else unmapped.push(message);
    }
  }
  return { mapped, unmapped };
}

/**
 * A failed write, as the dialogs present it. Field errors go to their inputs; everything else is
 * a notice above the actions. Notices are kept apart from Signal Forms submission errors on
 * purpose: those block the next submit until a value changes, but a stale version or an
 * unknown outcome must allow an explicit resubmission after Reload without editing anything.
 */
export type WriteFailure =
  | { kind: 'fields'; errors: Record<string, string[]> }
  /** Someone changed the person first; the draft stays and the user reloads before saving. */
  | { kind: 'stale'; message: string }
  /** No answer, or a failure that may have come after the commit. */
  | { kind: 'uncertain' }
  /** A definite refusal (a rule conflict, forbidden, unavailable); nothing was written. */
  | { kind: 'rejected'; code: string | undefined; message: string };

export function classifyWriteFailure(error: unknown): WriteFailure {
  if (isUncertainOutcome(error)) return { kind: 'uncertain' };
  const code = problemCode(error);
  if (code === 'concurrency_conflict') {
    return { kind: 'stale', message: 'Someone else changed this person after you opened this form.' };
  }
  const errors = fieldErrors(error);
  if (Object.keys(errors).length) return { kind: 'fields', errors };
  return { kind: 'rejected', code, message: rejectionMessage(error, code) };
}

function rejectionMessage(error: unknown, code: string | undefined): string {
  if (code === 'csrf_failed') return 'Your form session expired. Save again to continue.';
  if (error instanceof HttpErrorResponse) {
    if (error.status === 403) return 'Your account is not allowed to make this change.';
    if (error.status === 503) return 'The ACTS service is busy or unavailable. Nothing was saved; try again.';
  }
  return problemDetail(error) ?? 'The change could not be saved. Nothing was written.';
}

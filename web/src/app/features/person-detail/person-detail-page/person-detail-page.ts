import { LiveAnnouncer } from '@angular/cdk/a11y';
import { Clipboard } from '@angular/cdk/clipboard';
import { Location } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
  effect,
  inject,
  Injector,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '../../../core/auth/auth.store';
import { formatCalendarDate } from '../../../core/utils/calendar-date';
import { DutyDialog, DutySaved } from '../../admin/duty-dialog/duty-dialog';
import { PersonDialog, PersonDialogMode, PersonDialogResult } from '../../admin/person-dialog/person-dialog';
import { ADMIN_DIALOG_CONFIG } from '../../admin/shared/write-outcome';
import { StatusBadge } from '../../../shared/ui/status-badge/status-badge';
import { directoryReturnUrl } from '../utils/directory-return';
import { PersonDetailStore } from '../data-access/person-detail.store';

@Component({
  selector: 'acts-person-detail-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PersonDetailStore],
  imports: [MatButtonModule, MatCardModule, RouterLink, StatusBadge],
  templateUrl: './person-detail-page.html',
  styleUrl: './person-detail-page.scss',
})
export class PersonDetailPage {
  protected readonly store = inject(PersonDetailStore);
  private readonly clipboard = inject(Clipboard);
  private readonly announcer = inject(LiveAnnouncer);

  /** The directory search/page the user came from (navigation state), else the plain directory. */
  protected readonly backLink = inject(Router).parseUrl(directoryReturnUrl(inject(Location).getState()));

  protected readonly idCopied = signal(false);

  // Write actions appear only with the matching capability; a Reader sees none of them.
  private readonly capabilities = inject(AuthStore).capabilities;
  protected readonly canRename = computed(() => this.capabilities().has('People.Write'));
  protected readonly canAddDuty = computed(() => this.capabilities().has('Duties.Write'));

  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly injector = inject(Injector);
  private openDialog: MatDialogRef<unknown> | undefined;

  /** Summary-card facts. Career end appears only while retired (it clears on reactivation). */
  protected readonly summary = computed(() => {
    const astronaut = this.store.person()?.astronaut;
    if (!astronaut) return null;
    const retired = astronaut.status === 'RETIRED';
    return {
      rank: astronaut.currentRank,
      duty: retired ? 'Retired' : astronaut.currentDutyTitle,
      since: formatCalendarDate(astronaut.currentDutyStartDate),
      firstServiceDate: formatCalendarDate(astronaut.careerStartDate),
      careerEnd: astronaut.careerEndDate ? formatCalendarDate(astronaut.careerEndDate) : null,
    };
  });

  /** Only the name parts that were actually supplied; nothing is split from the full name. */
  protected readonly nameParts = computed(() => {
    const person = this.store.person();
    if (!person) return [];
    return [
      { label: 'First name', value: person.firstName },
      { label: 'Middle name', value: person.middleName },
      { label: 'Last name', value: person.lastName },
    ].filter((part): part is { label: string; value: string } => !!part.value);
  });

  constructor() {
    const title = inject(Title);
    effect(() => {
      const person = this.store.person();
      title.setTitle(person ? `${person.name} · ACTS` : 'Person · ACTS');
    });
    // Leaving the page (including on session expiry) never strands a dialog over another page.
    inject(DestroyRef).onDestroy(() => this.openDialog?.close());
  }

  protected rename(): void {
    const ref = this.dialog.open<PersonDialog, PersonDialogMode, PersonDialogResult>(PersonDialog, {
      ...ADMIN_DIALOG_CONFIG,
      data: 'rename',
      injector: this.injector,
    });
    this.afterSave(ref, (result) => (result ? 'Name updated' : undefined));
  }

  protected addDuty(): void {
    const ref = this.dialog.open<DutyDialog, unknown, DutySaved>(DutyDialog, {
      ...ADMIN_DIALOG_CONFIG,
      injector: this.injector,
    });
    this.afterSave(ref, (saved) => saved);
  }

  /** Success only: refresh the record and confirm. Failures stay inline in the dialog. */
  private afterSave<R>(
    ref: MatDialogRef<unknown, R>,
    message: (result: R | undefined) => string | undefined,
  ): void {
    this.openDialog = ref;
    ref.afterClosed().subscribe((result) => {
      this.openDialog = undefined;
      const text = message(result);
      if (!text) return;
      this.store.reload();
      this.snackBar.open(text, undefined, { duration: 4000 });
    });
  }

  protected copyId(id: string): void {
    if (!this.clipboard.copy(id)) {
      void this.announcer.announce('Copying the ID failed. Select it to copy it manually.');
      return;
    }
    this.idCopied.set(true);
    void this.announcer.announce('Full ID copied to the clipboard.');
    setTimeout(() => this.idCopied.set(false), 2000);
  }
}

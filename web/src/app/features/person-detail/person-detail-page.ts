import { LiveAnnouncer } from '@angular/cdk/a11y';
import { Clipboard } from '@angular/cdk/clipboard';
import { Location } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';
import { formatCalendarDate } from '../../core/dates/calendar-date';
import { StatusBadge } from '../people/status-badge';
import { RETIRED_DUTY_TITLE } from '../people/person-status';
import { directoryReturnUrl } from './directory-return';
import { PersonDetailStore } from './person-detail.store';

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

  /** Summary-card facts. Career end appears only while retired (it clears on reactivation). */
  protected readonly summary = computed(() => {
    const astronaut = this.store.person()?.astronaut;
    if (!astronaut) return null;
    const retired = astronaut.currentDutyTitle === RETIRED_DUTY_TITLE;
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

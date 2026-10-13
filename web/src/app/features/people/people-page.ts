import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { PagedResponse, PersonSummary } from '../../core/api/api.models';

/**
 * Slice 5b integration probe: one protected read through the proxy. The full directory
 * (search, server paging, table/cards) replaces this template in the next slice.
 */
@Component({
  selector: 'acts-people-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule],
  template: `
    <h1>People</h1>
    @if (people.isLoading()) {
      <p role="status">Loading people…</p>
    } @else if (people.error()) {
      <p role="alert">People could not be loaded.</p>
      <button mat-stroked-button type="button" (click)="people.reload()">Retry</button>
    } @else if (people.hasValue()) {
      <p role="status">{{ people.value().totalCount }} people</p>
      <ul class="people-list">
        @for (person of people.value().items; track person.id) {
          <li>{{ person.name }}</li>
        }
      </ul>
    }
  `,
})
export class PeoplePage {
  protected readonly people = httpResource<PagedResponse<PersonSummary>>(
    () => '/api/people?page=1&pageSize=25',
  );
}

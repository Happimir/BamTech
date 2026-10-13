import { BreakpointObserver } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { form, FormField, FormRoot, maxLength } from '@angular/forms/signals';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { map } from 'rxjs';
import { MAX_NAME_LENGTH, PAGE_SIZE_OPTIONS } from './people-query';
import { PeopleStore } from './people.store';
import { PersonRow, toPersonRows } from './person-rows';
import { StatusBadge } from './status-badge';

// Below this width the six-column table no longer fits; people are shown as cards instead.
const NARROW_VIEWPORT = '(max-width: 839.98px)';

@Component({
  selector: 'acts-people-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [PeopleStore],
  imports: [
    FormField,
    FormRoot,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressBarModule,
    MatTableModule,
    RouterLink,
    StatusBadge,
  ],
  templateUrl: './people-page.html',
  styleUrl: './people-page.scss',
})
export class PeoplePage {
  protected readonly store = inject(PeopleStore);

  protected readonly pageSizeOptions = PAGE_SIZE_OPTIONS;
  protected readonly columns = ['name', 'status', 'rank', 'duty', 'since', 'view'];
  protected readonly skeletonRows = [1, 2, 3, 4, 5];

  private readonly breakpoints = inject(BreakpointObserver);
  protected readonly isNarrow = toSignal(
    this.breakpoints.observe(NARROW_VIEWPORT).pipe(map((state) => state.matches)),
    { initialValue: this.breakpoints.isMatched(NARROW_VIEWPORT) },
  );

  /** Draft search text. Follows the applied search on navigation; typing alone sends nothing. */
  private readonly draft = linkedSignal(() => ({ name: this.store.query().name }));
  protected readonly searchForm = form(
    this.draft,
    (path) => {
      maxLength(path.name, MAX_NAME_LENGTH, {
        message: `Names are at most ${MAX_NAME_LENGTH} characters.`,
      });
    },
    {
      submission: {
        action: async () => {
          await this.store.search(this.draft().name);
          return undefined;
        },
      },
    },
  );

  protected readonly rows = computed<PersonRow[]>(() => toPersonRows(this.store.result()?.items ?? []));

  /** Polite status text: what the list currently shows. */
  protected readonly summary = computed(() => {
    const view = this.store.view();
    if (view === 'error') return '';
    if (view === 'loading') return 'Loading people…';
    if (this.store.isRefreshing()) return 'Updating results…';
    const result = this.store.result();
    if (!result) return '';
    const total = result.totalCount;
    const noun = total === 1 ? 'person' : 'people';
    const name = this.store.query().name;
    const count = name ? `${total} ${noun} with names starting “${name}”` : `${total} ${noun}`;
    if (result.items.length === 0) return count;
    const first = (result.page - 1) * result.pageSize + 1;
    return `${count} · showing ${first}–${first + result.items.length - 1}`;
  });

  protected clearSearch(): void {
    this.draft.set({ name: '' });
    void this.store.clearSearch();
  }

  protected onPage(event: PageEvent): void {
    void this.store.changePage(event.pageIndex, event.pageSize);
  }
}

import { httpResource } from '@angular/common/http';
import { computed, inject, linkedSignal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { signalStore, withComputed, withMethods, withProps } from '@ngrx/signals';
import { PagedResponse, PersonSummary } from '../../../core/data-access/api.models';
import { isUnavailable } from '../../../core/http/http-errors';
import { parsePeopleQuery, PeopleQuery, toQueryParams } from '../utils/people-query';

export type DirectoryView = 'loading' | 'error' | 'empty' | 'no-matches' | 'past-end' | 'results';

/**
 * Directory state for the /people route; provide it on the page so it lives and dies with the
 * route. The URL query parameters are the only source of the applied search and paging: methods
 * navigate, and the list request follows the URL. Draft search text never reaches the store.
 */
export const PeopleStore = signalStore(
  withProps(() => {
    const route = inject(ActivatedRoute);
    const params = toSignal(route.queryParams, { requireSync: true });
    return {
      _router: inject(Router),
      _route: route,
      query: computed(() => parsePeopleQuery(params())),
    };
  }),
  withProps(({ query }) => {
    const people = httpResource<PagedResponse<PersonSummary>>(() => {
      const { name, page, pageSize } = query();
      return { url: '/api/people', params: { page, pageSize, ...(name ? { name } : {}) } };
    });
    // The last settled page stays on screen (marked stale) while the next one loads. linkedSignal
    // is lazy: "previous" is the value last read, which is the page the template rendered. If it
    // was never rendered, the fallback is a skeleton, never another query's rows.
    const lastResult = linkedSignal<PagedResponse<PersonSummary> | undefined, PagedResponse<PersonSummary> | undefined>({
      source: () => (people.hasValue() ? people.value() : undefined),
      computation: (next, previous) => next ?? (people.error() ? undefined : previous?.value),
    });
    return { _people: people, _lastResult: lastResult };
  }),
  withComputed(({ _people, _lastResult, query }) => ({
    result: computed(() => _lastResult()),
    /** True while a newer request is in flight and the rows shown belong to the previous query. */
    isRefreshing: computed(() => _people.isLoading() && _lastResult() !== undefined),
    isUnavailable: computed(() => isUnavailable(_people.error())),
    view: computed<DirectoryView>(() => {
      if (_people.error()) return 'error';
      const result = _lastResult();
      if (!result) return 'loading';
      if (result.totalCount === 0) return query().name ? 'no-matches' : 'empty';
      if (result.items.length === 0) return 'past-end';
      return 'results';
    }),
  })),
  withMethods((store) => {
    const navigate = (next: PeopleQuery) =>
      store._router.navigate([], { relativeTo: store._route, queryParams: toQueryParams(next) });
    return {
      /** Applies a search; always starts again from the first page. */
      search(name: string): Promise<boolean> {
        return navigate({ ...store.query(), name: name.trim(), page: 1 });
      },
      clearSearch(): Promise<boolean> {
        return navigate({ ...store.query(), name: '', page: 1 });
      },
      /** Paginator event: a new page size starts again from the first page. */
      changePage(pageIndex: number, pageSize: number): Promise<boolean> {
        const sizeChanged = pageSize !== store.query().pageSize;
        return navigate({ ...store.query(), pageSize, page: sizeChanged ? 1 : pageIndex + 1 });
      },
      /** Offered (never automatic) when the current page is past the end after data changed. */
      goToLastPage(): Promise<boolean> {
        const totalPages = store.result()?.totalPages ?? 1;
        return navigate({ ...store.query(), page: Math.max(totalPages, 1) });
      },
      reload(): void {
        store._people.reload();
      },
    };
  }),
);

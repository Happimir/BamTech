import { HttpErrorResponse, httpResource } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { signalStore, withComputed, withMethods, withProps } from '@ngrx/signals';
import { PersonDuties } from '../../core/api/api.models';
import { isUnavailable } from '../../core/http/http-errors';
import { personStatus } from '../people/person-status';
import { buildTimeline } from './timeline';

export type DetailView = 'loading' | 'ready' | 'not-found' | 'forbidden' | 'error';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * State for /people/:id; provide it on the page. The summary and the timeline both come from
 * one /duties response, which the API reads as a single snapshot, so they can never disagree.
 * A malformed ID is a local not-found: no request is made for it.
 */
export const PersonDetailStore = signalStore(
  withProps(() => {
    const params = toSignal(inject(ActivatedRoute).paramMap, { requireSync: true });
    const id = computed(() => params().get('id') ?? '');
    return { id, isValidId: computed(() => UUID.test(id())) };
  }),
  withProps(({ id, isValidId }) => ({
    _details: httpResource<PersonDuties>(() =>
      isValidId() ? `/api/people/${encodeURIComponent(id())}/duties` : undefined,
    ),
  })),
  withComputed(({ _details, isValidId }) => {
    const snapshot = computed(() => (_details.hasValue() ? _details.value() : undefined));
    const person = computed(() => snapshot()?.person);
    return {
      person,
      status: computed(() => personStatus(person()?.astronaut ?? null)),
      timeline: computed(() => buildTimeline(snapshot()?.duties ?? [])),
      isUnavailable: computed(() => isUnavailable(_details.error())),
      view: computed<DetailView>(() => {
        if (!isValidId()) return 'not-found';
        const error = _details.error();
        if (error instanceof HttpErrorResponse && error.status === 404) return 'not-found';
        if (error instanceof HttpErrorResponse && error.status === 403) return 'forbidden';
        if (error) return 'error';
        return snapshot() ? 'ready' : 'loading';
      }),
    };
  }),
  withMethods((store) => ({
    reload(): void {
      store._details.reload();
    },
  })),
);

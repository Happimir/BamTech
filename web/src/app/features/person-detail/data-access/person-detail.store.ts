import { computed, inject } from '@angular/core';
import { rxResource, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute } from '@angular/router';
import { signalStore, withComputed, withMethods, withProps } from '@ngrx/signals';
import { Apollo } from 'apollo-angular';
import { map } from 'rxjs';
import { PersonDuties } from '../../../core/data-access/api.models';
import { PersonDetailDocument } from '../../../core/data-access/graphql/generated';
import { errorCode, isUnavailable } from '../../../core/http/api-errors';
import { personStatus } from '../../../shared/utils/person-status';
import { buildTimeline } from '../utils/timeline';

export type DetailView = 'loading' | 'ready' | 'not-found' | 'forbidden' | 'error';

const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

/**
 * State for /people/:id; provide it on the page. The summary and the timeline both come from
 * one query that selects the person with their duties, which the API reads as a single
 * snapshot, so they can never disagree. A malformed ID is a local not-found: no request is
 * made for it. An unknown ID is a null person.
 */
export const PersonDetailStore = signalStore(
  withProps(() => {
    const params = toSignal(inject(ActivatedRoute).paramMap, { requireSync: true });
    const id = computed(() => params().get('id') ?? '');
    return { id, isValidId: computed(() => UUID.test(id())) };
  }),
  withProps(({ id, isValidId }) => {
    const apollo = inject(Apollo);
    return {
      _details: rxResource<PersonDuties | null, string | undefined>({
        params: () => (isValidId() ? id() : undefined),
        stream: ({ params: personId }) =>
          apollo
            .query({
              query: PersonDetailDocument,
              variables: { id: personId },
              fetchPolicy: 'network-only',
            })
            .pipe(
              map((result) => {
                const person = result.data?.person;
                if (!person) return null;
                const { duties, ...detail } = person;
                return { person: detail, duties };
              }),
            ),
      }),
    };
  }),
  withComputed(({ _details, isValidId }) => {
    const snapshot = computed(() => (_details.hasValue() ? _details.value() : undefined));
    const person = computed(() => snapshot()?.person);
    return {
      person,
      status: computed(() => personStatus(person()?.astronaut ?? null)),
      timeline: computed(() => buildTimeline(snapshot()?.duties ?? [])),
      /** Start of the newest duty (duties are newest first); a new duty must start after it. */
      latestDutyStart: computed(() => snapshot()?.duties[0]?.dutyStartDate ?? null),
      isUnavailable: computed(() => isUnavailable(_details.error())),
      view: computed<DetailView>(() => {
        if (!isValidId()) return 'not-found';
        const error = _details.error();
        if (error && errorCode(error) === 'forbidden') return 'forbidden';
        if (error) return 'error';
        const value = snapshot();
        if (value === null) return 'not-found';
        return value ? 'ready' : 'loading';
      }),
    };
  }),
  withMethods((store) => ({
    reload(): void {
      store._details.reload();
    },
  })),
);

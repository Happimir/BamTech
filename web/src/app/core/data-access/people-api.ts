import { inject, Injectable } from '@angular/core';
import { Apollo } from 'apollo-angular';
import { firstValueFrom, map, Observable, timeout } from 'rxjs';
import { UserError } from '../http/api-errors';
import {
  AssignDutyRequest,
  AssignDutyResult,
  BusinessMetadata,
  PersonDetail,
  PersonNameFields,
} from './api.models';
import {
  AssignDutyDocument,
  BusinessCalendarDocument,
  CreatePersonDocument,
  UpdatePersonDocument,
  type UserErrorFieldsFragment,
} from './graphql/generated';

/**
 * A write that has not answered by now is reported as an unknown outcome, so the user can
 * check what happened. Writes are never retried automatically.
 */
const WRITE_TIMEOUT_MS = 15_000;

/**
 * Stateless transport for the people writes. Each resolves with the payload's result, or
 * throws: a UserError for a business refusal returned as data, or the transport's error.
 * Reads go through the feature stores. Provided with the GraphQL client on the signed-in routes
 * (provideActsGraphQL), so dialogs that use it are opened with their page's injector.
 */
@Injectable()
export class PeopleApi {
  private readonly apollo = inject(Apollo);

  createPerson(fields: PersonNameFields): Promise<PersonDetail> {
    return this.write(
      this.apollo.mutate({ mutation: CreatePersonDocument, variables: { input: fields } }),
      (data) => data?.createPerson,
    ).then((payload) => required(payload.person));
  }

  renamePerson(id: string, fields: PersonNameFields, version: string): Promise<PersonDetail> {
    return this.write(
      this.apollo.mutate({
        mutation: UpdatePersonDocument,
        variables: { input: { id, ...fields, version } },
      }),
      (data) => data?.updatePerson,
    ).then((payload) => required(payload.person));
  }

  assignDuty(personId: string, request: AssignDutyRequest): Promise<AssignDutyResult> {
    return this.write(
      this.apollo.mutate({
        mutation: AssignDutyDocument,
        variables: { input: { person: { id: personId }, ...request } },
      }),
      (data) => data?.assignDuty,
    ).then((payload) => ({ duty: required(payload.duty), person: required(payload.person) }));
  }

  metadata(): Promise<BusinessMetadata> {
    return firstValueFrom(
      this.apollo
        .query({ query: BusinessCalendarDocument })
        .pipe(map((result) => required(result.data?.businessCalendar))),
    );
  }

  /** Waits for a mutation's payload and turns its first business error into a UserError. */
  private write<TData, TPayload extends { errors: UserErrorFieldsFragment[] | null }>(
    mutation: Observable<{ data?: TData | null }>,
    payload: (data: TData | null | undefined) => TPayload | undefined,
  ): Promise<TPayload> {
    return firstValueFrom(
      mutation.pipe(
        timeout(WRITE_TIMEOUT_MS),
        map((result) => {
          const value = required(payload(result.data));
          const [error] = value.errors ?? [];
          if (error) throw toUserError(error);
          return value;
        }),
      ),
    );
  }
}

function toUserError(error: UserErrorFieldsFragment): UserError {
  const fields =
    error.__typename === 'ValidationError'
      ? Object.fromEntries(error.fields.map((field) => [field.field, field.messages]))
      : {};
  return new UserError(error.__typename, error.code, error.message, fields);
}

/** A successful payload always carries its result; anything else is a broken response. */
function required<T>(value: T | null | undefined): T {
  if (value === null || value === undefined) throw new Error('The API response was incomplete.');
  return value;
}

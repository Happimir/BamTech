import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  AssignDutyRequest,
  AssignDutyResult,
  BusinessMetadata,
  PersonDetail,
  PersonNameFields,
} from './api.models';

/**
 * A write that has not answered by now is reported as an unknown outcome (status 0), so the
 * user can check what happened. Writes are never retried automatically.
 */
const WRITE_TIMEOUT_MS = 15_000;

/** Stateless transport for the people write endpoints. Reads go through the feature stores. */
@Injectable({ providedIn: 'root' })
export class PeopleApi {
  private readonly http = inject(HttpClient);

  createPerson(fields: PersonNameFields): Promise<PersonDetail> {
    return firstValueFrom(this.http.post<PersonDetail>('/api/people', fields, { timeout: WRITE_TIMEOUT_MS }));
  }

  renamePerson(id: string, fields: PersonNameFields, version: string): Promise<PersonDetail> {
    return firstValueFrom(
      this.http.put<PersonDetail>(
        `/api/people/${encodeURIComponent(id)}`,
        { ...fields, version },
        { timeout: WRITE_TIMEOUT_MS },
      ),
    );
  }

  assignDuty(personId: string, request: AssignDutyRequest): Promise<AssignDutyResult> {
    return firstValueFrom(
      this.http.post<AssignDutyResult>(`/api/people/${encodeURIComponent(personId)}/duties`, request, {
        timeout: WRITE_TIMEOUT_MS,
      }),
    );
  }

  metadata(): Promise<BusinessMetadata> {
    return firstValueFrom(this.http.get<BusinessMetadata>('/api/metadata'));
  }
}

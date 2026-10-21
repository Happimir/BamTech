// The app's view of the ACTS API (see the repository README). GraphQL shapes come from the
// code generated from the API's schema (graphql/generated.ts), named here for the features.
// IDs are opaque UUID strings and dates are YYYY-MM-DD calendar-date strings.

import type {
  CareerStatus as ApiCareerStatus,
  DutyFieldsFragment,
  PeopleQuery,
  PersonFieldsFragment,
} from './graphql/generated';

export type Capability = 'People.Read' | 'People.Write' | 'Duties.Write';

/** GET /auth/me: the signed-in account. The sign-in routes are plain HTTP, not GraphQL. */
export interface AuthMe {
  id: string;
  userName: string;
  roles: string[];
  capabilities: Capability[];
}

/** Decided by the API (rule 6); never re-derived from duty titles in the client. */
export type CareerStatus = ApiCareerStatus;

/** A person as every read returns them: summary plus the supplied name parts. */
export type PersonDetail = PersonFieldsFragment;

/** The current assignment; null on a person with no assignments. */
export type AstronautSummary = NonNullable<PersonDetail['astronaut']>;

export type Duty = DutyFieldsFragment;

/** One directory page. */
export type PeoplePage = PeopleQuery['people'];

/** A person and their duty history, newest first, read as one consistent snapshot. */
export interface PersonDuties {
  person: PersonDetail;
  duties: Duty[];
}

/** Server "today" for date rules, and its zone (UTC in the demo). */
export interface BusinessMetadata {
  businessDate: string;
  businessTimeZone: string;
}

// Write inputs. Name parts are sent as entered, or null; the server never splits names.

export interface PersonNameFields {
  name: string;
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
}

export interface AssignDutyRequest {
  rank: string;
  dutyTitle: string;
  dutyStartDate: string;
  expectedVersion: string;
}

/** The new duty and the person after the change, including their new version. */
export interface AssignDutyResult {
  duty: Duty;
  person: PersonDetail;
}

/** API input limits, kept in step with the server's validation. */
export const LIMITS = { name: 300, namePart: 100, rank: 100, dutyTitle: 200 } as const;

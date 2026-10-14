// Response shapes of the ACTS API (see the repository README). IDs are opaque
// UUID strings and dates are YYYY-MM-DD calendar-date strings.

export type Capability = 'People.Read' | 'People.Write' | 'Duties.Write';

export interface AuthMe {
  id: string;
  userName: string;
  roles: string[];
  capabilities: Capability[];
}

/** Decided by the API (rule 6); never re-derived from duty titles in the client. */
export type CareerStatus = 'active' | 'retired';

export interface AstronautSummary {
  currentRank: string;
  currentDutyTitle: string;
  currentDutyStartDate: string;
  careerStartDate: string;
  careerEndDate: string | null;
  status: CareerStatus;
}

export interface PersonSummary {
  id: string;
  name: string;
  version: string;
  astronaut: AstronautSummary | null;
}

export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PersonDetail extends PersonSummary {
  firstName: string | null;
  middleName: string | null;
  lastName: string | null;
}

export interface Duty {
  id: string;
  rank: string;
  dutyTitle: string;
  dutyStartDate: string;
  dutyEndDate: string | null;
  /** True for a retirement duty, as decided by the API. */
  isRetirement: boolean;
}

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

// Request bodies. Name parts are sent as entered, or null; the server never splits names.

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

export interface AssignDutyResult {
  id: string;
  personId: string;
  version: string;
}

/** API input limits, kept in step with the server's validation. */
export const LIMITS = { name: 300, namePart: 100, rank: 100, dutyTitle: 200 } as const;

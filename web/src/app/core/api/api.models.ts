// Response shapes of the ACTS API (see the repository README). IDs are opaque
// UUID strings and dates are YYYY-MM-DD calendar-date strings.

export type Capability = 'People.Read' | 'People.Write' | 'Duties.Write';

export interface AuthMe {
  id: string;
  userName: string;
  roles: string[];
  capabilities: Capability[];
}

export interface AstronautSummary {
  currentRank: string;
  currentDutyTitle: string;
  currentDutyStartDate: string;
  careerStartDate: string;
  careerEndDate: string | null;
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

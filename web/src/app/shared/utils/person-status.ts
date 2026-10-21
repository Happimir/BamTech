import { AstronautSummary } from '../../core/data-access/api.models';

export type PersonStatus = 'not-assigned' | 'active' | 'retired';

/**
 * The title a retirement request sends (the API also accepts any case). Only for building
 * requests and steering input: whether someone is retired comes from the API's `status`.
 */
export const RETIRED_DUTY_TITLE = 'RETIRED';

/** No astronaut summary means no assignments (rule 2); otherwise the API's own status. */
export function personStatus(astronaut: AstronautSummary | null): PersonStatus {
  if (!astronaut) return 'not-assigned';
  return astronaut.status === 'RETIRED' ? 'retired' : 'active';
}

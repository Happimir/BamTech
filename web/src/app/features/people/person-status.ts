import { AstronautSummary } from '../../core/api/api.models';

export type PersonStatus = 'not-assigned' | 'active' | 'retired';

/** The API canonicalizes the retirement duty title to exactly this value. */
export const RETIRED_DUTY_TITLE = 'RETIRED';

export function personStatus(astronaut: AstronautSummary | null): PersonStatus {
  if (!astronaut) return 'not-assigned';
  return astronaut.currentDutyTitle === RETIRED_DUTY_TITLE ? 'retired' : 'active';
}

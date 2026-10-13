import { PersonSummary } from '../../core/api/api.models';
import { formatCalendarDate } from '../../core/dates/calendar-date';
import { personStatus, PersonStatus } from './person-status';

/** One directory row, shaped for display. */
export interface PersonRow {
  id: string;
  name: string;
  /** Short ID shown only when the same name appears more than once on the page. */
  namesakeHint: string | null;
  status: PersonStatus;
  rank: string | null;
  dutyTitle: string | null;
  since: string | null;
}

/**
 * A short visual aid that tells namesakes apart; never a lookup key. UUIDv7 IDs start with a
 * timestamp, so people created together share their leading characters. The trailing
 * characters are random.
 */
export function namesakeHint(id: string): string {
  return id.slice(-8);
}

export function toPersonRows(people: readonly PersonSummary[]): PersonRow[] {
  const nameCounts = new Map<string, number>();
  for (const person of people) nameCounts.set(person.name, (nameCounts.get(person.name) ?? 0) + 1);

  return people.map((person) => {
    const astronaut = person.astronaut;
    return {
      id: person.id,
      name: person.name,
      namesakeHint: (nameCounts.get(person.name) ?? 0) > 1 ? namesakeHint(person.id) : null,
      status: personStatus(astronaut),
      rank: astronaut?.currentRank ?? null,
      dutyTitle: astronaut?.currentDutyTitle ?? null,
      since: astronaut ? formatCalendarDate(astronaut.currentDutyStartDate) : null,
    };
  });
}

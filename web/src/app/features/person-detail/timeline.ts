import { Duty } from '../../core/api/api.models';
import { formatCalendarDate } from '../../core/dates/calendar-date';
import { RETIRED_DUTY_TITLE } from '../people/person-status';

export interface TimelineDate {
  /** YYYY-MM-DD for the `<time datetime>` attribute. */
  iso: string;
  label: string;
}

export interface TimelineItem {
  id: string;
  /** Display title; the canonical RETIRED title reads as "Retired". */
  title: string;
  rank: string;
  isRetirement: boolean;
  /** The open duty, i.e. the person's current assignment or retirement. */
  isCurrent: boolean;
  start: TimelineDate;
  /** Null while the duty is open ("Current"). */
  end: TimelineDate | null;
  /** First active duty after a retirement, derived from the history itself. */
  returnedToActiveDuty: boolean;
}

const date = (iso: string): TimelineDate => ({ iso, label: formatCalendarDate(iso) });

/**
 * Shapes a duty history for the timeline. Input and output are newest first, as the API
 * serves them; nothing is re-sorted or fetched separately, so the timeline always matches the
 * summary from the same snapshot.
 */
export function buildTimeline(dutiesNewestFirst: readonly Duty[]): TimelineItem[] {
  return dutiesNewestFirst.map((duty, index) => {
    const isRetirement = duty.dutyTitle === RETIRED_DUTY_TITLE;
    const previous = dutiesNewestFirst[index + 1];
    return {
      id: duty.id,
      title: isRetirement ? 'Retired' : duty.dutyTitle,
      rank: duty.rank,
      isRetirement,
      isCurrent: duty.dutyEndDate === null,
      start: date(duty.dutyStartDate),
      end: duty.dutyEndDate === null ? null : date(duty.dutyEndDate),
      returnedToActiveDuty: !isRetirement && previous?.dutyTitle === RETIRED_DUTY_TITLE,
    };
  });
}

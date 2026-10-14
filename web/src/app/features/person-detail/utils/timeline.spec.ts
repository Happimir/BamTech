import { Duty } from '../../../core/data-access/api.models';
import { buildTimeline } from './timeline';

// Flags retirement the way the API does for these fixtures (canonical RETIRED title).
const duty = (id: string, dutyTitle: string, dutyStartDate: string, dutyEndDate: string | null): Duty => ({
  id,
  rank: 'Captain',
  dutyTitle,
  dutyStartDate,
  dutyEndDate,
  isRetirement: dutyTitle === 'RETIRED',
});

describe('buildTimeline', () => {
  // Newest first, as served: active → retired → reactivated.
  const reactivated = [
    duty('d4', 'Flight Director', '2024-02-12', null),
    duty('d3', 'RETIRED', '2020-09-01', '2024-02-11'),
    duty('d2', 'Mission Commander', '2015-06-01', '2020-08-31'),
    duty('d1', 'Pilot', '2010-01-04', '2015-05-31'),
  ];

  it('keeps the served newest-first order and marks only the open duty as current', () => {
    const items = buildTimeline(reactivated);
    expect(items.map((i) => i.id)).toEqual(['d4', 'd3', 'd2', 'd1']);
    expect(items.map((i) => i.isCurrent)).toEqual([true, false, false, false]);
    expect(items[0].end).toBeNull();
    expect(items[1].end).toEqual({ iso: '2024-02-11', label: 'Feb 11, 2024' });
  });

  it('keeps a past retirement in history and annotates only the return after it', () => {
    const items = buildTimeline(reactivated);
    expect(items[1]).toMatchObject({ title: 'Retired', isRetirement: true, returnedToActiveDuty: false });
    expect(items.map((i) => i.returnedToActiveDuty)).toEqual([true, false, false, false]);
  });

  it('labels a current retirement as retired and current', () => {
    const [current] = buildTimeline([
      duty('d2', 'RETIRED', '2023-01-01', null),
      duty('d1', 'Pilot', '2012-03-01', '2022-12-31'),
    ]);
    expect(current).toMatchObject({ title: 'Retired', isRetirement: true, isCurrent: true, returnedToActiveDuty: false });
  });

  it("follows the API's retirement flag, never the title", () => {
    const [current, previous] = buildTimeline([
      duty('d2', 'Pilot', '2024-01-01', null),
      { ...duty('d1', 'Retired', '2020-01-01', '2023-12-31'), isRetirement: true },
    ]);
    expect(previous).toMatchObject({ title: 'Retired', isRetirement: true });
    expect(current.returnedToActiveDuty).toBe(true);
  });

  it('returns an empty timeline for a person with no duties', () => {
    expect(buildTimeline([])).toEqual([]);
  });
});

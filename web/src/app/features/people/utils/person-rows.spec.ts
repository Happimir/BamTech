import { PersonSummary } from '../../../core/data-access/api.models';
import { toPersonRows } from './person-rows';

function person(id: string, name: string, astronaut: PersonSummary['astronaut'] = null): PersonSummary {
  return { id, name, version: 'v', astronaut };
}

describe('toPersonRows', () => {
  it('gives namesakes distinct hints even when their UUIDv7 IDs share a timestamp prefix', () => {
    // Real shape: same millisecond, so the first 8+ characters are identical.
    const rows = toPersonRows([
      person('01a13662-6b4e-7c21-9f3a-1d2e3f4a5b6c', 'Alex Morgan'),
      person('01a13662-6b4e-7d88-a0b1-c2d3e4f5a6b7', 'Alex Morgan'),
      person('01a13662-6b4f-7aaa-8bbb-cccdddeeefff', 'Amara Diallo'),
    ]);

    const [first, second, unique] = rows;
    expect(first.namesakeHint).not.toBeNull();
    expect(first.namesakeHint).not.toBe(second.namesakeHint);
    expect(unique.namesakeHint).toBeNull();
  });

  it("shows the API's status, with no fabricated values for unassigned people", () => {
    const [retired, active, unassigned, flaggedOnly] = toPersonRows([
      person('a', 'R', {
        currentRank: 'Major',
        currentDutyTitle: 'RETIRED',
        currentDutyStartDate: '2019-11-30',
        careerStartDate: '2008-05-19',
        careerEndDate: '2019-11-29',
        status: 'retired',
      }),
      person('b', 'A', {
        currentRank: 'Captain',
        currentDutyTitle: 'Flight Director',
        currentDutyStartDate: '2024-02-12',
        careerStartDate: '2010-01-04',
        careerEndDate: null,
        status: 'active',
      }),
      person('c', 'U'),
      // The API decides retirement (rule 6); the client never inspects the title for it.
      person('d', 'F', {
        currentRank: 'Major',
        currentDutyTitle: 'Retired',
        currentDutyStartDate: '2019-11-30',
        careerStartDate: '2008-05-19',
        careerEndDate: '2019-11-29',
        status: 'retired',
      }),
    ]);

    expect(retired.status).toBe('retired');
    expect(active).toMatchObject({ status: 'active', rank: 'Captain', since: 'Feb 12, 2024' });
    expect(unassigned).toMatchObject({ status: 'not-assigned', rank: null, dutyTitle: null, since: null });
    expect(flaggedOnly.status).toBe('retired');
  });
});

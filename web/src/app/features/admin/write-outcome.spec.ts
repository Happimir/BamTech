import { HttpErrorResponse } from '@angular/common/http';
import { classifyWriteFailure } from './write-outcome';

const response = (status: number, error: object | null = null) => new HttpErrorResponse({ status, error });

describe('classifyWriteFailure', () => {
  it.each([0, 500, 502, 504])('treats status %i as an unknown outcome: the write may have committed', (status) => {
    expect(classifyWriteFailure(response(status)).kind).toBe('uncertain');
  });

  it('treats the API 503 as a definite refusal: it answers before changing anything', () => {
    expect(classifyWriteFailure(response(503, { code: 'service_unavailable' }))).toEqual({
      kind: 'rejected',
      code: 'service_unavailable',
      message: 'The ACTS service is busy or unavailable. Nothing was saved; try again.',
    });
  });

  it('recognises a stale version by its code, not its message', () => {
    expect(classifyWriteFailure(response(409, { code: 'concurrency_conflict', detail: 'anything' })).kind).toBe('stale');
  });

  it('keeps 400 field messages by field, and shows other refusals in the server’s words', () => {
    expect(classifyWriteFailure(response(400, { code: 'validation', errors: { rank: ['Rank is required.'] } }))).toEqual({
      kind: 'fields',
      errors: { rank: ['Rank is required.'] },
    });
    expect(
      classifyWriteFailure(response(409, { code: 'already_retired', detail: 'This person is already retired.' })),
    ).toEqual({ kind: 'rejected', code: 'already_retired', message: 'This person is already retired.' });
  });
});

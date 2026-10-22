import { HttpErrorResponse } from '@angular/common/http';
import { CombinedGraphQLErrors, ServerError } from '@apollo/client/errors';
import { TimeoutError } from 'rxjs';
import { UserError } from '../../../core/http/api-errors';
import { classifyWriteFailure } from './write-outcome';

const graphqlError = (code: string) =>
  new CombinedGraphQLErrors({ errors: [{ message: code, extensions: { code } }] });

const serverError = (status: number, body: object) =>
  new ServerError(`Response not successful: Received status code ${status}`, {
    response: new Response(null, { status }),
    bodyText: JSON.stringify(body),
  });

describe('classifyWriteFailure', () => {
  it.each([
    ['no response', new Error('Http failure response for /graphql: 0 Unknown Error')],
    ['a timeout', new TimeoutError()],
    ['a server fault', graphqlError('internal_error')],
    ['an HTTP 502', serverError(502, {})],
  ])('treats %s as an unknown outcome: the write may have committed', (_, error) => {
    expect(classifyWriteFailure(error).kind).toBe('uncertain');
  });

  it('treats a busy database as a definite refusal: it answers before changing anything', () => {
    expect(classifyWriteFailure(graphqlError('service_unavailable'))).toEqual({
      kind: 'rejected',
      code: 'service_unavailable',
      message: 'The ACTS service is busy or unavailable. Nothing was saved; try again.',
    });
  });

  it('recognises a stale version by its code, not its message', () => {
    expect(classifyWriteFailure(new UserError('ConcurrencyConflictError', 'concurrency_conflict', 'anything')).kind).toBe(
      'stale',
    );
  });

  it('keeps field messages by field, and shows other refusals in the server’s words', () => {
    expect(
      classifyWriteFailure(
        new UserError('ValidationError', 'validation', 'One or more fields are invalid.', { rank: ['Rank is required.'] }),
      ),
    ).toEqual({ kind: 'fields', errors: { rank: ['Rank is required.'] } });
    expect(
      classifyWriteFailure(new UserError('AlreadyRetiredError', 'already_retired', 'This person is already retired.')),
    ).toEqual({ kind: 'rejected', code: 'already_retired', message: 'This person is already retired.' });
  });

  it('explains a denied field and an expired form session', () => {
    expect(classifyWriteFailure(graphqlError('forbidden'))).toMatchObject({
      kind: 'rejected',
      message: 'Your account is not allowed to make this change.',
    });
    expect(classifyWriteFailure(serverError(400, { code: 'csrf_failed' }))).toMatchObject({
      kind: 'rejected',
      code: 'csrf_failed',
      message: 'Your form session expired. Save again to continue.',
    });
  });

  it('reads codes from the sign-in routes’ ProblemDetails too', () => {
    const problem = new HttpErrorResponse({ status: 503, error: { code: 'service_unavailable' } });
    expect(classifyWriteFailure(problem)).toMatchObject({ kind: 'rejected', code: 'service_unavailable' });
  });
});

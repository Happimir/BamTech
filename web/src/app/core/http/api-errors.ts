import { HttpErrorResponse } from '@angular/common/http';
import { CombinedGraphQLErrors, ServerError } from '@apollo/client/errors';
import { TimeoutError } from 'rxjs';

/**
 * A business error the API returned as data: a mutation payload's `errors` entry, such as a
 * ChronologyConflictError. Nothing was written. Thrown by the transport so callers handle every
 * failed write in one place.
 */
export class UserError extends Error {
  constructor(
    /** The GraphQL type, such as ChronologyConflictError. */
    readonly typename: string,
    /** The stable code, such as chronology_conflict. Branch on this, never on the message. */
    readonly code: string,
    message: string,
    /** For a ValidationError: messages by input field name. */
    readonly fields: Record<string, string[]> = {},
  ) {
    super(message);
    this.name = 'UserError';
  }
}

/**
 * The failures the client sees, by shape:
 * - UserError: a business refusal returned as data (see above);
 * - CombinedGraphQLErrors: GraphQL errors in a 200 response (a denied field, a server fault,
 *   invalid paging), each with `extensions.code`;
 * - ServerError: an HTTP status of 300 or more on /graphql (401, csrf_failed, an invalid document),
 *   with the body as text;
 * - HttpErrorResponse: the /auth routes, which use HttpClient directly (ProblemDetails bodies);
 * - TimeoutError or a plain Error: no answer (the link reports a lost connection without a status).
 */

/** The stable code, if the error carries one. Never branch on message text. */
export function errorCode(error: unknown): string | undefined {
  if (error instanceof UserError) return error.code;
  if (CombinedGraphQLErrors.is(error)) return stringCode(error.errors[0]?.extensions?.['code']);
  if (ServerError.is(error)) return codeFromBody(parseBody(error.bodyText));
  if (error instanceof HttpErrorResponse) return codeFromBody(error.error);
  return undefined;
}

/** The HTTP status, when the server answered with one other than 2xx. */
export function httpStatus(error: unknown): number | undefined {
  if (ServerError.is(error)) return error.statusCode;
  if (error instanceof HttpErrorResponse) return error.status;
  return undefined;
}

/** No response at all: a lost connection or a timeout. */
export function isNoResponse(error: unknown): boolean {
  if (error instanceof TimeoutError) return true;
  if (error instanceof HttpErrorResponse) return error.status === 0;
  return (
    error instanceof Error &&
    !(error instanceof UserError) &&
    !CombinedGraphQLErrors.is(error) &&
    !ServerError.is(error)
  );
}

/** No response, or a server-side failure: the service is unreachable or broken, not the request. */
export function isUnavailable(error: unknown): boolean {
  const status = httpStatus(error);
  const code = errorCode(error);
  return (
    isNoResponse(error) ||
    (status !== undefined && status >= 500) ||
    code === 'internal_error' ||
    code === 'service_unavailable'
  );
}

/**
 * A write whose outcome is unknown: no response (network loss or timeout), or a server failure
 * that may have come after the commit. `service_unavailable` (the database was busy) is a
 * refusal before any change, so it is a definite failure.
 */
export function isUncertainOutcome(error: unknown): boolean {
  const status = httpStatus(error);
  return (
    isNoResponse(error) ||
    (status !== undefined && status >= 500 && status !== 503) ||
    errorCode(error) === 'internal_error'
  );
}

/** A ValidationError's per-field messages, keyed by the API's camelCase input field names. */
export function fieldErrors(error: unknown): Record<string, string[]> {
  return error instanceof UserError ? error.fields : {};
}

/** The server's explanation of a business refusal, shown as written; never branched on. */
export function errorMessage(error: unknown): string | undefined {
  return error instanceof UserError ? error.message : undefined;
}

function parseBody(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

/** ProblemDetails `code`, or the first GraphQL error's `extensions.code`. */
function codeFromBody(body: unknown): string | undefined {
  if (!body || typeof body !== 'object') return undefined;
  if ('code' in body) return stringCode(body.code);
  if ('errors' in body && Array.isArray(body.errors)) {
    const first: unknown = body.errors[0];
    if (first && typeof first === 'object' && 'extensions' in first) {
      const extensions = first.extensions;
      if (extensions && typeof extensions === 'object' && 'code' in extensions) {
        return stringCode(extensions.code);
      }
    }
  }
  return undefined;
}

function stringCode(value: unknown): string | undefined {
  return typeof value === 'string' ? value : undefined;
}

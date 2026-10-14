import { HttpErrorResponse } from '@angular/common/http';

/** The stable ProblemDetails `code`, if the error carries one. Never branch on message text. */
export function problemCode(error: unknown): string | undefined {
  if (!(error instanceof HttpErrorResponse)) return undefined;
  const body: unknown = error.error;
  if (body && typeof body === 'object' && 'code' in body && typeof body.code === 'string') {
    return body.code;
  }
  return undefined;
}

/** No response, or a server-side failure: the service is unreachable or broken, not the request. */
export function isUnavailable(error: unknown): boolean {
  return error instanceof HttpErrorResponse && (error.status === 0 || error.status >= 500);
}

/**
 * A write whose outcome is unknown: no response (network loss or timeout), or a server failure
 * that may have come after the commit. The API's 503 is a refusal before any change (database
 * busy), so it is a definite failure.
 */
export function isUncertainOutcome(error: unknown): boolean {
  return (
    error instanceof HttpErrorResponse &&
    (error.status === 0 || (error.status >= 500 && error.status !== 503))
  );
}

/** A 400's per-field messages, keyed by the API's camelCase field names. */
export function fieldErrors(error: unknown): Record<string, string[]> {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) return {};
  const body: unknown = error.error;
  if (!body || typeof body !== 'object' || !('errors' in body)) return {};
  const errors = body.errors;
  if (!errors || typeof errors !== 'object') return {};
  const result: Record<string, string[]> = {};
  for (const [field, messages] of Object.entries(errors)) {
    if (Array.isArray(messages)) {
      result[field] = messages.filter((m): m is string => typeof m === 'string');
    }
  }
  return result;
}

/** The server's explanation (ProblemDetails `detail`), shown as written; never branched on. */
export function problemDetail(error: unknown): string | undefined {
  if (!(error instanceof HttpErrorResponse)) return undefined;
  const body: unknown = error.error;
  if (body && typeof body === 'object' && 'detail' in body && typeof body.detail === 'string') {
    return body.detail;
  }
  return undefined;
}

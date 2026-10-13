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

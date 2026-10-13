import { safeReturnUrl } from '../../core/navigation/return-url';

export const DIRECTORY_URL = '/people';

/** Navigation state the directory attaches to links into a person's detail page. */
export interface DirectoryReturnState {
  returnTo: string;
}

/**
 * Where "Back to people" goes: the directory URL (with its search and page) the user came
 * from, carried in navigation state. Only a local directory URL is accepted; a deep link,
 * reload without state or anything else returns to the plain directory.
 */
export function directoryReturnUrl(state: unknown): string {
  const returnTo =
    state && typeof state === 'object' && 'returnTo' in state ? state.returnTo : undefined;
  if (typeof returnTo !== 'string') return DIRECTORY_URL;
  const safe = safeReturnUrl(returnTo);
  const path = safe.split(/[?#]/, 1)[0];
  return path === DIRECTORY_URL ? safe : DIRECTORY_URL;
}

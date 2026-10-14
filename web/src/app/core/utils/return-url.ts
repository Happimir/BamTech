export const DEFAULT_HOME_URL = '/people';

const AUTH_FLOW_PATHS = ['/login', '/unavailable'];

/**
 * Accepts a post-sign-in return URL only when it is a local app path. Anything that could
 * leave the origin (`//host`, `/\host`, `https:`) or loop back into the auth flow falls back
 * to the home page.
 */
export function safeReturnUrl(candidate: string | null | undefined): string {
  if (!candidate || !candidate.startsWith('/')) return DEFAULT_HOME_URL;
  if (candidate.startsWith('//') || candidate.startsWith('/\\')) return DEFAULT_HOME_URL;
  const path = candidate.split(/[?#]/, 1)[0];
  if (AUTH_FLOW_PATHS.includes(path)) return DEFAULT_HOME_URL;
  return candidate;
}

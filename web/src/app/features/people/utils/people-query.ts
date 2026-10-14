import { Params } from '@angular/router';

export const PAGE_SIZE_OPTIONS = [10, 25, 50, 100] as const;
export const DEFAULT_PAGE_SIZE = 25;
export const MAX_NAME_LENGTH = 300;

/** The applied directory query. The URL is its source of truth. */
export interface PeopleQuery {
  name: string;
  page: number;
  pageSize: number;
}

/**
 * Reads the directory query from URL parameters. Anything malformed falls back to the default,
 * so a hand-edited URL shows a valid page rather than an error.
 */
export function parsePeopleQuery(params: Params): PeopleQuery {
  const page = Number(params['page']);
  const pageSize = Number(params['pageSize']);
  const name = typeof params['name'] === 'string' ? params['name'].trim() : '';
  return {
    name,
    page: Number.isInteger(page) && page >= 1 ? page : 1,
    pageSize: (PAGE_SIZE_OPTIONS as readonly number[]).includes(pageSize) ? pageSize : DEFAULT_PAGE_SIZE,
  };
}

/** URL parameters for a query, omitting defaults so the plain directory URL stays `/people`. */
export function toQueryParams(query: PeopleQuery): Params {
  return {
    name: query.name || null,
    page: query.page === 1 ? null : query.page,
    pageSize: query.pageSize === DEFAULT_PAGE_SIZE ? null : query.pageSize,
  };
}

import { DEFAULT_PAGE_SIZE, parsePeopleQuery, toQueryParams } from './people-query';

describe('parsePeopleQuery', () => {
  it('reads a valid query', () => {
    expect(parsePeopleQuery({ name: ' Ada ', page: '3', pageSize: '50' })).toEqual({
      name: 'Ada',
      page: 3,
      pageSize: 50,
    });
  });

  it.each(['0', '-2', '1.5', 'abc', '', undefined])('falls back to page 1 for %j', (page) => {
    expect(parsePeopleQuery({ page }).page).toBe(1);
  });

  it.each(['0', '7', '101', '1000', 'abc', undefined])(
    'falls back to the default page size for %j (only offered sizes are accepted)',
    (pageSize) => expect(parsePeopleQuery({ pageSize }).pageSize).toBe(DEFAULT_PAGE_SIZE),
  );

  it('treats a whitespace-only or repeated name parameter as no filter', () => {
    expect(parsePeopleQuery({ name: '   ' }).name).toBe('');
    expect(parsePeopleQuery({ name: ['a', 'b'] }).name).toBe('');
  });
});

describe('toQueryParams', () => {
  it('omits defaults so the unfiltered first page is plain /people', () => {
    expect(toQueryParams({ name: '', page: 1, pageSize: DEFAULT_PAGE_SIZE })).toEqual({
      name: null,
      page: null,
      pageSize: null,
    });
  });

  it('round-trips a non-default query', () => {
    const query = { name: 'Ada', page: 2, pageSize: 10 };
    expect(parsePeopleQuery(toQueryParams(query))).toEqual(query);
  });
});

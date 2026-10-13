// Creates fictional sample people and duty histories through the real API (signed in as admin),
// so every directory and detail state can be demonstrated. Development only.
//
//   npm run seed:sample              (API on https://localhost:7204, Development demo accounts)
//
// Env: ACTS_API_URL (default https://localhost:7204), ACTS_ADMIN_PASSWORD (default: the
// Development demo password). Refuses to run twice: it stops if its marker person exists.
import { request } from '@playwright/test';

const API_URL = process.env.ACTS_API_URL ?? 'https://localhost:7204';
const ADMIN_PASSWORD = process.env.ACTS_ADMIN_PASSWORD ?? 'Admin!2026';
const MARKER = 'Grace Okonkwo';

/** [name, [[rank, dutyTitle, startDate], ...]] — duties in chronological order. */
const PEOPLE = [
  // Retired and later reactivated: current career end clears, the RETIRED interval stays in history.
  [MARKER, [
    ['Lieutenant', 'Pilot', '2010-01-04'],
    ['Commander', 'Mission Commander', '2015-06-01'],
    ['Commander', 'RETIRED', '2020-09-01'],
    ['Captain', 'Flight Director', '2024-02-12'],
  ]],
  // Retired.
  ['Harold Jensen', [
    ['Ensign', 'Pilot', '2012-03-01'],
    ['Lieutenant', 'Mission Specialist', '2016-07-15'],
    ['Lieutenant', 'RETIRED', '2023-01-01'],
  ]],
  ['Ingrid Solberg', [
    ['Major', 'Flight Engineer', '2008-05-19'],
    ['Major', 'RETIRED', '2019-11-30'],
  ]],
  // Long history.
  ['Yuki Tanaka', [
    ['Ensign', 'Payload Specialist', '2006-04-03'],
    ['Lieutenant', 'Flight Engineer', '2009-08-17'],
    ['Lieutenant', 'Mission Specialist', '2012-01-09'],
    ['Lieutenant Commander', 'Pilot', '2015-10-26'],
    ['Commander', 'Mission Commander', '2019-03-04'],
    ['Captain', 'Chief of Training', '2022-07-18'],
    ['Captain', 'Flight Director', '2025-05-05'],
  ]],
  // Namesakes: two different people with the same name.
  ['Alex Morgan', [['Lieutenant', 'Mission Specialist', '2021-09-13']]],
  ['Alex Morgan', []],
  // Long name and long title.
  ['Maria Fernanda de los Ángeles Castillo-Villanueva', [
    ['Lieutenant Commander', 'Extravehicular Activity Lead and Robotics Systems Specialist', '2018-02-05'],
  ]],
  // Not assigned.
  ['Priya Raman', []],
  ['Tomás Herrera', []],
  ['Wen Li', []],
  // Active, one to three duties.
  ['Amara Diallo', [['Ensign', 'Payload Specialist', '2023-06-01']]],
  ['Ben Ishikawa', [['Lieutenant', 'Pilot', '2019-04-22'], ['Lieutenant Commander', 'Mission Commander', '2024-08-01']]],
  ['Carlos Mendes', [['Major', 'Flight Engineer', '2017-01-16']]],
  ['Dana Whitfield', [['Ensign', 'Mission Specialist', '2025-03-10']]],
  ['Elif Kaya', [['Lieutenant', 'Flight Engineer', '2020-10-05'], ['Lieutenant', 'Pilot', '2023-12-04']]],
  ['Femi Adeyemi', [['Captain', 'Chief of Training', '2016-02-29']]],
  ['Hannah Becker', [['Lieutenant', 'Mission Specialist', '2022-05-23']]],
  ['Isaac Lindqvist', [['Ensign', 'Pilot', '2024-01-15']]],
  ['Jae-won Park', [['Major', 'Payload Specialist', '2014-09-08'], ['Major', 'Flight Director', '2021-02-01']]],
  ['Kofi Mensah', [['Lieutenant', 'Flight Engineer', '2021-07-12']]],
  ['Lena Novak', [['Ensign', 'Mission Specialist', '2025-09-01']]],
  ['Mateo Rossi', [['Lieutenant Commander', 'Pilot', '2018-11-19']]],
  ['Nadia Haddad', [['Commander', 'Mission Commander', '2020-03-16']]],
  ['Oscar Lindgren', [['Lieutenant', 'Flight Engineer', '2019-06-03'], ['Lieutenant', 'RETIRED', '2022-06-01'], ['Lieutenant', 'Instructor', '2025-01-06']]],
  ['Paula Ortiz', [['Ensign', 'Payload Specialist', '2024-10-21']]],
  ['Quinn Harper', [['Lieutenant', 'Mission Specialist', '2023-02-13']]],
  ['Rania Aziz', [['Major', 'Flight Engineer', '2015-08-24']]],
  ['Sam Okafor', [['Captain', 'Flight Director', '2017-05-01']]],
  ['Theo Laurent', [['Ensign', 'Pilot', '2026-01-05']]],
  ['Uma Krishnan', [['Lieutenant', 'Mission Specialist', '2021-11-08']]],
  ['Victor Petrov', [['Commander', 'Instructor', '2016-12-12']]],
];

const api = await request.newContext({ baseURL: API_URL, ignoreHTTPSErrors: true });

async function xsrfHeader() {
  const { cookies } = await api.storageState();
  const token = cookies.find((c) => c.name === 'XSRF-TOKEN')?.value;
  if (!token) throw new Error('No XSRF-TOKEN cookie; is the API running?');
  return { 'X-XSRF-TOKEN': decodeURIComponent(token) };
}

async function post(url, data) {
  const response = await api.post(url, { data, headers: await xsrfHeader() });
  if (!response.ok()) {
    throw new Error(`POST ${url} -> ${response.status()} ${await response.text()}`);
  }
  return response.status() === 204 ? null : response.json();
}

try {
  await api.get('/auth/antiforgery');
  await post('/auth/login', { userName: 'admin', password: ADMIN_PASSWORD });

  const existing = await (await api.get(`/api/people?name=${encodeURIComponent(MARKER)}`)).json();
  if (existing.totalCount > 0) {
    console.log(`Sample data already present ("${MARKER}" exists); nothing to do.`);
  } else {
    for (const [name, duties] of PEOPLE) {
      const person = await post('/api/people', { name });
      for (const [rank, dutyTitle, dutyStartDate] of duties) {
        await post(`/api/people/${person.id}/duties`, { rank, dutyTitle, dutyStartDate });
      }
    }
    console.log(`Created ${PEOPLE.length} sample people.`);
  }
  await post('/auth/logout', null);
} finally {
  await api.dispose();
}

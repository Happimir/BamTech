// Walks through the ACTS rules against the running API, printing each request and the key
// facts of each response. Every run uses fresh, uniquely named people.
//
//   npm run demo:api      (API on https://localhost:7204, Development demo accounts)
//
// Env: ACTS_API_URL (default https://localhost:7204), ACTS_ADMIN_PASSWORD (default: the
// Development demo password).
import { request } from '@playwright/test';

const API_URL = process.env.ACTS_API_URL ?? 'https://localhost:7204';
const ADMIN_PASSWORD = process.env.ACTS_ADMIN_PASSWORD ?? 'Admin!2026';
const RUN = new Date().toISOString().slice(11, 19).replaceAll(':', '');

const api = await request.newContext({ baseURL: API_URL, ignoreHTTPSErrors: true });

async function call(method, url, data) {
  const { cookies } = await api.storageState();
  const token = cookies.find((c) => c.name === 'XSRF-TOKEN')?.value;
  const response = await api.fetch(url, {
    method,
    data,
    headers: token ? { 'X-XSRF-TOKEN': decodeURIComponent(token) } : {},
  });
  const text = await response.text();
  return { status: response.status(), body: text ? JSON.parse(text) : null };
}

function step(title) {
  console.log(`\n== ${title}`);
}

function show(method, url, result, summary) {
  console.log(`${method} ${url} -> ${result.status}`);
  if (summary) console.log(`   ${summary}`);
}

const summaryLine = (astronaut) =>
  astronaut
    ? `current: ${astronaut.currentRank} / ${astronaut.currentDutyTitle} since ${astronaut.currentDutyStartDate}; ` +
      `career ${astronaut.careerStartDate} .. ${astronaut.careerEndDate ?? '(open)'}`
    : 'astronaut: null (no assignments: no astronaut records exist)';

async function addDuty(id, rank, dutyTitle, dutyStartDate) {
  const url = `/api/people/${id}/duties`;
  const result = await call('POST', url, { rank, dutyTitle, dutyStartDate });
  const detail = (await call('GET', `/api/people/${id}`)).body;
  show('POST', url, result, result.status === 201 ? summaryLine(detail.astronaut) : `${result.body.code}: ${result.body.title}`);
  return result;
}

try {
  step('Sign in as admin (antiforgery token first; every write carries X-XSRF-TOKEN)');
  await call('GET', '/auth/antiforgery');
  const login = await call('POST', '/auth/login', { userName: 'admin', password: ADMIN_PASSWORD });
  show('POST', '/auth/login', login, `${login.body.userName}: ${login.body.capabilities.join(', ')}`);

  step('Rule 2: a new person has no astronaut records');
  const name = `Demo Vance ${RUN}`;
  const created = await call('POST', '/api/people', { name });
  const id = created.body.id;
  show('POST', '/api/people', created, `id ${id}; ${summaryLine(created.body.astronaut)}`);

  step('Rules 3–5: each new duty becomes current; the previous one ends the day before');
  await addDuty(id, 'Lieutenant', 'Pilot', '2015-03-01');
  await addDuty(id, 'Commander', 'Mission Commander', '2019-07-15');

  step('Rules 6–7: RETIRED (any case) retires; career ends the day before retirement');
  await addDuty(id, 'Commander', 'retired', '2023-01-01');
  step('Policy: retiring again while retired is refused');
  await addDuty(id, 'Commander', 'RETIRED', '2024-01-01');

  step('Policy: reactivation clears the current career end; the retirement stays in history');
  await addDuty(id, 'Captain', 'Flight Director', '2025-02-03');

  step('Policy: a start date not after the current duty is a chronology conflict (nothing changes)');
  const conflict = await addDuty(id, 'Captain', 'Instructor', '2024-06-01');

  step('History, newest first, from one consistent snapshot');
  const history = await call('GET', `/api/people/${id}/duties`);
  show('GET', `/api/people/${id}/duties`, history);
  for (const d of history.body.duties) {
    console.log(`   ${d.dutyStartDate} .. ${d.dutyEndDate ?? '(current)'}  ${d.dutyTitle} (${d.rank})`);
  }

  step('Rule 1 replaced: namesakes are separate people; a name alone never picks one');
  const twinName = `Demo Twin ${RUN}`;
  const twinA = (await call('POST', '/api/people', { name: twinName })).body;
  const twinB = (await call('POST', '/api/people', { name: twinName })).body;
  console.log(`   created two "${twinName}": ${twinA.id} and ${twinB.id}`);
  const byName = await call('GET', `/Person/${encodeURIComponent(twinName)}`);
  show('GET', `/Person/${twinName}`, byName, `${byName.body.code}: ${byName.body.title}`);
  const legacyDuty = await call('POST', '/AstronautDuty', {
    name: twinName,
    rank: 'Ensign',
    dutyTitle: 'Pilot',
    dutyStartDate: '2024-05-01',
  });
  show('POST', '/AstronautDuty (by name)', legacyDuty, `${legacyDuty.body.code}: nothing was written`);
  const search = await call('GET', `/api/people?name=${encodeURIComponent(twinName)}`);
  show('GET', `/api/people?name=${twinName}`, search, `${search.body.totalCount} matches, each with its own id`);
  await addDuty(twinB.id, 'Ensign', 'Pilot', '2024-05-01');

  step('Correlated database logs');
  console.log(`   The chronology conflict above returned traceId ${conflict.body.traceId}.`);
  console.log(`   SELECT Phase, Operation, Outcome, HttpStatusCode, ErrorCode FROM ProcessLog`);
  console.log(`   WHERE TraceId = '${conflict.body.traceId}';`);
  console.log(`   Change history: SELECT Message, ChangesJson FROM ProcessLog`);
  console.log(`   WHERE PersonId = upper('${id}') AND Phase = 'MutationAudit' ORDER BY OccurredAtUtc;`);

  await call('POST', '/auth/logout');
} finally {
  await api.dispose();
}

// Walks through the ACTS rules against the running API, printing each GraphQL operation and the
// key facts of each result. Every run uses fresh, uniquely named people.
//
//   npm run demo:api      (API on https://localhost:7204, Development demo accounts)
//
// Env: ACTS_API_URL (default https://localhost:7204), ACTS_ADMIN_PASSWORD (default: the
// Development demo password).
import { connect, ERROR_FIELDS, PERSON_FIELDS } from './acts-api.mjs';

const RUN = new Date().toISOString().slice(11, 19).replaceAll(':', '');

const CREATE_PERSON = `mutation CreatePerson($input: CreatePersonInput!) {
  createPerson(input: $input) { person { ${PERSON_FIELDS} } ${ERROR_FIELDS} }
}`;

const ASSIGN_DUTY = `mutation AssignDuty($input: AssignDutyInput!) {
  assignDuty(input: $input) { person { ${PERSON_FIELDS} } ${ERROR_FIELDS} }
}`;

const PERSON_WITH_DUTIES = `query PersonWithDuties($id: UUID!) {
  person(id: $id) { ${PERSON_FIELDS} duties { dutyTitle rank dutyStartDate dutyEndDate } }
}`;

const PERSON_BY_NAME = `query PersonByName($name: String!) {
  personByName(name: $name) { __typename ... on Person { id } ... on Error { code message } }
}`;

const SEARCH = `query Search($name: String!) { people(name: $name) { totalCount items { id } } }`;

function step(title) {
  console.log(`\n== ${title}`);
}

function show(operation, summary) {
  console.log(operation);
  if (summary) console.log(`   ${summary}`);
}

const summaryLine = (astronaut) =>
  astronaut
    ? `current: ${astronaut.currentRank} / ${astronaut.currentDutyTitle} since ${astronaut.currentDutyStartDate} ` +
      `(${astronaut.status}); career ${astronaut.careerStartDate} .. ${astronaut.careerEndDate ?? '(open)'}`
    : 'astronaut: null (no assignments: no astronaut records exist)';

const errorLine = (error) => `${error.__typename} (${error.code}): ${error.message}`;

const api = await connect();

/** Adds a duty by ID or by exact name; prints the person's summary or the refusal. */
async function addDuty(person, rank, dutyTitle, dutyStartDate) {
  const result = await api.graphql(ASSIGN_DUTY, { input: { person, rank, dutyTitle, dutyStartDate } });
  const payload = result.data.assignDuty;
  const [error] = payload.errors ?? [];
  show(
    `mutation assignDuty(${person.id ? 'id' : `name: "${person.name}"`}, ${dutyTitle}, ${dutyStartDate})`,
    error ? `${errorLine(error)}; nothing was written` : summaryLine(payload.person.astronaut),
  );
  return { error, traceId: result.traceId };
}

try {
  step('Sign in as admin (antiforgery token first; every POST, GraphQL included, carries X-XSRF-TOKEN)');
  const me = await api.signIn();
  show('POST /auth/login', `${me.userName}: ${me.capabilities.join(', ')}`);

  step('Rule 2: a new person has no astronaut records');
  const name = `Demo Vance ${RUN}`;
  const created = (await api.graphql(CREATE_PERSON, { input: { name } })).data.createPerson.person;
  const id = created.id;
  show('mutation createPerson', `id ${id}; ${summaryLine(created.astronaut)}`);

  step('Rules 3–5: each new duty becomes current; the previous one ends the day before');
  await addDuty({ id }, 'Lieutenant', 'Pilot', '2015-03-01');
  await addDuty({ id }, 'Commander', 'Mission Commander', '2019-07-15');

  step('Rules 6–7: RETIRED (any case) retires; career ends the day before retirement');
  await addDuty({ id }, 'Commander', 'retired', '2023-01-01');
  step('Policy: retiring again while retired is refused');
  await addDuty({ id }, 'Commander', 'RETIRED', '2024-01-01');

  step('Policy: reactivation clears the current career end; the retirement stays in history');
  await addDuty({ id }, 'Captain', 'Flight Director', '2025-02-03');

  step('Policy: a start date not after the current duty is a chronology conflict (nothing changes)');
  const conflict = await addDuty({ id }, 'Captain', 'Instructor', '2024-06-01');

  step('History, newest first: the person and their duties in one query, read as one snapshot');
  const history = (await api.graphql(PERSON_WITH_DUTIES, { id })).data.person;
  show('query person { duties }');
  for (const d of history.duties) {
    console.log(`   ${d.dutyStartDate} .. ${d.dutyEndDate ?? '(current)'}  ${d.dutyTitle} (${d.rank})`);
  }

  step('Rule 1 replaced: namesakes are separate people; a name alone never picks one');
  const twinName = `Demo Twin ${RUN}`;
  const twinA = (await api.graphql(CREATE_PERSON, { input: { name: twinName } })).data.createPerson.person;
  const twinB = (await api.graphql(CREATE_PERSON, { input: { name: twinName } })).data.createPerson.person;
  console.log(`   created two "${twinName}": ${twinA.id} and ${twinB.id}`);
  const byName = (await api.graphql(PERSON_BY_NAME, { name: twinName })).data.personByName;
  show(`query personByName("${twinName}")`, errorLine(byName));
  await addDuty({ name: twinName }, 'Ensign', 'Pilot', '2024-05-01');
  const search = (await api.graphql(SEARCH, { name: twinName })).data.people;
  show(`query people(name: "${twinName}")`, `${search.totalCount} matches, each with its own id`);
  await addDuty({ id: twinB.id }, 'Ensign', 'Pilot', '2024-05-01');

  step('Correlated database logs');
  console.log(`   The chronology conflict above returned traceresponse ${conflict.traceId}.`);
  console.log(`   SELECT Phase, Operation, Outcome, HttpStatusCode, ErrorCode FROM ProcessLog`);
  console.log(`   WHERE TraceId = '${conflict.traceId}';`);
  console.log(`   Change history: SELECT Message, ChangesJson FROM ProcessLog`);
  console.log(`   WHERE PersonId = upper('${id}') AND Phase = 'MutationAudit' ORDER BY OccurredAtUtc;`);

  await api.signOut();
} finally {
  await api.dispose();
}

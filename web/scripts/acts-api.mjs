// A minimal ACTS client for the Node scripts: the browser's sign-in flow (antiforgery token,
// then credentials) and GraphQL operations posted to /graphql with the X-XSRF-TOKEN header.
import { request } from '@playwright/test';

export const API_URL = process.env.ACTS_API_URL ?? 'https://localhost:7204';
const ADMIN_PASSWORD = process.env.ACTS_ADMIN_PASSWORD ?? 'Admin!2026';

export const PERSON_FIELDS = `
  id name version
  astronaut { currentRank currentDutyTitle currentDutyStartDate careerStartDate careerEndDate status }`;

export const ERROR_FIELDS = `errors { __typename ... on Error { code message } }`;

export async function connect() {
  const api = await request.newContext({ baseURL: API_URL, ignoreHTTPSErrors: true });

  async function xsrfHeader() {
    const { cookies } = await api.storageState();
    const token = cookies.find((c) => c.name === 'XSRF-TOKEN')?.value;
    return token ? { 'X-XSRF-TOKEN': decodeURIComponent(token) } : {};
  }

  async function post(url, data) {
    const response = await api.post(url, { data, headers: await xsrfHeader() });
    const text = await response.text();
    return { status: response.status(), headers: response.headers(), body: text ? JSON.parse(text) : null };
  }

  return {
    /** Signs in as admin; returns the /auth/login body (user name and capabilities). */
    async signIn() {
      const antiforgery = await api.get('/auth/antiforgery');
      if (!antiforgery.ok()) throw new Error(`GET /auth/antiforgery -> ${antiforgery.status()}; is the API running?`);
      const login = await post('/auth/login', { userName: 'admin', password: ADMIN_PASSWORD });
      if (login.status !== 200) throw new Error(`POST /auth/login -> ${login.status} ${JSON.stringify(login.body)}`);
      return login.body;
    },

    /**
     * Sends one operation. Returns { data, traceId }; request errors (a denied field, an invalid
     * document, a server fault) throw. Business errors are data: check the payload's errors.
     */
    async graphql(query, variables) {
      const response = await post('/graphql', { query, variables });
      if (response.status !== 200 || response.body?.errors?.length) {
        throw new Error(`POST /graphql -> ${response.status} ${JSON.stringify(response.body)}`);
      }
      return { data: response.body.data, traceId: response.headers['traceresponse'] };
    },

    async signOut() {
      await post('/auth/logout', null);
    },

    dispose: () => api.dispose(),
  };
}

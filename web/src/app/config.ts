/**
 * No build-time environment files: the dev server always runs on port 4200,
 * so that's the signal to talk to the local Aegis.Api dev port directly.
 * In any other deployment (docker-compose, a static host) the app is served
 * behind a reverse proxy that forwards /api to Aegis.Api, so a relative
 * path is correct.
 */
export const API_BASE_URL = typeof window !== 'undefined' && window.location.port === '4200'
  ? 'http://localhost:5092'
  : '';

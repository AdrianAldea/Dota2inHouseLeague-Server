/**
 * Frontend runtime configuration. The API base paths are the proxy prefixes
 * declared in `proxy.conf.json` (dev) and the reverse proxy (prod); both
 * environments use the same relative paths, so a single file suffices.
 */
export const environment = {
  production: false,

  /** Discord service, e.g. `${discordApi}/start`. */
  discordApi: '/discord/api',

  /** Game Manager domain API, e.g. `${gameManagerApi}/queue/status`. */
  gameManagerApi: '/gamemanager/api/gamemanager',

  /** Game Manager server-lifecycle API, e.g. `${serverApi}/start`. */
  serverApi: '/gamemanager/api/server',

  /** Upper bound applied to every HTTP request by the timeout interceptor. */
  httpTimeoutMs: 15000,
};

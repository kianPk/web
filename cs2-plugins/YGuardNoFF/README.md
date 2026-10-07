# YGuard No Friendly Fire

Panel-driven gameplay prefs on **Public / Custom dedicated** servers:

- Friendly fire (`mp_friendlyfire`) — default **off**
- Auto bunny hop (`sv_autobunnyhopping` / `sv_enablebunnyhopping`) — default **off**

Ranked (matchmaking) pods and Practice are left alone.

Prefs are read from `GET /hosted-servers/plugin/state` every ~30s and on map start, so toggles from the hosting panel stick across map changes. Instant apply also goes over RCON from the API.

## Env

Uses the same vars as other panel plugins: `API_DOMAIN`, `SERVER_ID`, `SERVER_API_PASSWORD`, `SERVER_TYPE`.

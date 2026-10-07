# YGuard No Friendly Fire

Panel-driven gameplay prefs on **Public / Custom dedicated** servers:

- Friendly fire (`mp_friendlyfire`) — default **off**
- Auto bunny hop (`sv_autobunnyhopping` / `sv_enablebunnyhopping`) — default **off**
- Parachute — hold **E** while airborne to fall slowly — default **off**

Ranked (matchmaking) pods and Practice are left alone.

Prefs are read from `GET /hosted-servers/plugin/state` every ~15s and on map start, so toggles from the hosting panel stick across map changes. FF/bhop also apply instantly over RCON from the API; parachute is applied in-plugin (hold E) and the API nudges `css_yguard_nof_reload` so the flag flips without waiting for the poll.

## Env

Uses the same vars as other panel plugins: `API_DOMAIN`, `SERVER_ID`, `SERVER_API_PASSWORD`, `SERVER_TYPE`.

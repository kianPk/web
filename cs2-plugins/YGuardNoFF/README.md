# YGuard No Friendly Fire

Panel-driven gameplay prefs on **Public / Custom dedicated** servers:

- Friendly fire (`mp_friendlyfire`) — default **off**
- Auto bunny hop (`sv_autobunnyhopping` / `sv_enablebunnyhopping`) — default **off**
- Parachute — hold **E** while airborne to fall slowly — default **off**

Ranked (matchmaking) pods and Practice are left alone.

Prefs are read from `GET /hosted-servers/plugin/state` every ~15s and on map start, so toggles from the hosting panel stick across map changes. FF/bhop also apply instantly over RCON from the API.

Parachute (hold **E** in the air) follows the same physics as [Franc1sco/CS2-Parachute](https://github.com/Franc1sco/CS2-Parachute): `GravityScale = 0.1` + fall-speed clamp. The API nudges `css_yguard_nof_reload` so the flag flips without waiting for the poll. Debug: `css_yguard_parachute 1|0`.

## Env

Uses the same vars as other panel plugins: `API_DOMAIN`, `SERVER_ID`, `SERVER_API_PASSWORD`, `SERVER_TYPE`.

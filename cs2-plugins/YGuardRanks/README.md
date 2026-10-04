# YGuardRanks

رنک سرورهای پابلیک YGuard: امتیاز روی پنل، آیکون Competitive (سیلور تا گلوبال) روی TAB.

## نیازمندی‌ها

- CounterStrikeSharp (net10 / API 1.0.374+)
- در `addons/counterstrikesharp/configs/core.json`:
  ```json
  "FollowCS2ServerGuidelines": false
  ```
- پاد گیم‌سرور باید envهای `SERVER_ID`، `SERVER_API_PASSWORD`، `API_DOMAIN` را داشته باشد (5Stack به‌صورت پیش‌فرض می‌گذارد)

## نصب

1. بیلد:
   ```bash
   dotnet build -c Release
   ```
2. خروجی را اینجا کپی کن:
   ```
   addons/counterstrikesharp/plugins/YGuardRanks/YGuardRanks.dll
   ```
   یا روی نود: `/opt/5stack/custom-plugins/YGuardRanks/`
3. API را با مایگریشن `1906000000000_public_server_ranks` دیپلوی کن.
4. سرور را ری‌استارت کن.

## دستورات

| چت | کار |
|---|---|
| `!rank` / `!myrank` | رنک و امتیاز خودت |
| `!top` / `!ranktop` | ۱۰ نفر برتر |

## امتیاز پیش‌فرض

- کیل ۵ + هدشات ۲ · اسیست ۲ · مرگ −۲ · MVP ۳ · برد راند ۱
- حداقل ۴ بازیکن انسانی در تیم‌ها
- روی Ranked / Practice خاموش است

## API پنل

- `POST /hosted-servers/plugin/ranks/sync`
- `GET /hosted-servers/plugin/ranks/players`
- `GET /hosted-servers/plugin/ranks/top`
- `GET /hosted-servers/ranks/leaderboard` (عمومی)

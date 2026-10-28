#!/usr/bin/env bash
# Restore GraphQL `teams` for logged-in roles.
# Fixes toast errors:
#   field 'teams' not found in type: 'players'
#   field 'teams' not found in type: 'query_root'
#
# Usage on the panel VPS:
#   curl -fsSL https://raw.githubusercontent.com/kianPk/web/main/scripts/fix-teams-hasura-perms-vps.sh | bash
set -euo pipefail
export KUBECONFIG="${KUBECONFIG:-/etc/rancher/k3s/k3s.yaml}"
NS=5stack

HASURA_POD=$(kubectl -n "$NS" get pods -o name \
  | grep -E 'pod/hasura' | head -1 | cut -d/ -f2)
echo "hasura: $HASURA_POD"

ADMIN_SECRET=$(kubectl -n "$NS" get secret -o name \
  | grep -E 'hasura' | head -1 \
  | xargs -I{} kubectl -n "$NS" get {} -o jsonpath='{.data.HASURA_GRAPHQL_ADMIN_SECRET}' \
  | base64 -d || true)
if [[ -z "${ADMIN_SECRET}" ]]; then
  ADMIN_SECRET=$(kubectl -n "$NS" get deploy hasura -o yaml \
    | awk '/HASURA_GRAPHQL_ADMIN_SECRET/{want=1; next} want && /value:/{gsub(/[" ]/,"",$2); print $2; exit} want && /valueFrom/{want=0}')
fi
if [[ -z "${ADMIN_SECRET}" ]]; then
  echo "could not read HASURA_GRAPHQL_ADMIN_SECRET" >&2
  exit 1
fi

meta() {
  local payload="$1"
  kubectl -n "$NS" exec "$HASURA_POD" -- bash -lc "
curl -sS -X POST http://127.0.0.1:8080/v1/metadata \
  -H 'Content-Type: application/json' \
  -H 'x-hasura-admin-secret: ${ADMIN_SECRET}' \
  -d $(printf '%q' "$payload")
"
  echo
}

TEAMS_PERM='{
  "columns": ["avatar_url","captain_steam_id","id","is_organization","name","owner_steam_id","short_name"],
  "computed_fields": ["can_change_role","can_invite","can_manage_scrims","can_remove","matches","role"],
  "filter": {},
  "allow_aggregations": true
}'

PLAYER_COMPUTED='["current_lobby_id","elo","banned_until","is_admin_sanctioned","is_banned","is_registered","is_gagged","is_muted","losses","losses_competitive","losses_duel","losses_wingman","matchmaking_cooldown","peak_elo","teams","tournament_cooldown","total_matches","wins","wins_competitive","wins_duel","wins_wingman"]'
PLAYER_COMPUTED_USER='["current_lobby_id","elo","banned_until","is_admin_sanctioned","is_banned","is_registered","is_gagged","is_in_another_match","is_in_draft","is_in_lobby","is_muted","losses","losses_competitive","losses_duel","losses_wingman","matchmaking_cooldown","peak_elo","teams","tournament_cooldown","total_matches","wins","wins_competitive","wins_duel","wins_wingman"]'

drop_select() {
  local table="$1" role="$2"
  meta "{\"type\":\"pg_drop_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"${table}\"},\"role\":\"${role}\"}}" >/dev/null 2>&1 || true
}

create_teams_select() {
  local role="$1"
  meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"teams\"},\"role\":\"${role}\",\"permission\":${TEAMS_PERM}}}"
}

echo "restoring teams select for user roles..."
for role in user match_organizer tournament_organizer; do
  drop_select teams "$role"
  create_teams_select "$role"
done

echo "restoring players.teams computed field..."
drop_select players guest
meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"players\"},\"role\":\"guest\",\"permission\":{\"columns\":[\"avatar_url\",\"country\",\"custom_avatar_url\",\"faceit_elo\",\"faceit_nickname\",\"faceit_player_id\",\"faceit_skill_level\",\"faceit_updated_at\",\"faceit_url\",\"language\",\"name\",\"name_registered\",\"premier_rank\",\"premier_rank_updated_at\",\"profile_url\",\"role\",\"roster_image_url\",\"show_match_ready_modal\",\"steam_id\",\"primary_club_id\",\"vac_banned\",\"vac_ban_count\",\"game_ban_count\",\"days_since_last_ban\"],\"computed_fields\":${PLAYER_COMPUTED},\"filter\":{}}}}"

drop_select players user
meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"players\"},\"role\":\"user\",\"permission\":{\"columns\":[\"avatar_url\",\"country\",\"last_read_news_at\",\"custom_avatar_url\",\"faceit_elo\",\"faceit_nickname\",\"faceit_player_id\",\"faceit_skill_level\",\"faceit_updated_at\",\"faceit_url\",\"language\",\"name\",\"name_registered\",\"premier_rank\",\"premier_rank_updated_at\",\"profile_url\",\"role\",\"roster_image_url\",\"show_match_ready_modal\",\"steam_id\",\"primary_club_id\",\"vac_banned\",\"vac_ban_count\",\"game_ban_count\",\"days_since_last_ban\"],\"computed_fields\":${PLAYER_COMPUTED_USER},\"filter\":{},\"allow_aggregations\":true}}}"

drop_select players match_organizer
meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"players\"},\"role\":\"match_organizer\",\"permission\":{\"columns\":[\"avatar_url\",\"country\",\"custom_avatar_url\",\"faceit_elo\",\"faceit_nickname\",\"faceit_player_id\",\"faceit_skill_level\",\"faceit_updated_at\",\"faceit_url\",\"language\",\"last_read_news_at\",\"last_sign_in_at\",\"name\",\"name_registered\",\"premier_rank\",\"premier_rank_updated_at\",\"profile_url\",\"role\",\"roster_image_url\",\"show_match_ready_modal\",\"steam_id\",\"primary_club_id\",\"vac_banned\",\"vac_ban_count\",\"game_ban_count\",\"days_since_last_ban\"],\"computed_fields\":${PLAYER_COMPUTED_USER},\"filter\":{}}}}"

echo "done — hard-refresh the site"

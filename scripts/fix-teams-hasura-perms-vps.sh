#!/usr/bin/env bash
# Restore GraphQL teams access + repair players select if a prior run dropped it.
#
# On this Hasura build, table-returning computed fields (players.teams,
# teams.matches) are AUTO-DERIVED from select on the returning table — they
# must NOT be listed in computed_fields.
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
  local out
  out=$(kubectl -n "$NS" exec "$HASURA_POD" -c hasura -- bash -lc "
curl -sS -X POST http://127.0.0.1:8080/v1/metadata \
  -H 'Content-Type: application/json' \
  -H 'x-hasura-admin-secret: ${ADMIN_SECRET}' \
  -d $(printf '%q' "$payload")
" 2>/dev/null || true)
  echo "$out"
  if echo "$out" | grep -q '"error"'; then
    return 1
  fi
  return 0
}

drop_select() {
  local table="$1" role="$2"
  meta "{\"type\":\"pg_drop_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"${table}\"},\"role\":\"${role}\"}}" >/dev/null 2>&1 || true
}

# Scalar computed fields only — never list table-returning ones (teams/matches).
PLAYER_COMPUTED_GUEST='["current_lobby_id","elo","banned_until","is_admin_sanctioned","is_banned","is_registered","is_gagged","is_muted","losses","losses_competitive","losses_duel","losses_wingman","matchmaking_cooldown","peak_elo","tournament_cooldown","total_matches","wins","wins_competitive","wins_duel","wins_wingman"]'
PLAYER_COMPUTED_USER='["current_lobby_id","elo","banned_until","is_admin_sanctioned","is_banned","is_registered","is_gagged","is_in_another_match","is_in_draft","is_in_lobby","is_muted","losses","losses_competitive","losses_duel","losses_wingman","matchmaking_cooldown","peak_elo","tournament_cooldown","total_matches","wins","wins_competitive","wins_duel","wins_wingman"]'
TEAMS_COMPUTED='["can_change_role","can_invite","can_manage_scrims","can_remove","role"]'
TEAMS_COLS='["avatar_url","captain_steam_id","id","is_organization","name","owner_steam_id","short_name"]'
PLAYER_COLS_GUEST='["avatar_url","country","custom_avatar_url","faceit_elo","faceit_nickname","faceit_player_id","faceit_skill_level","faceit_updated_at","faceit_url","language","name","name_registered","premier_rank","premier_rank_updated_at","profile_url","role","roster_image_url","show_match_ready_modal","steam_id","primary_club_id","vac_banned","vac_ban_count","game_ban_count","days_since_last_ban"]'
PLAYER_COLS_USER='["avatar_url","country","last_read_news_at","custom_avatar_url","faceit_elo","faceit_nickname","faceit_player_id","faceit_skill_level","faceit_updated_at","faceit_url","language","name","name_registered","premier_rank","premier_rank_updated_at","profile_url","role","roster_image_url","show_match_ready_modal","steam_id","primary_club_id","vac_banned","vac_ban_count","game_ban_count","days_since_last_ban"]'
PLAYER_COLS_MO='["avatar_url","country","custom_avatar_url","faceit_elo","faceit_nickname","faceit_player_id","faceit_skill_level","faceit_updated_at","faceit_url","language","last_read_news_at","last_sign_in_at","name","name_registered","premier_rank","premier_rank_updated_at","profile_url","role","roster_image_url","show_match_ready_modal","steam_id","primary_club_id","vac_banned","vac_ban_count","game_ban_count","days_since_last_ban"]'

echo "1) restoring players select (guest/user/match_organizer)..."
drop_select players guest
meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"players\"},\"role\":\"guest\",\"permission\":{\"columns\":${PLAYER_COLS_GUEST},\"computed_fields\":${PLAYER_COMPUTED_GUEST},\"filter\":{}}}}"

drop_select players user
meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"players\"},\"role\":\"user\",\"permission\":{\"columns\":${PLAYER_COLS_USER},\"computed_fields\":${PLAYER_COMPUTED_USER},\"filter\":{},\"allow_aggregations\":true}}}"

drop_select players match_organizer
meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"players\"},\"role\":\"match_organizer\",\"permission\":{\"columns\":${PLAYER_COLS_MO},\"computed_fields\":${PLAYER_COMPUTED_USER},\"filter\":{}}}}"

echo "2) adding teams select for logged-in roles (auto-derives players.teams)..."
for role in user match_organizer tournament_organizer; do
  drop_select teams "$role"
  meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"teams\"},\"role\":\"${role}\",\"permission\":{\"columns\":${TEAMS_COLS},\"computed_fields\":${TEAMS_COMPUTED},\"filter\":{},\"allow_aggregations\":true}}}"
done

echo "3) fixing guest teams select (drop manual matches computed field if present)..."
drop_select teams guest
meta "{\"type\":\"pg_create_select_permission\",\"args\":{\"source\":\"default\",\"table\":{\"schema\":\"public\",\"name\":\"teams\"},\"role\":\"guest\",\"permission\":{\"columns\":${TEAMS_COLS},\"computed_fields\":${TEAMS_COMPUTED},\"filter\":{},\"allow_aggregations\":true}}}"

echo "done — hard-refresh the site"

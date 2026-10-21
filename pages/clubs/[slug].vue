<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import {
  MessageSquare,
  Trophy,
  Users,
  Swords,
  ListOrdered,
  PartyPopper,
  Settings,
  Newspaper,
  Search,
  Shield,
  CalendarDays,
} from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import ClubAvatar from "~/components/clubs/ClubAvatar.vue";
import ClubSocialLinks from "~/components/clubs/ClubSocialLinks.vue";
import ClubTeamBadge from "~/components/clubs/ClubTeamBadge.vue";
import TournamentCard from "~/components/tournament/TournamentCard.vue";
import { tournamentCardFields } from "~/graphql/tournamentCardFields";
import { generateQuery } from "~/graphql/graphqlGen";
import { $, order_by } from "~/generated/zeus";
import PlayerDisplay from "~/components/PlayerDisplay.vue";
import ImageUploadTile from "~/components/ImageUploadTile.vue";
import { resolveAvatarUrl } from "~/utilities/avatarUrl";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";
import { clubErrorMessage } from "~/utilities/clubErrors";
import { parseInstagramUrl, parseTelegramUrl } from "~/utilities/clubSocials";

const VALID_TABS = [
  "overview",
  "members",
  "chat",
  "leaderboard",
  "queues",
  "parties",
  "teams",
  "tournaments",
  "clash",
  "admin",
] as const;
type Tab = (typeof VALID_TABS)[number];

const route = useRoute();
const { t } = useI18n();
const { client } = useApolloClient();
const auth = useAuthStore();
const me = computed(() => auth.me);

const slugParam = computed(() => String(route.params.slug || ""));
const tab = ref<Tab>(
  VALID_TABS.includes(route.query.tab as Tab)
    ? (route.query.tab as Tab)
    : "overview",
);

watch(tab, (value) => {
  if (typeof window === "undefined") return;
  const url = new URL(window.location.href);
  if (value === "overview") url.searchParams.delete("tab");
  else url.searchParams.set("tab", value);
  window.history.replaceState(window.history.state, "", url.toString());
});

const club = ref<any>(null);
const loading = ref(true);
const error = ref("");
const busy = ref(false);
const inviteSteamId = ref("");
const inviteMessage = ref("");
const joinMessage = ref("");
const chatBody = ref("");
const activeChannelId = ref<string | null>(null);
const linkMaxUses = ref<number | null>(null);
const linkHours = ref<number | null>(24);
const linkBypass = ref(false);
const newQueueName = ref("In-house 5v5");
const newPartyTitle = ref("");
const guestSteamId = ref("");
const seasonLbName = ref("Season");
const minElo = ref<number | null>(null);
const minLevel = ref<number | null>(null);
const createdLinkCode = ref("");
const memberQuery = ref("");
const transferSteamId = ref("");
const telegramInput = ref("");
const instagramInput = ref("");

const myMembership = computed(() => {
  if (!me.value || !club.value) return null;
  return (
    club.value.members?.find(
      (m: any) => String(m.steam_id) === String(me.value.steam_id),
    ) ?? null
  );
});
const canManage = computed(() =>
  ["owner", "admin"].includes(myMembership.value?.role),
);
const isOwner = computed(
  () =>
    myMembership.value?.role === "owner" ||
    String(club.value?.owner_steam_id) === String(me.value?.steam_id),
);
const canModerate = computed(() =>
  ["owner", "admin", "moderator"].includes(myMembership.value?.role),
);
const isMember = computed(() => !!myMembership.value);
const apiDomain = useRuntimeConfig().public.apiDomain as string;
const coverSrc = computed(() =>
  resolveAvatarUrl(club.value?.cover_url, apiDomain),
);
const clubAsMemberClub = computed(() =>
  club.value
    ? [
        {
          id: club.value.id,
          name: club.value.name,
          slug: club.value.slug,
          short_name: club.value.short_name,
          avatar_url: club.value.avatar_url,
        },
      ]
    : [],
);
const avatarSrc = computed(() =>
  resolveAvatarUrl(club.value?.avatar_url, apiDomain),
);
const bannerUploadUrl = computed(
  () => `https://${apiDomain}/avatars/clubs/${club.value?.id}/banner`,
);
const avatarUploadUrl = computed(
  () => `https://${apiDomain}/avatars/clubs/${club.value?.id}/avatar`,
);

function onCoverUploaded(path: string) {
  if (!club.value) return;
  club.value.cover_url = path || club.value.cover_url;
}
function onCoverRemoved() {
  if (!club.value) return;
  club.value.cover_url = null;
}
function onAvatarUploaded(path: string) {
  if (!club.value) return;
  club.value.avatar_url = path || club.value.avatar_url;
}
function onAvatarRemoved() {
  if (!club.value) return;
  club.value.avatar_url = null;
}

const activeChannel = computed(() => {
  const channels = club.value?.channels ?? [];
  return (
    channels.find((c: any) => c.id === activeChannelId.value) || channels[0]
  );
});

async function load() {
  loading.value = true;
  error.value = "";
  try {
    const isUuid =
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
        slugParam.value,
      );
    const { data } = await client.query({
      query: gql`
        query ClubDetail($where: clubs_bool_exp!) {
          clubs(where: $where, limit: 1) {
            id
            name
            slug
            short_name
            avatar_url
            cover_url
            description
            join_mode
            min_elo
            min_faceit_level
            owner_steam_id
            role
            can_manage
            created_at
            members_aggregate {
              aggregate {
                count
              }
            }
            members(order_by: [{ role: asc }, { joined_at: asc }]) {
              steam_id
              role
              is_primary
              joined_at
              player {
                steam_id
                name
                avatar_url
                faceit_elo
                faceit_skill_level
              }
            }
            activity(order_by: [{ created_at: desc }], limit: 40) {
              id
              kind
              payload
              created_at
              actor_steam_id
            }
            channels(order_by: [{ name: asc }]) {
              id
              name
              slug
              messages(order_by: [{ created_at: desc }], limit: 80) {
                id
                body
                created_at
                deleted_at
                author_steam_id
                author {
                  name
                  steam_id
                }
              }
            }
            leaderboards(order_by: [{ created_at: asc }]) {
              id
              name
              type
              points_win
              points_loss
              starting_points
              frozen
              starts_at
              ends_at
              entries(
                order_by: [{ points: desc }]
                limit: 50
              ) {
                steam_id
                points
                wins
                losses
                streak
                player {
                  name
                  steam_id
                  avatar_url
                  faceit_elo
                  faceit_skill_level
                }
              }
            }
            queues(order_by: [{ created_at: asc }]) {
              id
              name
              region
              team_size
              min_players
              enabled
              map_pool
              players {
                steam_id
                joined_at
                player {
                  name
                }
              }
            }
            parties(order_by: [{ created_at: desc }], limit: 30) {
              id
              title
              listed
              min_elo
              max_elo
              leader_steam_id
              members {
                steam_id
                is_guest
                player {
                  name
                  steam_id
                  avatar_url
                }
              }
            }
            invites(order_by: [{ created_at: desc }], limit: 30) {
              id
              steam_id
              created_at
              player {
                name
              }
            }
            invite_links(order_by: [{ created_at: desc }], limit: 20) {
              id
              code
              max_uses
              uses
              expires_at
              bypass_requirements
              revoked_at
              created_at
            }
            join_requests(
              where: { status: { _eq: "pending" } }
              order_by: [{ created_at: desc }]
            ) {
              id
              steam_id
              message
              created_at
              player {
                name
              }
            }
            bans(order_by: [{ created_at: desc }]) {
              id
              steam_id
              reason
              created_at
              player {
                name
              }
            }
          }
        }
      `,
      variables: {
        where: isUuid
          ? { _or: [{ id: { _eq: slugParam.value } }, { slug: { _eq: slugParam.value } }] }
          : { slug: { _eq: slugParam.value } },
      },
      fetchPolicy: "network-only",
    });
    club.value = data?.clubs?.[0] ?? null;
    if (!club.value) error.value = t("pages.clubs.not_found");
    else {
      minElo.value = club.value.min_elo;
      minLevel.value = club.value.min_faceit_level;
      telegramInput.value = club.value.telegram_url || "";
      instagramInput.value = club.value.instagram_url || "";
      void loadSocials(club.value.id);
      if (!activeChannelId.value && club.value.channels?.[0]) {
        activeChannelId.value = club.value.channels[0].id;
      }
    }
  } catch (e: any) {
    error.value = e?.message || t("pages.clubs.load_failed");
    club.value = null;
  } finally {
    loading.value = false;
  }
}

onMounted(() => void load());
watch(slugParam, () => void load());

async function loadSocials(clubId: string) {
  try {
    const { data } = await client.query({
      query: gql`
        query ClubSocials($id: uuid!) {
          clubs_by_pk(id: $id) {
            telegram_url
            instagram_url
          }
        }
      `,
      variables: { id: clubId },
      fetchPolicy: "network-only",
      context: { optional: true },
    });
    const row = data?.clubs_by_pk;
    if (!row || !club.value || club.value.id !== clubId) return;
    club.value.telegram_url = row.telegram_url;
    club.value.instagram_url = row.instagram_url;
    telegramInput.value = row.telegram_url || "";
    instagramInput.value = row.instagram_url || "";
  } catch {
    // Columns land after the Hasura metadata apply.
  }
}

async function runAction(mutation: string, variables: Record<string, unknown>) {
  busy.value = true;
  error.value = "";
  try {
    await client.mutate({ mutation: gql(mutation), variables });
    await load();
  } catch (e: any) {
    error.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
}

async function joinOpen() {
  await runAction(
    `mutation($club_id: uuid!) { joinClubOpen(club_id: $club_id) { success } }`,
    { club_id: club.value.id },
  );
}

async function requestJoin() {
  if (!me.value) return;
  await runAction(
    `mutation($object: club_join_requests_insert_input!) {
      insert_club_join_requests_one(object: $object) { id }
    }`,
    {
      object: {
        club_id: club.value.id,
        message: joinMessage.value.trim(),
      },
    },
  );
}

async function leaveClub() {
  if (!me.value) return;
  await runAction(
    `mutation($club_id: uuid!) { leaveClub(club_id: $club_id) { success } }`,
    { club_id: club.value.id },
  );
}

async function transferClub() {
  const sid = transferSteamId.value.trim();
  if (!sid) return;
  await runAction(
    `mutation($club_id: uuid!, $steam_id: String!) {
      transferClub(club_id: $club_id, steam_id: $steam_id) { success }
    }`,
    { club_id: club.value.id, steam_id: sid },
  );
  transferSteamId.value = "";
}

async function setPrimary() {
  await runAction(
    `mutation($club_id: uuid!) { setPrimaryClub(club_id: $club_id) { success } }`,
    { club_id: club.value.id },
  );
}

async function sendInvite() {
  const sid = inviteSteamId.value.trim();
  if (!sid) return;
  await runAction(
    `mutation($object: club_invites_insert_input!) {
      insert_club_invites_one(object: $object) { id }
    }`,
    { object: { club_id: club.value.id, steam_id: sid } },
  );
  inviteSteamId.value = "";
}

async function createLink() {
  busy.value = true;
  try {
    const { data } = await client.mutate({
      mutation: gql`
        mutation(
          $club_id: uuid!
          $max_uses: Int
          $expires_hours: Int
          $bypass_requirements: Boolean
        ) {
          createClubInviteLink(
            club_id: $club_id
            max_uses: $max_uses
            expires_hours: $expires_hours
            bypass_requirements: $bypass_requirements
          ) {
            success
            code
          }
        }
      `,
      variables: {
        club_id: club.value.id,
        max_uses: linkMaxUses.value,
        expires_hours: linkHours.value,
        bypass_requirements: linkBypass.value,
      },
    });
    createdLinkCode.value = data?.createClubInviteLink?.code || "";
    await load();
  } catch (e: any) {
    error.value = e?.message || t("pages.clubs.action_failed");
  } finally {
    busy.value = false;
  }
}

async function reviewRequest(id: string, accept: boolean) {
  await runAction(
    `mutation($request_id: uuid!, $accept: Boolean!) {
      reviewClubJoinRequest(request_id: $request_id, accept: $accept) { success }
    }`,
    { request_id: id, accept },
  );
}

async function banMember(steamId: string) {
  await runAction(
    `mutation($object: club_bans_insert_input!) {
      insert_club_bans_one(object: $object) { id }
    }`,
    {
      object: {
        club_id: club.value.id,
        steam_id: steamId,
        reason: "Banned by admin",
      },
    },
  );
  await runAction(
    `mutation($club_id: uuid!, $steam_id: bigint!) {
      delete_club_members_by_pk(club_id: $club_id, steam_id: $steam_id) { club_id }
    }`,
    { club_id: club.value.id, steam_id: steamId },
  );
}

async function saveRequirements() {
  await runAction(
    `mutation($id: uuid!, $_set: clubs_set_input!) {
      update_clubs_by_pk(pk_columns: { id: $id }, _set: $_set) { id }
    }`,
    {
      id: club.value.id,
      _set: {
        min_elo: minElo.value,
        min_faceit_level: minLevel.value,
      },
    },
  );
}

async function saveSocials() {
  const telegram = parseTelegramUrl(telegramInput.value);
  const instagram = parseInstagramUrl(instagramInput.value);
  if (!telegram.ok) {
    error.value = t("pages.clubs.invalid_telegram");
    return;
  }
  if (!instagram.ok) {
    error.value = t("pages.clubs.invalid_instagram");
    return;
  }
  await runAction(
    `mutation($id: uuid!, $_set: clubs_set_input!) {
      update_clubs_by_pk(pk_columns: { id: $id }, _set: $_set) {
        id
        telegram_url
        instagram_url
      }
    }`,
    {
      id: club.value.id,
      _set: {
        telegram_url: telegram.url,
        instagram_url: instagram.url,
      },
    },
  );
}

async function sendChat() {
  if (!activeChannel.value || !chatBody.value.trim() || !me.value) return;
  await runAction(
    `mutation($object: club_messages_insert_input!) {
      insert_club_messages_one(object: $object) { id }
    }`,
    {
      object: {
        channel_id: activeChannel.value.id,
        club_id: club.value.id,
        body: chatBody.value.trim(),
      },
    },
  );
  chatBody.value = "";
}

async function deleteMessage(id: string) {
  await runAction(
    `mutation($id: uuid!, $deleted_at: timestamptz!) {
      update_club_messages_by_pk(pk_columns: { id: $id }, _set: { deleted_at: $deleted_at }) { id }
    }`,
    { id, deleted_at: new Date().toISOString() },
  );
}

async function createSeasonLb() {
  await runAction(
    `mutation($object: club_leaderboards_insert_input!) {
      insert_club_leaderboards_one(object: $object) { id }
    }`,
    {
      object: {
        club_id: club.value.id,
        name: seasonLbName.value.trim() || "Season",
        type: "season",
        points_win: 25,
        points_loss: -15,
        starting_points: 1000,
        starts_at: new Date().toISOString(),
      },
    },
  );
}

async function freezeLb(id: string, frozen: boolean) {
  await runAction(
    `mutation($id: uuid!, $frozen: Boolean!) {
      update_club_leaderboards_by_pk(pk_columns: { id: $id }, _set: { frozen: $frozen }) { id }
    }`,
    { id, frozen },
  );
}

async function createQueue() {
  await runAction(
    `mutation($object: club_queues_insert_input!) {
      insert_club_queues_one(object: $object) { id }
    }`,
    {
      object: {
        club_id: club.value.id,
        name: newQueueName.value.trim() || "In-house",
        team_size: 5,
        min_players: 10,
        enabled: true,
      },
    },
  );
}

async function joinQueue(queueId: string) {
  await runAction(
    `mutation($queue_id: uuid!) { joinClubQueue(queue_id: $queue_id) { success queued } }`,
    { queue_id: queueId },
  );
}

async function leaveQueue(queueId: string) {
  await runAction(
    `mutation($queue_id: uuid!) { leaveClubQueue(queue_id: $queue_id) { success } }`,
    { queue_id: queueId },
  );
}

async function createParty() {
  if (!me.value) return;
  await runAction(
    `mutation($object: club_parties_insert_input!) {
      insert_club_parties_one(object: $object) { id }
    }`,
    {
      object: {
        club_id: club.value.id,
        title: newPartyTitle.value.trim() || "Looking for players",
        listed: true,
        members: { data: [{ is_guest: false }] },
      },
    },
  );
  newPartyTitle.value = "";
}

async function joinParty(partyId: string, asGuest = false) {
  if (!me.value) return;
  await runAction(
    `mutation($object: club_party_members_insert_input!) {
      insert_club_party_members_one(object: $object) { party_id }
    }`,
    {
      object: {
        party_id: partyId,
        is_guest: asGuest,
      },
    },
  );
}

async function deleteParty(partyId: string) {
  await runAction(
    `mutation($id: uuid!) {
      delete_club_parties_by_pk(id: $id) { id }
    }`,
    { id: partyId },
  );
}

function canDeleteParty(party: { leader_steam_id?: string | number }) {
  if (canManage.value) return true;
  return String(party.leader_steam_id) === String(me.value?.steam_id);
}

async function leaveParty(partyId: string) {
  if (!me.value) return;
  await runAction(
    `mutation($party_id: uuid!, $steam_id: bigint!) {
      delete_club_party_members_by_pk(party_id: $party_id, steam_id: $steam_id) { party_id }
    }`,
    { party_id: partyId, steam_id: String(me.value.steam_id) },
  );
}

async function addGuest(partyId: string) {
  const sid = guestSteamId.value.trim();
  if (!sid) return;
  await runAction(
    `mutation($party_id: uuid!, $steam_id: String!) {
      addClubPartyGuest(party_id: $party_id, steam_id: $steam_id) { success }
    }`,
    { party_id: partyId, steam_id: sid },
  );
  guestSteamId.value = "";
}

async function queuePartyToLobby(party: any) {
  const mm = useMatchmakingStore();
  const lobbyId = await mm.createLobby();
  if (!lobbyId) return;
  for (const m of party.members || []) {
    if (String(m.steam_id) === String(me.value?.steam_id)) continue;
    try {
      await mm.inviteToLobby(String(m.steam_id));
    } catch (e) {
      console.warn(e);
    }
  }
}

async function registerClash(eventId: string) {
  await runAction(
    `mutation($event_id: uuid!, $club_id: uuid!) {
      registerClubClash(event_id: $event_id, club_id: $club_id) { success }
    }`,
    { event_id: eventId, club_id: club.value.id },
  );
}

const officialTeam = ref<any>(null);
const memberTeams = ref<any[]>([]);
const clubTournaments = ref<any[]>([]);
const clashEvents = ref<any[]>([]);
async function loadTeams() {
  if (!club.value) return;
  try {
    const { data } = await client.query({
      query: gql`
        query ClubOfficialTeam($id: uuid!) {
          clubs_by_pk(id: $id) {
            official_team_id
            official_team {
              id
              name
              short_name
              avatar_url
              owner_steam_id
            }
          }
        }
      `,
      variables: { id: club.value.id },
      fetchPolicy: "network-only",
      context: { optional: true },
    });
    officialTeam.value = data?.clubs_by_pk?.official_team ?? null;
  } catch {
    officialTeam.value = null;
  }
  const steamIds = (club.value.members ?? []).map((m: any) => m.steam_id);
  if (!steamIds.length) {
    memberTeams.value = [];
    return;
  }
  try {
    const { data } = await client.query({
      query: gql`
        query ClubMemberTeams($ids: [bigint!]!) {
          teams(
            where: { owner_steam_id: { _in: $ids } }
            order_by: [{ name: asc }]
          ) {
            id
            name
            short_name
            avatar_url
            owner_steam_id
            owner {
              name
            }
          }
        }
      `,
      variables: { ids: steamIds },
      fetchPolicy: "network-only",
    });
    const officialId = officialTeam.value?.id;
    memberTeams.value = (data?.teams ?? []).filter(
      (row: any) => row.id !== officialId,
    );
  } catch {
    memberTeams.value = [];
  }
}

async function loadTournaments() {
  if (!club.value) return;
  try {
    const { data } = await client.query({
      query: generateQuery({
        tournaments: [
          {
            where: { club_id: { _eq: $("clubId", "uuid!") } },
            order_by: [{ start: order_by.desc }],
            limit: 40,
          },
          tournamentCardFields,
        ],
      } as any),
      variables: { clubId: club.value.id },
      fetchPolicy: "network-only",
      context: { optional: true },
    });
    clubTournaments.value = data?.tournaments ?? [];
  } catch {
    clubTournaments.value = [];
  }
}

async function createOfficialTeam() {
  if (!club.value) return;
  await runAction(
    `mutation($club_id: uuid!) {
      createClubOfficialTeam(club_id: $club_id) { success team_id }
    }`,
    { club_id: club.value.id },
  );
  await loadTeams();
}

async function loadClash() {
  try {
    const { data } = await client.query({
      query: gql`
        query ClubClashEvents {
          club_clash_events(order_by: [{ starts_at: desc }], limit: 20) {
            id
            name
            starts_at
            ends_at
            locked
            entries(order_by: [{ points: desc }]) {
              club_id
              points
              club {
                name
                slug
              }
            }
          }
        }
      `,
      fetchPolicy: "network-only",
    });
    clashEvents.value = data?.club_clash_events ?? [];
  } catch {
    clashEvents.value = [];
  }
}

watch(tab, (v) => {
  if (v === "clash") void loadClash();
  if (v === "teams") void loadTeams();
  if (v === "tournaments") void loadTournaments();
});

const tabs = computed(() => {
  const base: { id: Tab; label: string; icon: any }[] = [
    { id: "overview", label: t("pages.clubs.tabs.overview"), icon: Newspaper },
    { id: "members", label: t("pages.clubs.tabs.members"), icon: Users },
    { id: "chat", label: t("pages.clubs.tabs.chat"), icon: MessageSquare },
    {
      id: "leaderboard",
      label: t("pages.clubs.tabs.leaderboard"),
      icon: ListOrdered,
    },
    { id: "queues", label: t("pages.clubs.tabs.queues"), icon: Swords },
    { id: "parties", label: t("pages.clubs.tabs.parties"), icon: PartyPopper },
    { id: "teams", label: t("pages.clubs.tabs.teams"), icon: Shield },
    {
      id: "tournaments",
      label: t("pages.clubs.tabs.tournaments"),
      icon: CalendarDays,
    },
    { id: "clash", label: t("pages.clubs.tabs.clash"), icon: Trophy },
  ];
  if (canManage.value) {
    base.push({
      id: "admin",
      label: t("pages.clubs.tabs.admin"),
      icon: Settings,
    });
  }
  return base;
});

const filteredMembers = computed(() => {
  const list = club.value?.members ?? [];
  const q = memberQuery.value.trim().toLowerCase();
  if (!q) return list;
  return list.filter((m: any) =>
    String(m.player?.name || m.steam_id).toLowerCase().includes(q),
  );
});

function roleLabel(role: string) {
  const key = `pages.clubs.roles.${role}`;
  const out = t(key);
  return out === key ? role : String(out);
}

function roleChipClass(role: string) {
  if (role === "owner")
    return "bg-[hsl(var(--tac-amber)/0.18)] text-[hsl(var(--tac-amber))]";
  if (role === "admin") return "bg-orange-500/15 text-orange-300";
  if (role === "moderator") return "bg-sky-500/15 text-sky-300";
  return "bg-muted text-muted-foreground";
}

function winRate(wins: number, losses: number) {
  const total = (wins || 0) + (losses || 0);
  if (!total) return "—";
  return `${Math.round(((wins || 0) / total) * 100)}%`;
}

function activityTitle(kind: string) {
  const key = `pages.clubs.activity_kinds.${kind}`;
  const out = t(key);
  return out === key ? String(kind).replace(/_/g, " ") : String(out);
}
</script>

<template>
  <div v-if="loading" class="space-y-4">
    <div class="h-44 animate-pulse rounded-xl bg-muted/40" />
    <div class="grid gap-6 md:grid-cols-[13rem_1fr]">
      <div class="hidden h-64 animate-pulse rounded-xl bg-muted/30 md:block" />
      <div class="h-64 animate-pulse rounded-xl bg-muted/30" />
    </div>
  </div>
  <div v-else-if="!club" class="rounded-xl border border-border bg-card/40 p-8 text-sm text-destructive">
    {{ error || t("pages.clubs.not_found") }}
  </div>
  <div v-else class="pb-8">
    <PageTransition>
      <header class="overflow-hidden rounded-xl border border-border/70 bg-card/30">
        <div class="relative">
          <ImageUploadTile
            v-if="canManage"
            class="[&>div]:rounded-none [&>div]:border-0"
            :aspect="8 / 3"
            fit="cover"
            filename="banner.webp"
            :crop-output="{ w: 1600, h: 600 }"
            crop
            :hint="t('pages.clubs.banner_hint')"
            :upload-url="bannerUploadUrl"
            :delete-url="bannerUploadUrl"
            :has-custom="!!club.cover_url"
            :current-src="coverSrc"
            @uploaded="onCoverUploaded"
            @removed="onCoverRemoved"
          />
          <div
            v-else
            class="h-36 w-full sm:h-44"
            :style="
              coverSrc
                ? {
                    backgroundImage: `linear-gradient(180deg, rgba(8,8,10,0.1) 0%, rgba(8,8,10,0.78) 100%), url(${coverSrc})`,
                    backgroundSize: 'cover',
                    backgroundPosition: 'center',
                  }
                : undefined
            "
            :class="
              coverSrc
                ? ''
                : 'bg-[radial-gradient(ellipse_at_top_left,hsl(var(--tac-amber)/0.32),transparent_52%),linear-gradient(180deg,#1c1c20_0%,#0b0b0d_100%)]'
            "
          />
        </div>
        <div class="relative flex flex-wrap items-end gap-4 px-4 pb-4 sm:px-6 sm:pb-5">
          <div class="-mt-12 sm:-mt-14">
            <ImageUploadTile
              v-if="canManage"
              class="h-[7.25rem] w-[7.25rem] overflow-hidden rounded-full [&>div]:rounded-full [&>div]:border-2 [&>div]:border-[hsl(var(--tac-amber)/0.5)]"
              aspect="square"
              fit="cover"
              filename="profile.webp"
              :crop-output="{ w: 512, h: 512 }"
              crop
              :upload-url="avatarUploadUrl"
              :delete-url="avatarUploadUrl"
              :has-custom="!!club.avatar_url"
              :current-src="avatarSrc"
              @uploaded="onAvatarUploaded"
              @removed="onAvatarRemoved"
            />
            <ClubAvatar
              v-else
              :name="club.name"
              :short-name="club.short_name"
              :src="avatarSrc"
              size="xl"
            />
          </div>
          <div class="min-w-0 flex-1 py-1">
            <div class="flex flex-wrap items-center gap-2">
              <h1 class="truncate font-sans text-[1.65rem] font-bold leading-none tracking-tight sm:text-3xl">
                {{ club.name }}
              </h1>
              <span
                v-if="club.short_name"
                class="rounded bg-[hsl(var(--tac-amber)/0.14)] px-1.5 py-0.5 font-mono text-[0.65rem] font-bold uppercase tracking-wider text-[hsl(var(--tac-amber))]"
              >
                {{ club.short_name }}
              </span>
              <span
                v-if="myMembership?.is_primary"
                class="rounded-full bg-[hsl(var(--tac-amber)/0.16)] px-2 py-0.5 text-[0.65rem] font-medium text-[hsl(var(--tac-amber))]"
              >
                {{ t("pages.clubs.representing") }}
              </span>
            </div>
            <p class="mt-2 max-w-2xl text-sm text-muted-foreground line-clamp-2">
              {{ club.description || t("pages.clubs.no_description") }}
            </p>
            <ClubSocialLinks
              class="mt-2"
              :telegram="club.telegram_url"
              :instagram="club.instagram_url"
            />
            <div class="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
              <span>{{ club.members_aggregate?.aggregate?.count ?? 0 }} {{ t("pages.clubs.members_label") }}</span>
              <span class="text-border">·</span>
              <span>{{ t(`pages.clubs.join_modes.${club.join_mode}`) }}</span>
              <template v-if="club.min_elo">
                <span class="text-border">·</span>
                <span>Elo ≥ {{ club.min_elo }}</span>
              </template>
            </div>
          </div>
          <div class="flex flex-wrap items-center gap-2 pb-1">
            <Button
              v-if="!isMember && club.join_mode === 'open' && me"
              :disabled="busy"
              :class="tacticalCtaButtonClasses"
              @click="joinOpen"
            >
              {{ t("pages.clubs.join") }}
            </Button>
            <div
              v-else-if="!isMember && club.join_mode === 'request' && me"
              class="flex gap-2"
            >
              <Input v-model="joinMessage" :placeholder="t('pages.clubs.request_message')" class="w-40" />
              <Button :disabled="busy" :class="tacticalCtaButtonClasses" @click="requestJoin">
                {{ t("pages.clubs.request_join") }}
              </Button>
            </div>
            <Button
              v-if="isMember && !myMembership?.is_primary"
              variant="outline"
              :disabled="busy"
              @click="setPrimary"
            >
              {{ t("pages.clubs.set_primary") }}
            </Button>
            <Button
              v-if="isOwner"
              :class="tacticalCtaButtonClasses"
              @click="$router.push(`/tournaments/create?club=${club.id}`)"
            >
              {{ t("pages.clubs.create_tournament") }}
            </Button>
            <Button
              v-if="isMember && myMembership?.role !== 'owner'"
              variant="ghost"
              :disabled="busy"
              @click="leaveClub"
            >
              {{ t("pages.clubs.leave") }}
            </Button>
          </div>
        </div>
      </header>
    </PageTransition>

    <p v-if="error" class="mt-3 text-sm text-destructive">{{ error }}</p>

    <div class="mt-6 grid items-start gap-6 md:grid-cols-[13.5rem_minmax(0,1fr)]">
      <nav
        class="flex gap-1 overflow-x-auto pb-1 md:sticky md:top-4 md:flex-col md:overflow-visible md:rounded-xl md:border md:border-border/70 md:bg-card/40 md:p-2"
      >
        <button
          v-for="item in tabs"
          :key="item.id"
          type="button"
          class="inline-flex shrink-0 items-center gap-2 rounded-md px-3 py-2 text-sm font-medium transition"
          :class="
            tab === item.id
              ? 'bg-[hsl(var(--tac-amber)/0.12)] text-foreground md:border-l-2 md:border-[hsl(var(--tac-amber))] md:pl-[10px]'
              : 'text-muted-foreground hover:bg-muted/40 hover:text-foreground'
          "
          @click="tab = item.id"
        >
          <component :is="item.icon" class="h-4 w-4 shrink-0" />
          {{ item.label }}
        </button>
      </nav>

      <div class="min-w-0">
        <!-- Feed -->
        <section v-if="tab === 'overview'" class="grid gap-6 lg:grid-cols-[1fr_16rem]">
          <div class="rounded-xl border border-border/70 bg-card/40 p-4 sm:p-5">
            <h2 class="mb-4 text-sm font-semibold">{{ t("pages.clubs.activity") }}</h2>
            <ul class="space-y-3">
              <li
                v-for="a in club.activity"
                :key="a.id"
                class="rounded-lg border border-border/50 bg-background/40 px-3 py-2.5"
              >
                <div class="text-sm font-medium capitalize">{{ activityTitle(a.kind) }}</div>
                <div class="mt-0.5 text-xs text-muted-foreground">
                  {{ new Date(a.created_at).toLocaleString() }}
                </div>
              </li>
              <li v-if="!club.activity?.length" class="py-8 text-center text-sm text-muted-foreground">
                {{ t("pages.clubs.no_activity") }}
              </li>
            </ul>
          </div>
          <aside class="space-y-3">
            <div class="rounded-xl border border-border/70 bg-card/40 p-4">
              <div class="text-xs text-muted-foreground">{{ t("pages.clubs.members_label") }}</div>
              <div class="mt-1 text-2xl font-bold tabular-nums">
                {{ club.members_aggregate?.aggregate?.count ?? 0 }}
              </div>
            </div>
            <div class="rounded-xl border border-border/70 bg-card/40 p-4">
              <div class="text-xs text-muted-foreground">{{ t("pages.clubs.channels") }}</div>
              <div class="mt-1 text-2xl font-bold tabular-nums">{{ club.channels?.length ?? 0 }}</div>
            </div>
          </aside>
        </section>

        <!-- Members -->
        <section v-else-if="tab === 'members'" class="rounded-xl border border-border/70 bg-card/40 p-4 sm:p-5">
          <div class="relative mb-4 max-w-sm">
            <Search class="pointer-events-none absolute left-3 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
            <input
              v-model="memberQuery"
              type="search"
              :placeholder="t('pages.clubs.search_members')"
              class="h-9 w-full rounded-lg border border-border bg-background/50 pl-9 pr-3 text-sm outline-none focus:border-[hsl(var(--tac-amber)/0.5)]"
            />
          </div>
          <div class="divide-y divide-border/50">
            <div
              v-for="m in filteredMembers"
              :key="m.steam_id"
              class="flex items-center gap-3 py-2.5"
            >
              <PlayerDisplay
                :player="m.player || { steam_id: m.steam_id, name: String(m.steam_id) }"
                size="xs"
                compact
                linkable
                :show-flag="false"
                :show-role="false"
                match-type="Faceit"
              />
              <span
                class="ml-auto shrink-0 rounded-full px-2 py-0.5 text-[0.65rem] font-medium uppercase tracking-wide"
                :class="roleChipClass(m.role)"
              >
                {{ roleLabel(m.role) }}
              </span>
              <Button
                v-if="canManage && m.role !== 'owner'"
                size="sm"
                variant="ghost"
                class="text-destructive"
                @click="banMember(String(m.steam_id))"
              >
                {{ t("pages.clubs.ban") }}
              </Button>
            </div>
          </div>
        </section>

        <!-- Chat -->
        <section v-else-if="tab === 'chat'" class="grid gap-3 md:grid-cols-[11rem_minmax(0,1fr)]">
          <div class="flex gap-1 overflow-x-auto md:flex-col md:rounded-xl md:border md:border-border/70 md:bg-card/40 md:p-2">
            <button
              v-for="ch in club.channels"
              :key="ch.id"
              type="button"
              class="shrink-0 rounded-md px-3 py-1.5 text-left text-sm"
              :class="
                activeChannel?.id === ch.id
                  ? 'bg-[hsl(var(--tac-amber)/0.12)] text-[hsl(var(--tac-amber))]'
                  : 'text-muted-foreground hover:bg-muted/40 hover:text-foreground'
              "
              @click="activeChannelId = ch.id"
            >
              #{{ ch.name }}
            </button>
          </div>
          <div
            v-if="isMember"
            class="flex min-h-[380px] flex-col overflow-hidden rounded-xl border border-border/70 bg-card/40"
          >
            <div class="border-b border-border/60 px-4 py-2 text-sm font-medium">
              #{{ activeChannel?.name }}
            </div>
            <div class="flex-1 space-y-3 overflow-y-auto p-4">
              <div
                v-for="msg in [...(activeChannel?.messages || [])].reverse()"
                :key="msg.id"
                class="text-sm"
                :class="{ 'opacity-40 line-through': msg.deleted_at }"
              >
                <span class="font-semibold text-[hsl(var(--tac-amber))]">
                  {{ msg.author?.name || msg.author_steam_id }}
                </span>
                <span class="ml-2 text-foreground/90">{{ msg.body }}</span>
                <button
                  v-if="canModerate && !msg.deleted_at"
                  type="button"
                  class="ml-2 text-xs text-destructive"
                  @click="deleteMessage(msg.id)"
                >
                  {{ t("common.delete") }}
                </button>
              </div>
            </div>
            <form class="flex gap-2 border-t border-border/60 p-3" @submit.prevent="sendChat">
              <Input v-model="chatBody" class="flex-1" :placeholder="t('pages.clubs.chat_placeholder')" />
              <Button type="submit" :disabled="busy" :class="tacticalCtaButtonClasses">
                {{ t("pages.clubs.send") }}
              </Button>
            </form>
          </div>
          <p v-else class="rounded-xl border border-border/70 bg-card/40 p-6 text-sm text-muted-foreground">
            {{ t("pages.clubs.chat_members_only") }}
          </p>
        </section>

        <!-- Leaderboard -->
        <section v-else-if="tab === 'leaderboard'" class="space-y-5">
          <div v-if="canManage" class="flex flex-wrap items-end gap-2">
            <Input v-model="seasonLbName" class="w-48" />
            <Button :disabled="busy" @click="createSeasonLb">
              {{ t("pages.clubs.create_season_lb") }}
            </Button>
          </div>
          <div
            v-for="lb in club.leaderboards"
            :key="lb.id"
            class="overflow-hidden rounded-xl border border-border/70 bg-card/40"
          >
            <div class="flex items-center justify-between gap-3 border-b border-border/60 px-4 py-3">
              <div>
                <h3 class="font-semibold">{{ lb.name }}</h3>
                <p class="text-xs text-muted-foreground">
                  {{ lb.type }} · +{{ lb.points_win }} / {{ lb.points_loss }}
                  <span v-if="lb.frozen"> · {{ t("pages.clubs.frozen") }}</span>
                </p>
              </div>
              <Button
                v-if="canManage"
                size="sm"
                variant="outline"
                @click="freezeLb(lb.id, !lb.frozen)"
              >
                {{ lb.frozen ? t("pages.clubs.unfreeze") : t("pages.clubs.freeze") }}
              </Button>
            </div>
            <table class="w-full text-sm">
              <thead class="text-left text-[0.7rem] uppercase tracking-wider text-muted-foreground">
                <tr class="border-b border-border/50">
                  <th class="px-4 py-2 w-10">#</th>
                  <th>{{ t("pages.clubs.player") }}</th>
                  <th class="text-right">{{ t("pages.clubs.points") }}</th>
                  <th class="text-right">W/L</th>
                  <th class="px-4 text-right">{{ t("pages.clubs.winrate") }}</th>
                </tr>
              </thead>
              <tbody>
                <tr
                  v-for="(e, i) in lb.entries"
                  :key="e.steam_id"
                  class="border-t border-border/40"
                  :class="i < 3 ? 'bg-[hsl(var(--tac-amber)/0.04)]' : ''"
                >
                  <td class="px-4 py-2.5 font-mono tabular-nums" :class="i === 0 ? 'text-[hsl(var(--tac-amber))]' : ''">
                    {{ i + 1 }}
                  </td>
                  <td class="py-1.5">
                    <PlayerDisplay
                      :player="e.player || { steam_id: e.steam_id }"
                      size="xs"
                      compact
                      linkable
                      :show-flag="false"
                      :show-role="false"
                      :show-elo="false"
                    />
                  </td>
                  <td class="text-right font-semibold tabular-nums">{{ e.points }}</td>
                  <td class="text-right tabular-nums text-muted-foreground">{{ e.wins }}/{{ e.losses }}</td>
                  <td class="px-4 text-right tabular-nums text-muted-foreground">{{ winRate(e.wins, e.losses) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Queues -->
        <section v-else-if="tab === 'queues'" class="space-y-4">
          <div v-if="canManage" class="flex gap-2">
            <Input v-model="newQueueName" class="max-w-xs" />
            <Button :disabled="busy" @click="createQueue">
              {{ t("pages.clubs.create_queue") }}
            </Button>
          </div>
          <div
            v-for="q in club.queues"
            :key="q.id"
            class="flex flex-wrap items-center justify-between gap-4 overflow-hidden rounded-xl border border-border/70 bg-card/40 p-4"
          >
            <div class="min-w-0">
              <h3 class="font-semibold">{{ q.name }}</h3>
              <p class="mt-1 text-sm text-muted-foreground">
                {{ t("pages.clubs.queued_count", { n: q.players?.length || 0 }) }}
                · {{ q.team_size }}v{{ q.team_size }}
                · {{ q.players?.length || 0 }}/{{ q.min_players }}
              </p>
              <div class="mt-2 h-1.5 w-48 overflow-hidden rounded-full bg-muted">
                <div
                  class="h-full rounded-full bg-[hsl(var(--tac-amber))]"
                  :style="{
                    width: `${Math.min(100, ((q.players?.length || 0) / (q.min_players || 1)) * 100)}%`,
                  }"
                />
              </div>
            </div>
            <div v-if="isMember" class="flex gap-2">
              <Button size="sm" :class="tacticalCtaButtonClasses" :disabled="busy" @click="joinQueue(q.id)">
                {{ t("pages.clubs.join_queue") }}
              </Button>
              <Button size="sm" variant="outline" :disabled="busy" @click="leaveQueue(q.id)">
                {{ t("pages.clubs.leave_queue") }}
              </Button>
            </div>
          </div>
          <p
            v-if="!club.queues?.length"
            class="rounded-xl border border-border/70 bg-card/40 p-8 text-center text-sm text-muted-foreground"
          >
            {{ t("pages.clubs.no_queues") }}
          </p>
        </section>

        <!-- Parties -->
        <section v-else-if="tab === 'parties'" class="space-y-4">
          <div v-if="isMember" class="flex gap-2">
            <Input v-model="newPartyTitle" :placeholder="t('pages.clubs.party_title')" class="max-w-xs" />
            <Button :disabled="busy" :class="tacticalCtaButtonClasses" @click="createParty">
              {{ t("pages.clubs.create_party") }}
            </Button>
          </div>
          <div
            v-for="p in club.parties"
            :key="p.id"
            class="rounded-xl border border-border/70 bg-card/40 p-4"
          >
            <div class="flex flex-wrap items-center justify-between gap-3">
              <div>
                <h3 class="font-semibold">{{ p.title || t("pages.clubs.party") }}</h3>
                <div class="mt-2 flex -space-x-2">
                  <div
                    v-for="i in 5"
                    :key="i"
                    class="flex h-8 w-8 items-center justify-center overflow-hidden rounded-full border-2 border-background bg-muted text-[0.6rem] text-muted-foreground"
                  >
                    <img
                      v-if="p.members?.[i - 1]?.player?.avatar_url"
                      :src="p.members[i - 1].player.avatar_url"
                      alt=""
                      class="h-full w-full object-cover"
                    />
                    <span v-else-if="p.members?.[i - 1]">
                      {{ (p.members[i - 1].player?.name || "?").slice(0, 1) }}
                    </span>
                  </div>
                </div>
              </div>
              <div class="flex flex-wrap gap-2">
                <Button size="sm" variant="outline" @click="joinParty(p.id, !isMember)">
                  {{ t("pages.clubs.join_party") }}
                </Button>
                <Button size="sm" variant="ghost" @click="leaveParty(p.id)">
                  {{ t("pages.clubs.leave_party") }}
                </Button>
                <Button
                  v-if="String(p.leader_steam_id) === String(me?.steam_id)"
                  size="sm"
                  :class="tacticalCtaButtonClasses"
                  @click="queuePartyToLobby(p)"
                >
                  {{ t("pages.clubs.queue_party") }}
                </Button>
                <Button
                  v-if="canDeleteParty(p)"
                  size="sm"
                  variant="ghost"
                  class="text-destructive"
                  :disabled="busy"
                  @click="deleteParty(p.id)"
                >
                  {{ t("pages.clubs.delete_party") }}
                </Button>
              </div>
            </div>
            <div
              v-if="String(p.leader_steam_id) === String(me?.steam_id)"
              class="mt-3 flex gap-2"
            >
              <Input v-model="guestSteamId" :placeholder="t('pages.clubs.guest_steam')" class="max-w-xs" />
              <Button size="sm" variant="outline" @click="addGuest(p.id)">
                {{ t("pages.clubs.add_guest") }}
              </Button>
            </div>
          </div>
        </section>

        <!-- Teams -->
        <section v-else-if="tab === 'teams'" class="space-y-5">
          <div
            class="relative overflow-hidden rounded-2xl border-2 border-[hsl(var(--tac-amber)/0.55)] bg-[linear-gradient(135deg,hsl(var(--tac-amber)/0.22)_0%,hsl(var(--card)/0.85)_42%,hsl(var(--tac-amber)/0.08)_100%)] p-4 sm:p-5"
          >
            <img
              v-if="coverSrc"
              :src="coverSrc"
              alt=""
              class="pointer-events-none absolute inset-0 h-full w-full object-cover opacity-20"
            />
            <div class="relative flex flex-wrap items-start justify-between gap-3">
              <div class="min-w-0">
                <p class="font-mono text-[0.6rem] font-bold uppercase tracking-[0.22em] text-[hsl(var(--tac-amber))]">
                  {{ t("pages.clubs.official_identity_label") }}
                </p>
                <h2 class="mt-1 text-base font-semibold">{{ t("pages.clubs.official_team") }}</h2>
                <p class="mt-1 max-w-xl text-xs text-muted-foreground">
                  {{ t("pages.clubs.official_team_hint") }}
                </p>
              </div>
              <Button
                v-if="isOwner && !officialTeam"
                size="sm"
                :class="tacticalCtaButtonClasses"
                :disabled="busy"
                @click="createOfficialTeam"
              >
                {{ t("pages.clubs.create_official_team") }}
              </Button>
            </div>
            <NuxtLink
              v-if="officialTeam"
              :to="`/teams/${officialTeam.id}`"
              class="relative mt-4 flex items-center gap-3 rounded-xl border border-[hsl(var(--tac-amber)/0.45)] bg-black/25 px-3 py-3 hover:border-[hsl(var(--tac-amber)/0.75)]"
            >
              <ClubAvatar
                :name="club.name"
                :short-name="club.short_name"
                :src="club.avatar_url"
                size="md"
              />
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-2">
                  <span class="font-semibold">{{ officialTeam.name }}</span>
                  <span
                    v-if="officialTeam.short_name"
                    class="rounded bg-[hsl(var(--tac-amber)/0.2)] px-1.5 py-0.5 font-mono text-[0.65rem] font-bold uppercase tracking-wider text-[hsl(var(--tac-amber))]"
                  >
                    {{ officialTeam.short_name }}
                  </span>
                </div>
                <p class="mt-0.5 text-xs text-[hsl(var(--tac-amber))]">
                  {{ t("pages.clubs.official_identity_of", { club: club.name }) }}
                </p>
              </div>
              <span class="text-xs text-muted-foreground">{{ t("pages.clubs.open_team") }}</span>
            </NuxtLink>
          </div>

          <div>
            <h2 class="text-sm font-semibold">{{ t("pages.clubs.member_teams") }}</h2>
            <p class="mt-1 text-xs text-muted-foreground">
              {{ t("pages.clubs.member_teams_hint") }}
            </p>
            <div class="mt-3 grid gap-2">
              <NuxtLink
                v-for="team in memberTeams"
                :key="team.id"
                :to="`/teams/${team.id}`"
                class="flex items-center justify-between gap-3 rounded-xl border border-border/70 bg-card/40 px-3 py-2.5 hover:border-[hsl(var(--tac-amber)/0.4)]"
              >
                <div class="min-w-0">
                  <div class="flex flex-wrap items-center gap-2">
                    <span class="font-medium">{{ team.name }}</span>
                    <span
                      v-if="team.short_name"
                      class="rounded bg-muted px-1.5 py-0.5 font-mono text-[0.65rem] font-bold uppercase tracking-wider text-muted-foreground"
                    >
                      {{ team.short_name }}
                    </span>
                    <ClubTeamBadge :clubs="clubAsMemberClub" />
                  </div>
                  <div class="mt-0.5 text-xs text-muted-foreground">
                    {{ t("pages.clubs.team_owner") }}:
                    {{ team.owner?.name || team.owner_steam_id }}
                  </div>
                </div>
              </NuxtLink>
              <p
                v-if="!memberTeams.length"
                class="py-8 text-center text-sm text-muted-foreground"
              >
                {{ t("pages.clubs.no_member_teams") }}
              </p>
            </div>
          </div>
        </section>

        <!-- Club tournaments -->
        <section v-else-if="tab === 'tournaments'" class="space-y-4">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <p class="text-sm text-muted-foreground">
              {{ t("pages.clubs.tournaments_hint") }}
            </p>
            <Button
              v-if="isOwner"
              size="sm"
              :class="tacticalCtaButtonClasses"
              @click="$router.push(`/tournaments/create?club=${club.id}`)"
            >
              {{ t("pages.clubs.create_tournament") }}
            </Button>
          </div>
          <div class="grid gap-3 sm:grid-cols-2">
            <TournamentCard
              v-for="row in clubTournaments"
              :key="row.id"
              variant="compact"
              :tournament="row"
            />
          </div>
          <p
            v-if="!clubTournaments.length"
            class="py-10 text-center text-sm text-muted-foreground"
          >
            {{ t("pages.clubs.no_tournaments") }}
          </p>
        </section>

        <!-- Clash -->
        <section v-else-if="tab === 'clash'" class="space-y-4">
          <p class="text-sm text-muted-foreground">{{ t("pages.clubs.clash_hint") }}</p>
          <div
            v-for="ev in clashEvents"
            :key="ev.id"
            class="rounded-xl border border-border/70 bg-card/40 p-4"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h3 class="font-semibold">{{ ev.name }}</h3>
                <p class="text-xs text-muted-foreground">
                  {{ new Date(ev.starts_at).toLocaleString() }} →
                  {{ new Date(ev.ends_at).toLocaleString() }}
                </p>
              </div>
              <Button
                size="sm"
                :class="tacticalCtaButtonClasses"
                :disabled="busy || !myMembership?.is_primary"
                @click="registerClash(ev.id)"
              >
                {{ t("pages.clubs.register_clash") }}
              </Button>
            </div>
            <ul class="mt-3 space-y-1 text-sm">
              <li v-for="e in ev.entries" :key="e.club_id">
                <NuxtLink :to="`/clubs/${e.club?.slug || e.club_id}`" class="hover:text-[hsl(var(--tac-amber))]">
                  {{ e.club?.name }}
                </NuxtLink>
                — {{ e.points }} pts
              </li>
            </ul>
          </div>
          <p
            v-if="!clashEvents.length"
            class="rounded-xl border border-border/70 bg-card/40 p-8 text-center text-sm text-muted-foreground"
          >
            {{ t("pages.clubs.no_clash") }}
          </p>
        </section>

        <!-- Admin / Settings -->
        <section v-else-if="tab === 'admin' && canManage" class="grid gap-4 lg:grid-cols-2">
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.profile") }}</h3>
            <p class="text-xs text-muted-foreground">{{ t("pages.clubs.profile_hint") }}</p>
            <ImageUploadTile
              class="max-w-[9rem]"
              aspect="square"
              fit="cover"
              filename="profile.webp"
              :crop-output="{ w: 512, h: 512 }"
              crop
              :upload-url="avatarUploadUrl"
              :delete-url="avatarUploadUrl"
              :has-custom="!!club.avatar_url"
              :current-src="avatarSrc"
              @uploaded="onAvatarUploaded"
              @removed="onAvatarRemoved"
            />
          </div>
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.socials") }}</h3>
            <p class="text-xs text-muted-foreground">{{ t("pages.clubs.socials_hint") }}</p>
            <Input
              v-model="telegramInput"
              :placeholder="t('pages.clubs.telegram_placeholder')"
            />
            <Input
              v-model="instagramInput"
              :placeholder="t('pages.clubs.instagram_placeholder')"
            />
            <Button :disabled="busy" @click="saveSocials">
              {{ t("common.save") }}
            </Button>
          </div>
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4 lg:col-span-2">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.banner") }}</h3>
            <p class="text-xs text-muted-foreground">{{ t("pages.clubs.banner_hint") }}</p>
            <ImageUploadTile
              :aspect="8 / 3"
              fit="cover"
              filename="banner.webp"
              :crop-output="{ w: 1600, h: 600 }"
              crop
              :upload-url="bannerUploadUrl"
              :delete-url="bannerUploadUrl"
              :has-custom="!!club.cover_url"
              :current-src="coverSrc"
              @uploaded="onCoverUploaded"
              @removed="onCoverRemoved"
            />
          </div>
          <div
            v-if="isOwner"
            class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4 lg:col-span-2"
          >
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.transfer") }}</h3>
            <p class="text-xs text-muted-foreground">{{ t("pages.clubs.transfer_hint") }}</p>
            <div class="flex gap-2">
              <Input
                v-model="transferSteamId"
                :placeholder="t('pages.clubs.transfer_to')"
              />
              <Button :disabled="busy" @click="transferClub">
                {{ t("pages.clubs.transfer") }}
              </Button>
            </div>
          </div>
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.invites") }}</h3>
            <div class="flex gap-2">
              <Input v-model="inviteSteamId" :placeholder="t('pages.clubs.steam_id')" />
              <Button :disabled="busy" @click="sendInvite">{{ t("pages.clubs.send_invite") }}</Button>
            </div>
            <ul class="text-sm text-muted-foreground">
              <li v-for="inv in club.invites" :key="inv.id">
                {{ inv.player?.name || inv.steam_id }}
              </li>
            </ul>
          </div>
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.invite_links") }}</h3>
            <div class="flex flex-wrap gap-2">
              <Input v-model.number="linkHours" type="number" class="w-24" :placeholder="'hrs'" />
              <Input v-model.number="linkMaxUses" type="number" class="w-24" :placeholder="'max'" />
              <label class="flex items-center gap-2 text-xs">
                <input v-model="linkBypass" type="checkbox" />
                {{ t("pages.clubs.bypass_req") }}
              </label>
              <Button :disabled="busy" @click="createLink">{{ t("pages.clubs.create_link") }}</Button>
            </div>
            <p v-if="createdLinkCode" class="text-sm">
              {{ t("pages.clubs.link_created") }}:
              <NuxtLink :to="`/clubs/join/${createdLinkCode}`" class="underline">
                /clubs/join/{{ createdLinkCode }}
              </NuxtLink>
            </p>
            <ul class="text-xs text-muted-foreground">
              <li v-for="l in club.invite_links" :key="l.id">
                {{ l.code }} · {{ l.uses }}{{ l.max_uses != null ? `/${l.max_uses}` : "" }}
              </li>
            </ul>
          </div>
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.requests") }}</h3>
            <div
              v-for="r in club.join_requests"
              :key="r.id"
              class="flex items-center gap-2 text-sm"
            >
              <span>{{ r.player?.name }} — {{ r.message }}</span>
              <Button size="sm" @click="reviewRequest(r.id, true)">{{ t("common.accept") }}</Button>
              <Button size="sm" variant="outline" @click="reviewRequest(r.id, false)">
                {{ t("common.decline") }}
              </Button>
            </div>
          </div>
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.requirements") }}</h3>
            <div class="flex flex-wrap gap-2">
              <Input v-model.number="minElo" type="number" :placeholder="'Min Elo'" class="w-32" />
              <Input v-model.number="minLevel" type="number" :placeholder="'Min FACEIT lvl'" class="w-36" />
              <Button :disabled="busy" @click="saveRequirements">{{ t("common.save") }}</Button>
            </div>
            <h3 class="mt-4 text-sm font-semibold">{{ t("pages.clubs.bans") }}</h3>
            <ul class="text-sm text-muted-foreground">
              <li v-for="b in club.bans" :key="b.id">
                {{ b.player?.name || b.steam_id }} — {{ b.reason }}
              </li>
            </ul>
          </div>
        </section>
      </div>
    </div>
  </div>
</template>

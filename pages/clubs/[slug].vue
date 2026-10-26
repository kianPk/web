<script setup lang="ts">
import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import {
  Megaphone,
  Trophy,
  Users,
  ListOrdered,
  Server,
  Settings,
  Newspaper,
  Search,
  Shield,
  CalendarDays,
  Flame,
  Activity,
  Gift,
  BadgePercent,
} from "lucide-vue-next";
import PageTransition from "~/components/ui/transitions/PageTransition.vue";
import { Button } from "~/components/ui/button";
import { Input } from "~/components/ui/input";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "~/components/ui/alert-dialog";
import ClubAvatar from "~/components/clubs/ClubAvatar.vue";
import ClubSocialLinks from "~/components/clubs/ClubSocialLinks.vue";
import ClubTeamBadge from "~/components/clubs/ClubTeamBadge.vue";
import ClubChallengesPanel from "~/components/clubs/ClubChallengesPanel.vue";
import ClubServersPanel from "~/components/clubs/ClubServersPanel.vue";
import ClubLevelSettings from "~/components/clubs/ClubLevelSettings.vue";
import {
  clubLevelLabel,
  DEFAULT_LEVEL_PERMISSIONS,
  type ClubPermKey,
} from "~/utilities/clubLevels";
import TournamentCard from "~/components/tournament/TournamentCard.vue";
import { tournamentCardFields } from "~/graphql/tournamentCardFields";
import { generateQuery } from "~/graphql/graphqlGen";
import { $, order_by } from "~/generated/zeus";
import PlayerDisplay from "~/components/PlayerDisplay.vue";
import ImageUploadTile from "~/components/ImageUploadTile.vue";
import { resolveAvatarUrl } from "~/utilities/avatarUrl";
import { tacticalCtaButtonClasses } from "~/utilities/tacticalClasses";
import { clubErrorMessage } from "~/utilities/clubErrors";
import {
  parseBaleUrl,
  parseInstagramUrl,
  parseRubikaUrl,
  parseSoroushUrl,
  parseTelegramUrl,
} from "~/utilities/clubSocials";
import { formatSiteDate } from "~/utilities/dateLocale";

const VALID_TABS = [
  "overview",
  "feed",
  "members",
  "chat",
  "leaderboard",
  "servers",
  "teams",
  "tournaments",
  "challenges",
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

// Apollo freezes query results, and Vue will not wrap a frozen object, so
// later writes such as social links throw on `id`.
function thaw<T>(value: T): T {
  if (value == null || typeof value !== "object") return value;
  return JSON.parse(JSON.stringify(value)) as T;
}
const loading = ref(true);
const error = ref("");
const socialError = ref("");
const busy = ref(false);
const inviteSteamId = ref("");
const inviteMessage = ref("");
const joinMessage = ref("");
const leaveConfirmOpen = ref(false);
const leaveBlockedUntil = ref<string | null>(null);
const leaveBlockedWhen = computed(() => {
  if (!leaveBlockedUntil.value) return "";
  const parsed = new Date(leaveBlockedUntil.value);
  return Number.isNaN(parsed.getTime())
    ? leaveBlockedUntil.value
    : formatSiteDate(parsed);
});
const chatBody = ref("");
const newsImage = ref<File | null>(null);
const newsPreview = ref<string | null>(null);
const newsFileInput = ref<HTMLInputElement | null>(null);
const activeChannelId = ref<string | null>(null);
const linkMaxUses = ref<number | null>(null);
const linkHours = ref<number | null>(24);
const linkBypass = ref(false);
const seasonLbName = ref("Season");
const minElo = ref<number | null>(null);
const minLevel = ref<number | null>(null);
const createdLinkCode = ref("");
const memberQuery = ref("");
const transferSteamId = ref("");
const telegramInput = ref("");
const instagramInput = ref("");
const baleInput = ref("");
const rubikaInput = ref("");
const soroushInput = ref("");
const descriptionInput = ref("");
const promoInput = ref("");
const benefitsInput = ref("");
const profileError = ref("");
const joinModeInput = ref<"open" | "request" | "invite_only">("open");
const joinModeError = ref("");

const myMembership = computed(() => {
  if (!me.value || !club.value) return null;
  return (
    club.value.members?.find(
      (m: any) => String(m.steam_id) === String(me.value.steam_id),
    ) ?? null
  );
});
const myLevel = computed(() => Number(myMembership.value?.level) || 0);

function memberHasPerm(perm: ClubPermKey) {
  const level = myLevel.value;
  if (!level) return false;
  const override = (club.value?.perm_overrides || []).find(
    (o: any) =>
      String(o.steam_id) === String(me.value?.steam_id) && o.perm_key === perm,
  );
  if (override?.allowed === true) return true;
  if (override?.allowed === false) return false;
  const min = Number(
    club.value?.level_permissions?.[perm]?.min ??
      DEFAULT_LEVEL_PERMISSIONS[perm].min,
  );
  return level >= min;
}

const canManage = computed(
  () => myLevel.value >= 8 || memberHasPerm("manage_settings"),
);
const isOwner = computed(
  () =>
    myLevel.value >= 10 ||
    myMembership.value?.role === "owner" ||
    String(club.value?.owner_steam_id) === String(me.value?.steam_id),
);
const canModerate = computed(
  () => myLevel.value >= 7 || memberHasPerm("moderate_news"),
);
const canPostNews = computed(() => memberHasPerm("post_news"));
const canManageLevels = computed(() => memberHasPerm("manage_levels"));
const isMember = computed(() => !!myMembership.value);
const myPendingRequest = computed(() => {
  if (!me.value || !club.value) return null;
  return (
    (club.value.join_requests ?? []).find(
      (row: any) => String(row.steam_id) === String(me.value.steam_id),
    ) ?? null
  );
});
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
    channels.find((c: any) => c.slug === "general") ||
    channels.find((c: any) => c.id === activeChannelId.value) ||
    channels[0]
  );
});
const newsPosts = computed(() => {
  const rows = (club.value?.channels ?? []).flatMap(
    (channel: any) => channel.messages ?? [],
  );
  return rows
    .filter((msg: any) => canModerate.value || !msg.deleted_at)
    .sort(
      (a: any, b: any) =>
        new Date(b.created_at).getTime() - new Date(a.created_at).getTime(),
    );
});

async function load(options?: { silent?: boolean }) {
  if (!options?.silent) loading.value = true;
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
            promo_text
            join_benefits
            level_permissions
            join_mode
            min_elo
            min_faceit_level
            owner_steam_id
            xp
            role
            can_manage
            created_at
            members_aggregate {
              aggregate {
                count
              }
            }
            members(order_by: [{ level: desc }, { xp: desc }, { joined_at: asc }]) {
              steam_id
              role
              level
              xp
              title
              staff_level
              is_primary
              is_vip
              joined_at
              player {
                steam_id
                name
                avatar_url
                faceit_elo
                faceit_skill_level
              }
            }
            perm_overrides {
              steam_id
              perm_key
              allowed
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
                image_url
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
    club.value = data?.clubs?.[0] ? thaw(data.clubs[0]) : null;
    if (!club.value) error.value = t("pages.clubs.not_found");
    else {
      minElo.value = club.value.min_elo;
      minLevel.value = club.value.min_faceit_level;
      descriptionInput.value = club.value.description || "";
      promoInput.value = club.value.promo_text || "";
      benefitsInput.value = club.value.join_benefits || "";
      {
        const mode = String(club.value.join_mode || "");
        joinModeInput.value =
          mode === "request" || mode === "invite_only" ? mode : "open";
      }
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
            bale_url
            rubika_url
            soroush_url
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
    club.value.bale_url = row.bale_url;
    club.value.rubika_url = row.rubika_url;
    club.value.soroush_url = row.soroush_url;
    telegramInput.value = row.telegram_url || "";
    instagramInput.value = row.instagram_url || "";
    baleInput.value = row.bale_url || "";
    rubikaInput.value = row.rubika_url || "";
    soroushInput.value = row.soroush_url || "";
  } catch {
    // Columns land after the Hasura metadata apply.
  }
}

async function runAction(mutation: string, variables: Record<string, unknown>) {
  busy.value = true;
  error.value = "";
  try {
    await client.mutate({ mutation: gql(mutation), variables });
    await load({ silent: true });
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
    `mutation($club_id: uuid!, $message: String) {
      requestClubJoin(club_id: $club_id, message: $message) { success }
    }`,
    {
      club_id: club.value.id,
      message: joinMessage.value.trim(),
    },
  );
}

async function openLeaveConfirm() {
  if (!me.value || !club.value) return;
  leaveBlockedUntil.value = null;
  leaveConfirmOpen.value = true;
  try {
    const { data } = await client.mutate({
      mutation: gql`
        mutation($club_id: uuid!) {
          previewClubLeave(club_id: $club_id) {
            blocked_until
            leave_count
          }
        }
      `,
      variables: { club_id: club.value.id },
      context: { optional: true },
    });
    leaveBlockedUntil.value = data?.previewClubLeave?.blocked_until ?? null;
  } catch (e: any) {
    leaveConfirmOpen.value = false;
    error.value = clubErrorMessage(e, t);
  }
}

async function leaveClub() {
  if (!me.value) return;
  leaveConfirmOpen.value = false;
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

async function toggleVip(steamId: string, isVip: boolean) {
  await runAction(
    `mutation($club_id: uuid!, $steam_id: String!, $is_vip: Boolean!) {
      setClubMemberVip(club_id: $club_id, steam_id: $steam_id, is_vip: $is_vip) {
        success
      }
    }`,
    { club_id: club.value.id, steam_id: steamId, is_vip: isVip },
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

async function saveJoinMode() {
  if (!club.value || !isOwner.value) return;
  joinModeError.value = "";
  busy.value = true;
  try {
    const { data } = await client.mutate({
      mutation: gql`
        mutation($id: uuid!, $_set: clubs_set_input!) {
          update_clubs_by_pk(pk_columns: { id: $id }, _set: $_set) {
            id
            join_mode
          }
        }
      `,
      variables: {
        id: club.value.id,
        _set: { join_mode: joinModeInput.value },
      },
    });
    const row = data?.update_clubs_by_pk;
    if (!row || !club.value) {
      joinModeError.value = t("pages.clubs.action_failed");
      return;
    }
    club.value = { ...club.value, join_mode: row.join_mode };
    joinModeInput.value = row.join_mode;
  } catch (e: any) {
    joinModeError.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
}

async function saveClubProfile() {
  if (!club.value) return;
  profileError.value = "";
  busy.value = true;
  try {
    const { data } = await client.mutate({
      mutation: gql`
        mutation($id: uuid!, $_set: clubs_set_input!) {
          update_clubs_by_pk(pk_columns: { id: $id }, _set: $_set) {
            id
            description
            promo_text
            join_benefits
          }
        }
      `,
      variables: {
        id: club.value.id,
        _set: {
          description: descriptionInput.value.trim() || null,
          promo_text: promoInput.value.trim() || null,
          join_benefits: benefitsInput.value.trim() || null,
        },
      },
    });
    const row = data?.update_clubs_by_pk;
    if (!row || !club.value) {
      profileError.value = t("pages.clubs.action_failed");
      return;
    }
    club.value = {
      ...club.value,
      description: row.description ?? null,
      promo_text: row.promo_text ?? null,
      join_benefits: row.join_benefits ?? null,
    };
  } catch (e: any) {
    profileError.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
}

function isOwnerNewsPost(msg: { author_steam_id?: string | number | null }) {
  if (!club.value?.owner_steam_id || msg?.author_steam_id == null) return false;
  return String(msg.author_steam_id) === String(club.value.owner_steam_id);
}

function openClubPage() {
  tab.value = "overview";
}

async function saveSocials() {
  const telegram = parseTelegramUrl(telegramInput.value);
  const instagram = parseInstagramUrl(instagramInput.value);
  const bale = parseBaleUrl(baleInput.value);
  const rubika = parseRubikaUrl(rubikaInput.value);
  const soroush = parseSoroushUrl(soroushInput.value);
  socialError.value = "";
  if (!telegram.ok) {
    socialError.value = t("pages.clubs.invalid_telegram");
    return;
  }
  if (!instagram.ok) {
    socialError.value = t("pages.clubs.invalid_instagram");
    return;
  }
  if (!bale.ok) {
    socialError.value = t("pages.clubs.invalid_bale");
    return;
  }
  if (!rubika.ok) {
    socialError.value = t("pages.clubs.invalid_rubika");
    return;
  }
  if (!soroush.ok) {
    socialError.value = t("pages.clubs.invalid_soroush");
    return;
  }
  busy.value = true;
  try {
    const { data } = await client.mutate({
      mutation: gql`
        mutation($id: uuid!, $_set: clubs_set_input!) {
          update_clubs_by_pk(pk_columns: { id: $id }, _set: $_set) {
            id
            telegram_url
            instagram_url
            bale_url
            rubika_url
            soroush_url
          }
        }
      `,
      variables: {
        id: club.value.id,
        _set: {
          telegram_url: telegram.url,
          instagram_url: instagram.url,
          bale_url: bale.url,
          rubika_url: rubika.url,
          soroush_url: soroush.url,
        },
      },
    });
    const row = data?.update_clubs_by_pk;
    if (!row || !club.value) {
      socialError.value = t("pages.clubs.action_failed");
      return;
    }
    club.value = {
      ...club.value,
      telegram_url: row.telegram_url ?? null,
      instagram_url: row.instagram_url ?? null,
      bale_url: row.bale_url ?? null,
      rubika_url: row.rubika_url ?? null,
      soroush_url: row.soroush_url ?? null,
    };
    telegramInput.value = row.telegram_url || "";
    instagramInput.value = row.instagram_url || "";
    baleInput.value = row.bale_url || "";
    rubikaInput.value = row.rubika_url || "";
    soroushInput.value = row.soroush_url || "";
  } catch (e: any) {
    socialError.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
}

function clearNewsImage() {
  if (newsPreview.value) URL.revokeObjectURL(newsPreview.value);
  newsImage.value = null;
  newsPreview.value = null;
  if (newsFileInput.value) newsFileInput.value.value = "";
}

function onNewsImage(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0];
  if (!file) return;
  if (!/^image\/(png|jpeg|webp)$/.test(file.type) || file.size > 10 * 1024 * 1024) {
    error.value = t("pages.clubs.news_image_invalid");
    if (newsFileInput.value) newsFileInput.value.value = "";
    return;
  }
  clearNewsImage();
  newsImage.value = file;
  newsPreview.value = URL.createObjectURL(file);
}

onBeforeUnmount(() => {
  if (newsPreview.value) URL.revokeObjectURL(newsPreview.value);
});

async function sendNews() {
  if (!canManage.value || !club.value || !activeChannel.value) return;
  const body = chatBody.value.trim();
  const file = newsImage.value;
  if (!body && !file) return;
  busy.value = true;
  error.value = "";
  try {
    let imageUrl: string | undefined;
    if (file) {
      const form = new FormData();
      form.append("file", file, file.name);
      const response = await fetch(
        `https://${apiDomain}/avatars/clubs/${club.value.id}/news-image`,
        { method: "POST", credentials: "include", body: form },
      );
      const payload = await response.json().catch(() => null);
      if (!response.ok || !payload?.path) {
        throw new Error(payload?.message || t("pages.clubs.news_image_failed"));
      }
      imageUrl = payload.path;
    }
    await client.mutate({
      mutation: gql`
        mutation ($object: club_messages_insert_input!) {
          insert_club_messages_one(object: $object) {
            id
          }
        }
      `,
      variables: {
        object: {
          channel_id: activeChannel.value.id,
          club_id: club.value.id,
          body,
          image_url: imageUrl ?? null,
        },
      },
    });
    chatBody.value = "";
    clearNewsImage();
    await load();
  } catch (e: any) {
    error.value = clubErrorMessage(e, t);
  } finally {
    busy.value = false;
  }
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

async function registerClash(eventId: string) {
  await runAction(
    `mutation($event_id: uuid!, $club_id: uuid!) {
      registerClubClash(event_id: $event_id, club_id: $club_id) { success }
    }`,
    { event_id: eventId, club_id: club.value.id },
  );
  await loadClash();
}

function clashStatus(ev: { starts_at: string; ends_at: string; locked?: boolean }) {
  if (ev.locked) return t("pages.clubs.clash_locked");
  const now = Date.now();
  const start = new Date(ev.starts_at).getTime();
  const end = new Date(ev.ends_at).getTime();
  if (now < start) return t("pages.clubs.clash_upcoming");
  if (now > end) return t("pages.clubs.clash_ended");
  return t("pages.clubs.clash_active");
}

function isRegisteredForClash(ev: { players?: Array<{ steam_id: string | number; club_id: string }> }) {
  if (!me.value || !club.value) return false;
  return (ev.players || []).some(
    (p) =>
      String(p.steam_id) === String(me.value.steam_id) &&
      String(p.club_id) === String(club.value.id),
  );
}

async function onStaffLevelChange(member: any, value: string) {
  if (!club.value) return;
  const staff_level = value === "" ? null : Number(value);
  await runAction(
    `mutation($club_id: uuid!, $steam_id: String!, $staff_level: Int, $title: String) {
      setClubMemberStaffLevel(
        club_id: $club_id
        steam_id: $steam_id
        staff_level: $staff_level
        title: $title
      ) { success }
    }`,
    {
      club_id: club.value.id,
      steam_id: String(member.steam_id),
      staff_level,
      title: member.title || null,
    },
  );
  await load({ silent: true });
}

async function setMemberTitle(steamId: string, title: string) {
  if (!club.value) return;
  await runAction(
    `mutation($club_id: uuid!, $steam_id: String!, $title: String) {
      setClubMemberTitle(club_id: $club_id, steam_id: $steam_id, title: $title) { success }
    }`,
    { club_id: club.value.id, steam_id: steamId, title: title || null },
  );
  await load({ silent: true });
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
            players {
              steam_id
              club_id
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

watch([tab, isOwner, loading], () => {
  if (loading.value) return;
  if (tab.value === "feed" && !isOwner.value) {
    tab.value = "overview";
  }
});

watch(tab, (v) => {
  if (v === "clash") void loadClash();
  if (v === "teams") void loadTeams();
  if (v === "tournaments") void loadTournaments();
});

type TabItem = { id: Tab; label: string; icon: any };
type TabGroup = { id: string; label: string; items: TabItem[] };

const tabGroups = computed((): TabGroup[] => {
  const clubItems: TabItem[] = [
    { id: "overview", label: t("pages.clubs.tabs.overview"), icon: Newspaper },
    { id: "chat", label: t("pages.clubs.tabs.chat"), icon: Megaphone },
    { id: "members", label: t("pages.clubs.tabs.members"), icon: Users },
  ];
  if (isOwner.value) {
    clubItems.splice(1, 0, {
      id: "feed",
      label: t("pages.clubs.tabs.feed"),
      icon: Activity,
    });
  }
  const groups: TabGroup[] = [
    {
      id: "club",
      label: t("pages.clubs.tab_groups.club"),
      items: clubItems,
    },
    {
      id: "play",
      label: t("pages.clubs.tab_groups.play"),
      items: [
        { id: "servers", label: t("pages.clubs.tabs.servers"), icon: Server },
        {
          id: "challenges",
          label: t("pages.clubs.tabs.challenges"),
          icon: Flame,
        },
        {
          id: "leaderboard",
          label: t("pages.clubs.tabs.leaderboard"),
          icon: ListOrdered,
        },
      ],
    },
    {
      id: "compete",
      label: t("pages.clubs.tab_groups.compete"),
      items: [
        { id: "teams", label: t("pages.clubs.tabs.teams"), icon: Shield },
        {
          id: "tournaments",
          label: t("pages.clubs.tabs.tournaments"),
          icon: CalendarDays,
        },
        { id: "clash", label: t("pages.clubs.tabs.clash"), icon: Trophy },
      ],
    },
  ];
  if (canManage.value) {
    groups.push({
      id: "manage",
      label: t("pages.clubs.tab_groups.manage"),
      items: [
        { id: "admin", label: t("pages.clubs.tabs.admin"), icon: Settings },
      ],
    });
  }
  return groups;
});

const benefitLines = computed(() =>
  String(club.value?.join_benefits || "")
    .split(/\r?\n/)
    .map((line: string) => line.trim())
    .filter(Boolean),
);

const ROLE_RANK: Record<string, number> = {
  owner: 0,
  co_owner: 1,
  admin: 2,
  moderator: 3,
  member: 4,
};

const filteredMembers = computed(() => {
  const q = memberQuery.value.trim().toLowerCase();
  const list = (club.value?.members ?? []).filter((m: any) => {
    if (!q) return true;
    return String(m.player?.name || m.steam_id).toLowerCase().includes(q);
  });
  return [...list].sort((a: any, b: any) => {
    const levelDiff = (Number(b.level) || 1) - (Number(a.level) || 1);
    if (levelDiff) return levelDiff;
    const xpDiff = (Number(b.xp) || 0) - (Number(a.xp) || 0);
    if (xpDiff) return xpDiff;
    const roleDiff =
      (ROLE_RANK[String(a.role)] ?? 9) - (ROLE_RANK[String(b.role)] ?? 9);
    if (roleDiff) return roleDiff;
    return String(a.joined_at || "").localeCompare(String(b.joined_at || ""));
  });
});

function roleLabel(role: string) {
  const key = `pages.clubs.roles.${role}`;
  const out = t(key);
  return out === key ? role : String(out);
}

function roleChipClass(role: string) {
  if (role === "owner")
    return "bg-[hsl(var(--tac-amber)/0.18)] text-[hsl(var(--tac-amber))]";
  if (role === "co_owner") return "bg-violet-500/15 text-violet-300";
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
              <button
                type="button"
                class="truncate text-start font-sans text-[1.65rem] font-bold leading-none tracking-tight hover:text-[hsl(var(--tac-amber))] sm:text-3xl"
                @click="openClubPage"
              >
                {{ club.name }}
              </button>
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
              :bale="club.bale_url"
              :rubika="club.rubika_url"
              :soroush="club.soroush_url"
            />
            <div class="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
              <span>{{ club.members_aggregate?.aggregate?.count ?? 0 }} {{ t("pages.clubs.members_label") }}</span>
              <span class="text-border">·</span>
              <span class="tabular-nums">
                {{ Number(club.xp) || 0 }} {{ t("pages.clubs.club_xp_label") }}
              </span>
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
              <p
                v-if="myPendingRequest"
                class="self-center text-sm text-muted-foreground"
              >
                {{ t("pages.clubs.request_pending") }}
              </p>
              <template v-else>
                <Input v-model="joinMessage" :placeholder="t('pages.clubs.request_message')" class="w-40" />
                <Button :disabled="busy" :class="tacticalCtaButtonClasses" @click="requestJoin">
                  {{ t("pages.clubs.request_join") }}
                </Button>
              </template>
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
              @click="openLeaveConfirm"
            >
              {{ t("pages.clubs.leave") }}
            </Button>
          </div>
        </div>
      </header>
    </PageTransition>

    <p v-if="error" class="mt-3 text-sm text-destructive">{{ error }}</p>

    <div class="mt-6 grid items-start gap-6 md:grid-cols-[14rem_minmax(0,1fr)]">
      <nav
        class="flex gap-3 overflow-x-auto pb-1 md:sticky md:top-4 md:flex-col md:gap-4 md:overflow-visible md:rounded-xl md:border md:border-border/70 md:bg-card/40 md:p-3"
      >
        <div
          v-for="group in tabGroups"
          :key="group.id"
          class="flex shrink-0 gap-1 md:flex-col md:gap-0.5"
        >
          <div
            class="hidden px-2 pb-1 pt-0.5 text-[0.65rem] font-semibold uppercase tracking-wider text-muted-foreground/80 md:block"
          >
            {{ group.label }}
          </div>
          <button
            v-for="item in group.items"
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
        </div>
      </nav>

      <div class="min-w-0">
        <!-- Club page -->
        <section v-if="tab === 'overview'" class="space-y-4">
          <div class="grid gap-4 lg:grid-cols-[minmax(0,1fr)_16rem]">
            <div class="space-y-4">
              <div class="rounded-xl border border-border/70 bg-card/40 p-4 sm:p-5">
                <h2 class="mb-3 text-sm font-semibold">{{ t("pages.clubs.about") }}</h2>
                <p class="whitespace-pre-wrap text-sm leading-relaxed text-muted-foreground">
                  {{ club.description || t("pages.clubs.no_description") }}
                </p>
              </div>

              <div
                v-if="club.promo_text"
                class="rounded-xl border border-[hsl(var(--tac-amber)/0.35)] bg-[hsl(var(--tac-amber)/0.08)] p-4 sm:p-5"
              >
                <div class="mb-2 flex items-center gap-2 text-sm font-semibold text-[hsl(var(--tac-amber))]">
                  <BadgePercent class="h-4 w-4" />
                  {{ t("pages.clubs.promo") }}
                </div>
                <p class="whitespace-pre-wrap text-sm leading-relaxed">
                  {{ club.promo_text }}
                </p>
              </div>

              <div
                v-if="benefitLines.length"
                class="rounded-xl border border-border/70 bg-card/40 p-4 sm:p-5"
              >
                <div class="mb-3 flex items-center gap-2 text-sm font-semibold">
                  <Gift class="h-4 w-4 text-[hsl(var(--tac-amber))]" />
                  {{ t("pages.clubs.join_benefits") }}
                </div>
                <ul class="space-y-2">
                  <li
                    v-for="(line, idx) in benefitLines"
                    :key="idx"
                    class="flex gap-2 text-sm text-muted-foreground"
                  >
                    <span class="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-[hsl(var(--tac-amber))]" />
                    <span>{{ line }}</span>
                  </li>
                </ul>
              </div>
            </div>

            <aside class="space-y-3">
              <div class="rounded-xl border border-border/70 bg-card/40 p-4">
                <div class="text-xs text-muted-foreground">{{ t("pages.clubs.members_label") }}</div>
                <div class="mt-1 text-2xl font-bold tabular-nums">
                  {{ club.members_aggregate?.aggregate?.count ?? 0 }}
                </div>
              </div>
              <div class="rounded-xl border border-border/70 bg-card/40 p-4">
                <div class="text-xs text-muted-foreground">{{ t("pages.clubs.club_xp_label") }}</div>
                <div class="mt-1 text-2xl font-bold tabular-nums">
                  {{ Number(club.xp) || 0 }}
                </div>
              </div>
              <div class="rounded-xl border border-border/70 bg-card/40 p-4">
                <div class="text-xs text-muted-foreground">{{ t("pages.clubs.news_stat") }}</div>
                <div class="mt-1 text-2xl font-bold tabular-nums">{{ newsPosts.length }}</div>
              </div>
              <div class="rounded-xl border border-border/70 bg-card/40 p-4">
                <h3 class="mb-2 text-xs font-semibold uppercase tracking-wide text-muted-foreground">
                  {{ t("pages.clubs.socials") }}
                </h3>
                <ClubSocialLinks
                  :telegram="club.telegram_url"
                  :instagram="club.instagram_url"
                  :bale="club.bale_url"
                  :rubika="club.rubika_url"
                  :soroush="club.soroush_url"
                />
                <p
                  v-if="
                    !club.telegram_url &&
                    !club.instagram_url &&
                    !club.bale_url &&
                    !club.rubika_url &&
                    !club.soroush_url
                  "
                  class="text-xs text-muted-foreground"
                >
                  {{ t("pages.clubs.no_socials") }}
                </p>
              </div>
            </aside>
          </div>
        </section>

        <!-- Owner-only activity feed -->
        <section v-else-if="tab === 'feed' && isOwner" class="grid gap-6 lg:grid-cols-[1fr_16rem]">
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
                  {{ formatSiteDate(a.created_at) }}
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
              <div class="text-xs text-muted-foreground">{{ t("pages.clubs.news_stat") }}</div>
              <div class="mt-1 text-2xl font-bold tabular-nums">{{ newsPosts.length }}</div>
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
          <p class="mb-3 text-xs text-muted-foreground">{{ t("pages.clubs.levels_members_hint") }}</p>
          <div class="divide-y divide-border/50">
            <div
              v-for="m in filteredMembers"
              :key="m.steam_id"
              class="flex flex-wrap items-center gap-2 py-2.5 sm:gap-3"
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
                class="shrink-0 rounded-full bg-[hsl(var(--tac-amber)/0.14)] px-2 py-0.5 text-[0.65rem] font-semibold text-[hsl(var(--tac-amber))]"
              >
                L{{ m.level || 1 }} · {{ clubLevelLabel(Number(m.level) || 1, t) }}
              </span>
              <span
                v-if="m.title"
                class="shrink-0 rounded-full border border-border/70 px-2 py-0.5 text-[0.65rem] text-muted-foreground"
              >
                {{ m.title }}
              </span>
              <span class="text-[0.65rem] tabular-nums text-muted-foreground">
                {{ m.xp || 0 }} XP
              </span>
              <span
                class="ms-auto shrink-0 rounded-full px-2 py-0.5 text-[0.65rem] font-medium uppercase tracking-wide"
                :class="roleChipClass(m.role)"
              >
                {{ roleLabel(m.role) }}
              </span>
              <template v-if="canManageLevels && Number(m.level) !== 10">
                <select
                  class="h-8 rounded-md border border-border bg-background px-2 text-xs"
                  :value="m.staff_level ?? ''"
                  @change="onStaffLevelChange(m, ($event.target as HTMLSelectElement).value)"
                >
                  <option value="">{{ t("pages.clubs.staff_auto") }}</option>
                  <option value="7">{{ t("pages.clubs.levels.7") }}</option>
                  <option value="8">{{ t("pages.clubs.levels.8") }}</option>
                  <option value="9">{{ t("pages.clubs.levels.9") }}</option>
                </select>
                <Input
                  class="h-8 w-28 text-xs"
                  :model-value="m.title || ''"
                  :placeholder="t('pages.clubs.title_placeholder')"
                  @blur="setMemberTitle(String(m.steam_id), ($event.target as HTMLInputElement).value)"
                />
              </template>
              <Button
                v-if="isOwner && Number(m.level) !== 10"
                size="sm"
                variant="outline"
                :disabled="busy"
                @click="toggleVip(String(m.steam_id), !m.is_vip)"
              >
                {{
                  m.is_vip
                    ? t("pages.clubs.vip_remove")
                    : t("pages.clubs.club_vip_grant")
                }}
              </Button>
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

        <!-- News -->
        <section v-else-if="tab === 'chat'" class="space-y-4">
          <form
            v-if="canPostNews"
            class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4"
            @submit.prevent="sendNews"
          >
            <p class="text-xs text-muted-foreground">
              {{
                isOwner
                  ? t("pages.clubs.news_as_club_hint")
                  : t("pages.clubs.news_as_account_hint")
              }}
            </p>
            <textarea
              v-model="chatBody"
              rows="3"
              class="w-full resize-y rounded-md border border-border bg-background px-3 py-2 text-sm"
              :placeholder="t('pages.clubs.news_placeholder')"
            />
            <div class="flex flex-wrap items-center gap-3">
              <label class="inline-flex cursor-pointer items-center rounded-md border border-border px-3 py-1.5 text-sm text-muted-foreground hover:text-foreground">
                <input
                  ref="newsFileInput"
                  type="file"
                  accept="image/png,image/jpeg,image/webp"
                  class="sr-only"
                  @change="onNewsImage"
                />
                {{ t("pages.clubs.news_add_image") }}
              </label>
              <img
                v-if="newsPreview"
                :src="newsPreview"
                alt=""
                class="h-16 w-24 rounded-md object-cover"
              />
              <button
                v-if="newsPreview"
                type="button"
                class="text-xs text-destructive"
                @click="clearNewsImage"
              >
                {{ t("common.delete") }}
              </button>
              <Button
                type="submit"
                class="ms-auto"
                :disabled="busy || (!chatBody.trim() && !newsImage)"
                :class="tacticalCtaButtonClasses"
              >
                {{ t("pages.clubs.send") }}
              </Button>
            </div>
          </form>
          <p v-else class="text-sm text-muted-foreground">
            {{ t("pages.clubs.news_staff_only") }}
          </p>

          <article
            v-for="msg in newsPosts"
            :key="msg.id"
            class="rounded-xl border border-border/70 bg-card/40 p-4"
            :class="{ 'opacity-40': msg.deleted_at }"
          >
            <div class="flex flex-wrap items-baseline justify-between gap-2">
              <button
                v-if="isOwnerNewsPost(msg)"
                type="button"
                class="text-sm font-semibold text-[hsl(var(--tac-amber))] hover:underline"
                @click="openClubPage"
              >
                {{ club.name }}
              </button>
              <span
                v-else
                class="text-sm font-semibold text-[hsl(var(--tac-amber))]"
              >
                {{ msg.author?.name || msg.author_steam_id }}
              </span>
              <span class="text-xs text-muted-foreground">
                {{ formatSiteDate(msg.created_at) }}
              </span>
            </div>
            <p
              v-if="msg.body"
              class="mt-3 whitespace-pre-wrap text-sm leading-relaxed"
              :class="{ 'line-through': msg.deleted_at }"
            >
              {{ msg.body }}
            </p>
            <img
              v-if="msg.image_url && !msg.deleted_at"
              :src="resolveAvatarUrl(msg.image_url, apiDomain) || undefined"
              alt=""
              class="mt-3 max-h-96 w-full rounded-lg object-cover"
            />
            <button
              v-if="canModerate && !msg.deleted_at"
              type="button"
              class="mt-3 text-xs text-destructive"
              @click="deleteMessage(msg.id)"
            >
              {{ t("common.delete") }}
            </button>
          </article>
          <p
            v-if="!newsPosts.length"
            class="rounded-xl border border-border/70 bg-card/40 p-8 text-center text-sm text-muted-foreground"
          >
            {{ t("pages.clubs.news_empty") }}
          </p>
        </section>

        <!-- Leaderboard -->
        <section
          v-else-if="tab === 'leaderboard'"
          class="rounded-xl border border-border/70 bg-card/40 px-6 py-16 text-center"
        >
          <h2 class="text-base font-semibold">{{ t("pages.clubs.tabs.leaderboard") }}</h2>
          <p class="mt-2 text-sm text-muted-foreground">
            {{ t("pages.clubs.coming_soon") }}
          </p>
        </section>

        <ClubServersPanel
          v-else-if="tab === 'servers'"
          :club-id="club.id"
          :can-manage="canManage || auth.isAdmin"
        />

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

        <!-- Club challenges -->
        <section v-else-if="tab === 'challenges'" class="space-y-4">
          <ClubChallengesPanel
            v-if="club?.id"
            :club-id="club.id"
            :is-owner="isOwner"
            :members="club.members || []"
          />
        </section>

        <!-- Clash -->
        <section
          v-else-if="tab === 'clash'"
          class="rounded-xl border border-border/70 bg-card/40 px-6 py-16 text-center"
        >
          <h2 class="text-base font-semibold">{{ t("pages.clubs.clash_title") }}</h2>
          <p class="mt-2 text-sm text-muted-foreground">
            {{ t("pages.clubs.coming_soon") }}
          </p>
        </section>

        <!-- Admin / Settings -->
        <section v-else-if="tab === 'admin' && canManage" class="grid gap-4 lg:grid-cols-2">
          <div
            v-if="isOwner"
            class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4 lg:col-span-2"
          >
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.form.join_mode") }}</h3>
            <p class="text-xs text-muted-foreground">{{ t("pages.clubs.join_mode_hint") }}</p>
            <div class="flex flex-wrap items-end gap-2">
              <label class="block min-w-[12rem] flex-1 space-y-1 text-xs font-medium">
                {{ t("pages.clubs.form.join_mode") }}
                <select
                  v-model="joinModeInput"
                  class="h-9 w-full rounded-md border border-border bg-background px-3 text-sm font-normal text-foreground"
                >
                  <option value="open">{{ t("pages.clubs.join_modes.open") }}</option>
                  <option value="request">{{ t("pages.clubs.join_modes.request") }}</option>
                  <option value="invite_only">
                    {{ t("pages.clubs.join_modes.invite_only") }}
                  </option>
                </select>
              </label>
              <Button type="button" :disabled="busy" @click="saveJoinMode">
                {{ t("common.save") }}
              </Button>
            </div>
            <p v-if="joinModeError" class="text-xs text-destructive">{{ joinModeError }}</p>
          </div>
          <div
            class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4 lg:col-span-2"
          >
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.requests") }}</h3>
            <p class="text-xs text-muted-foreground">{{ t("pages.clubs.requests_hint") }}</p>
            <p
              v-if="!(club.join_requests || []).length"
              class="text-sm text-muted-foreground"
            >
              {{ t("pages.clubs.requests_empty") }}
            </p>
            <div
              v-for="r in club.join_requests"
              :key="r.id"
              class="flex flex-wrap items-center gap-2 rounded-lg border border-border/60 bg-background/40 px-3 py-2 text-sm"
            >
              <span class="min-w-0 flex-1 truncate font-medium">
                {{ r.player?.name || r.steam_id }}
              </span>
              <span
                v-if="r.message"
                class="w-full text-xs text-muted-foreground sm:w-auto sm:flex-1"
              >
                {{ r.message }}
              </span>
              <Button size="sm" :disabled="busy" @click="reviewRequest(r.id, true)">
                {{ t("common.accept") }}
              </Button>
              <Button
                size="sm"
                variant="outline"
                :disabled="busy"
                @click="reviewRequest(r.id, false)"
              >
                {{ t("common.decline") }}
              </Button>
            </div>
          </div>
          <ClubLevelSettings
            v-if="canManageLevels && club"
            :club-id="club.id"
            :level-permissions="club.level_permissions"
            :members="club.members || []"
            @saved="(perms) => { if (club) club.level_permissions = perms; }"
          />
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
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4 lg:col-span-2">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.page_content") }}</h3>
            <p class="text-xs text-muted-foreground">{{ t("pages.clubs.page_content_hint") }}</p>
            <label class="block space-y-1 text-xs font-medium">
              {{ t("pages.clubs.form.description") }}
              <textarea
                v-model="descriptionInput"
                rows="4"
                class="w-full resize-y rounded-md border border-border bg-background px-3 py-2 text-sm font-normal text-foreground"
                :placeholder="t('pages.clubs.description_placeholder')"
              />
            </label>
            <label class="block space-y-1 text-xs font-medium">
              {{ t("pages.clubs.promo") }}
              <textarea
                v-model="promoInput"
                rows="3"
                class="w-full resize-y rounded-md border border-border bg-background px-3 py-2 text-sm font-normal text-foreground"
                :placeholder="t('pages.clubs.promo_placeholder')"
              />
            </label>
            <label class="block space-y-1 text-xs font-medium">
              {{ t("pages.clubs.join_benefits") }}
              <textarea
                v-model="benefitsInput"
                rows="4"
                class="w-full resize-y rounded-md border border-border bg-background px-3 py-2 text-sm font-normal text-foreground"
                :placeholder="t('pages.clubs.join_benefits_placeholder')"
              />
            </label>
            <p v-if="profileError" class="text-xs text-destructive">{{ profileError }}</p>
            <Button type="button" :disabled="busy" @click="saveClubProfile">
              {{ t("common.save") }}
            </Button>
          </div>
          <div class="space-y-3 rounded-xl border border-border/70 bg-card/40 p-4">
            <h3 class="text-sm font-semibold">{{ t("pages.clubs.socials") }}</h3>
            <p class="text-xs text-muted-foreground">{{ t("pages.clubs.socials_hint") }}</p>
            <label class="block space-y-1 text-xs font-medium text-[#2AABEE]">
              {{ t("pages.clubs.telegram") }}
              <Input
                v-model="telegramInput"
                dir="ltr"
                class="font-mono text-foreground"
                :placeholder="t('pages.clubs.telegram_placeholder')"
              />
            </label>
            <label class="block space-y-1 text-xs font-medium text-[#E1306C]">
              {{ t("pages.clubs.instagram") }}
              <Input
                v-model="instagramInput"
                dir="ltr"
                class="font-mono text-foreground"
                :placeholder="t('pages.clubs.instagram_placeholder')"
              />
            </label>
            <label class="block space-y-1 text-xs font-medium text-[#00A86B]">
              {{ t("pages.clubs.bale") }}
              <Input
                v-model="baleInput"
                dir="ltr"
                class="font-mono text-foreground"
                :placeholder="t('pages.clubs.bale_placeholder')"
              />
            </label>
            <label class="block space-y-1 text-xs font-medium text-[#7B2FBE]">
              {{ t("pages.clubs.rubika") }}
              <Input
                v-model="rubikaInput"
                dir="ltr"
                class="font-mono text-foreground"
                :placeholder="t('pages.clubs.rubika_placeholder')"
              />
            </label>
            <label class="block space-y-1 text-xs font-medium text-[#1E88E5]">
              {{ t("pages.clubs.soroush") }}
              <Input
                v-model="soroushInput"
                dir="ltr"
                class="font-mono text-foreground"
                :placeholder="t('pages.clubs.soroush_placeholder')"
              />
            </label>
            <p v-if="socialError" class="text-xs text-destructive">{{ socialError }}</p>
            <Button type="button" :disabled="busy" @click="saveSocials">
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

  <AlertDialog v-model:open="leaveConfirmOpen">
    <AlertDialogContent>
      <AlertDialogHeader>
        <AlertDialogTitle>{{ t("pages.clubs.leave_confirm_title") }}</AlertDialogTitle>
        <AlertDialogDescription>
          {{
            leaveBlockedWhen
              ? t("pages.clubs.leave_confirm_body", { when: leaveBlockedWhen })
              : t("pages.clubs.leave_confirm_body_unknown")
          }}
        </AlertDialogDescription>
      </AlertDialogHeader>
      <AlertDialogFooter>
        <AlertDialogCancel>{{ t("common.cancel") }}</AlertDialogCancel>
        <AlertDialogAction
          class="bg-destructive text-destructive-foreground hover:bg-destructive/90"
          :disabled="busy"
          @click="leaveClub"
        >
          {{ t("pages.clubs.leave_confirm_action") }}
        </AlertDialogAction>
      </AlertDialogFooter>
    </AlertDialogContent>
  </AlertDialog>
</template>

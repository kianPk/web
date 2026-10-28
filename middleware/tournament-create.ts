import { gql } from "@apollo/client/core";
import { useApolloClient } from "@vue/apollo-composable";
import { useAuthStore } from "~/stores/AuthStore";

const CLUB_UUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export default defineNuxtRouteMiddleware(async (to, from) => {
  if (process.server) {
    return;
  }

  const router = useRouter();

  // A redirect issued after an await would pull the player off whatever page
  // they moved on to while this navigation was still pending.
  const superseded = () => router.currentRoute.value !== from;

  const authStore = useAuthStore();

  // /tournaments is a public route, so nothing upstream has waited out the
  // verification of a cached `me`.
  await authStore.getMe();

  if (superseded()) {
    return;
  }

  if (!authStore.me) {
    return navigateTo(`/login?redirect=${to.path}`);
  }

  // Site admins may create any tournament.
  if (authStore.isAdmin) {
    return;
  }

  // Club owners may create only for a club they own (?club=<uuid>).
  const clubQuery = to.query?.club;
  const clubId = Array.isArray(clubQuery) ? clubQuery[0] : clubQuery;
  if (typeof clubId === "string" && CLUB_UUID_RE.test(clubId)) {
    try {
      const { client } = useApolloClient();
      const { data } = await client.query({
        query: gql`
          query TournamentCreateClubOwner($id: uuid!) {
            clubs_by_pk(id: $id) {
              owner_steam_id
            }
          }
        `,
        variables: { id: clubId },
        fetchPolicy: "network-only",
      });
      if (superseded()) {
        return;
      }
      if (
        String(data?.clubs_by_pk?.owner_steam_id) ===
        String(authStore.me.steam_id)
      ) {
        return;
      }
    } catch {
      // Fall through to redirect.
    }
  }

  return navigateTo("/tournaments");
});

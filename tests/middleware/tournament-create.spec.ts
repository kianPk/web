import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { reactive, shallowRef } from "vue";
import { mockNuxtImport } from "@nuxt/test-utils/runtime";
import type { RouteLocationNormalized } from "vue-router";
import { e_player_roles_enum } from "~/generated/zeus";
import tournamentCreate from "~/middleware/tournament-create";

const { navigateToMock, router, apolloQuery } = vi.hoisted(() => ({
  navigateToMock: vi.fn((to: unknown) => to),
  router: { currentRoute: { value: undefined as unknown } },
  apolloQuery: vi.fn(),
}));

mockNuxtImport("navigateTo", () => navigateToMock);
mockNuxtImport("useRouter", () => () => router);

vi.mock("@vue/apollo-composable", () => ({
  useApolloClient: () => ({ client: { query: apolloQuery } }),
}));

const auth = reactive({
  me: undefined as
    | { role: e_player_roles_enum; steam_id: string }
    | undefined,
  isAdmin: false,
  hasCheckedSession: true,
  getMe: vi.fn(async () => true),
});

vi.mock("~/stores/AuthStore", () => ({ useAuthStore: () => auth }));

const from = { path: "/tournaments" } as RouteLocationNormalized;
const to = { path: "/tournaments/create" } as RouteLocationNormalized;

function run() {
  return tournamentCreate(to, from);
}

beforeEach(() => {
  navigateToMock.mockClear();
  apolloQuery.mockReset();
  router.currentRoute = shallowRef(from);
  auth.getMe.mockReset();
  auth.getMe.mockResolvedValue(true);
  auth.me = { role: e_player_roles_enum.user, steam_id: "111" };
  auth.isAdmin = false;
});

afterEach(() => {
  vi.useRealTimers();
});

describe("tournament-create middleware", () => {
  it("lets a site admin through", async () => {
    auth.me = { role: e_player_roles_enum.administrator, steam_id: "1" };
    auth.isAdmin = true;

    await expect(run()).resolves.toBeUndefined();
    expect(navigateToMock).not.toHaveBeenCalled();
    expect(apolloQuery).not.toHaveBeenCalled();
  });

  it("blocks a regular user without a club", async () => {
    await run();
    expect(navigateToMock).toHaveBeenCalledWith("/tournaments");
  });

  it("blocks tournament_organizer without a club", async () => {
    auth.me = {
      role: e_player_roles_enum.tournament_organizer,
      steam_id: "111",
    };

    await run();

    expect(navigateToMock).toHaveBeenCalledWith("/tournaments");
  });

  it("lets a verified club owner through create", async () => {
    apolloQuery.mockResolvedValue({
      data: { clubs_by_pk: { owner_steam_id: "111" } },
    });
    const clubTo = {
      path: "/tournaments/create",
      query: { club: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" },
    } as unknown as RouteLocationNormalized;

    await expect(tournamentCreate(clubTo, from)).resolves.toBeUndefined();
    expect(navigateToMock).not.toHaveBeenCalled();
    expect(apolloQuery).toHaveBeenCalledOnce();
  });

  it("blocks a non-owner even with a club query", async () => {
    apolloQuery.mockResolvedValue({
      data: { clubs_by_pk: { owner_steam_id: "999" } },
    });
    const clubTo = {
      path: "/tournaments/create",
      query: { club: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" },
    } as unknown as RouteLocationNormalized;

    await tournamentCreate(clubTo, from);

    expect(navigateToMock).toHaveBeenCalledWith("/tournaments");
  });

  it("sends a signed-out visitor to login and back", async () => {
    auth.me = undefined;
    auth.getMe.mockResolvedValue(false);

    await run();

    expect(navigateToMock).toHaveBeenCalledWith(
      "/login?redirect=/tournaments/create",
    );
  });

  it("leaves a player alone once they have moved on", async () => {
    apolloQuery.mockImplementation(async () => {
      router.currentRoute.value = {
        path: "/matches",
      } as RouteLocationNormalized;
      return { data: { clubs_by_pk: { owner_steam_id: "111" } } };
    });
    const clubTo = {
      path: "/tournaments/create",
      query: { club: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" },
    } as unknown as RouteLocationNormalized;

    await expect(tournamentCreate(clubTo, from)).resolves.toBeUndefined();
    expect(navigateToMock).not.toHaveBeenCalled();
  });
});

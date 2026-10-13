import gql from "graphql-tag";

// Raw documents, like graphql/leagues.ts: the 1v1 tables and actions work
// before the zeus client is regenerated against a Hasura that has them.

export const DUEL_LISTINGS_SUBSCRIPTION = gql`
  subscription DuelListings {
    player_duel_listings(order_by: { created_at: desc }, limit: 60) {
      steam_id
      note
      created_at
      player {
        steam_id
        name
        avatar_url
        country
        elo
      }
    }
  }
`;

export const MY_DUELS_SUBSCRIPTION = gql`
  subscription MyDuels($steamId: bigint!) {
    player_duel_requests(
      where: {
        _or: [
          { from_steam_id: { _eq: $steamId } }
          { to_steam_id: { _eq: $steamId } }
        ]
        status: { _in: ["Pending", "Matched"] }
      }
      order_by: { created_at: desc }
      limit: 30
    ) {
      id
      status
      from_steam_id
      to_steam_id
      best_of
      proposed_scheduled_at
      expires_at
      match_id
      wager_ypoints
      wager_status
      created_at
      map {
        id
        name
        patch
      }
      match {
        id
        status
      }
      from_player {
        steam_id
        name
        avatar_url
      }
      to_player {
        steam_id
        name
        avatar_url
      }
    }
  }
`;

export const DUEL_MAP_POOLS_QUERY = gql`
  query DuelMapPools {
    map_pools(where: { type: { _eq: "Duel" }, enabled: { _eq: true } }) {
      id
      seed
      maps {
        id
        name
        patch
      }
    }
  }
`;

export const JOIN_DUEL_LIST_MUTATION = gql`
  mutation JoinDuelList($note: String) {
    insert_player_duel_listings_one(object: { note: $note }) {
      steam_id
    }
  }
`;

export const LEAVE_DUEL_LIST_MUTATION = gql`
  mutation LeaveDuelList($steamId: bigint!) {
    delete_player_duel_listings(where: { steam_id: { _eq: $steamId } }) {
      affected_rows
    }
  }
`;

export const SEND_DUEL_MUTATION = gql`
  mutation SendDuel(
    $toSteamId: bigint!
    $bestOf: Int
    $mapId: uuid
    $proposedScheduledAt: timestamptz
    $wagerYpoints: Int
  ) {
    sendDuelRequest(
      to_steam_id: $toSteamId
      best_of: $bestOf
      map_id: $mapId
      proposed_scheduled_at: $proposedScheduledAt
      wager_ypoints: $wagerYpoints
    ) {
      success
    }
  }
`;

export const RESPOND_DUEL_MUTATION = gql`
  mutation RespondDuel($requestId: uuid!, $accept: Boolean!) {
    respondToDuelRequest(request_id: $requestId, accept: $accept) {
      success
    }
  }
`;

export const CANCEL_DUEL_MUTATION = gql`
  mutation CancelDuel($requestId: uuid!) {
    cancelDuelRequest(request_id: $requestId) {
      success
    }
  }
`;

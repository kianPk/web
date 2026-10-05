export type HostedPlan = {
  id: string;
  title: string;
  description: string;
  price_irr: number;
  price_ypoint: number | null;
  image_url: string | null;
  hosted_slots: number;
  duration: string;
};

export type HostedAdminPlan = {
  id: string;
  title: string;
  price_irr: number;
  price_ypoint: number | null;
  hosted_slots: number;
  duration: string;
  active: boolean;
  servers: number;
};

export type HostedCheckoutResult =
  | { paid: false; deepLink: string }
  | { paid: true; hostedServerId: string | null; balance: number };

export type HostedOverview = {
  enabled: boolean;
  available: boolean;
  remaining: number;
  plans: HostedPlan[];
  types: string[];
  slot_price_irr: number;
  slot_price_ypoint: number;
  max_slots: number;
};

export type HostedServer = {
  id: string;
  server_id: string | null;
  owner_steam_id: string;
  owner_name: string | null;
  label: string;
  slots: number;
  extra_slots: number;
  status:
    "provisioning" | "active" | "expired" | "suspended" | "failed" | "deleted";
  status_detail: string | null;
  expires_at: string;
  created_at: string;
  product_id: string | null;
  type: string | null;
  host: string | null;
  port: number | null;
  connect_password: string | null;
  enabled: boolean | null;
  connected: boolean | null;
  has_gslt: boolean;
  players: number | null;
  map: string | null;
  chat_ads?: HostedChatAds;
};

export type HostedChatAds = {
  enabled: boolean;
  interval_seconds: number;
  color: string;
  messages: string[];
};

export type HostedAdminSettings = {
  enabled: boolean;
  node_id: string;
  max_active: number;
  reserve_match_slots: number;
  grace_days: number;
  gslt_pool_size: number;
  steam_api_key_set: boolean;
  slot_price_irr: number;
  slot_price_ypoint: number;
  max_slots: number;
};

export type HostedSlotsQuote = {
  count: number;
  days: number;
  slots_after: number;
  price_irr: number;
  price_ypoint: number;
};

export function hostedErrorMessage(error: any): string {
  const message =
    error?.data?.message ||
    error?.data?.statusMessage ||
    error?.statusMessage ||
    error?.message ||
    String(error);
  return Array.isArray(message) ? message.join(", ") : String(message);
}

export function hostedApi<T>(
  path: string,
  options: { method?: "GET" | "POST"; body?: Record<string, unknown> } = {},
): Promise<T> {
  const apiDomain = useRuntimeConfig().public.apiDomain as string;
  return $fetch<T>(`https://${apiDomain}${path}`, {
    method: options.method || "GET",
    body: options.body,
    credentials: "include",
  });
}

export function hostedConnectCommand(server: HostedServer): string | null {
  if (!server.host || !server.port) {
    return null;
  }
  const password = server.connect_password
    ? `; password ${server.connect_password}`
    : "";
  return `connect ${server.host}:${server.port}${password}`;
}

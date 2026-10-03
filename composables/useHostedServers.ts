export type HostedPlan = {
  id: string;
  title: string;
  description: string;
  price_irr: number;
  image_url: string | null;
  hosted_slots: number;
  duration: string;
};

export type HostedOverview = {
  enabled: boolean;
  available: boolean;
  remaining: number;
  plans: HostedPlan[];
  types: string[];
};

export type HostedServer = {
  id: string;
  server_id: string | null;
  owner_steam_id: string;
  owner_name: string | null;
  label: string;
  slots: number;
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
};

export type HostedAdminSettings = {
  enabled: boolean;
  node_id: string;
  max_active: number;
  reserve_match_slots: number;
  grace_days: number;
  gslt_pool_size: number;
  steam_api_key_set: boolean;
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

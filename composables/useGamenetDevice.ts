const DEVICE_KEY = "yguard-gamenet-device-id";

function randomDeviceId(): string {
  if (typeof crypto !== "undefined" && crypto.randomUUID) {
    return `ygd_${crypto.randomUUID().replace(/-/g, "")}`;
  }
  const bytes = new Uint8Array(16);
  if (typeof crypto !== "undefined" && crypto.getRandomValues) {
    crypto.getRandomValues(bytes);
  } else {
    for (let i = 0; i < bytes.length; i++) bytes[i] = Math.floor(Math.random() * 256);
  }
  return `ygd_${Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("")}`;
}

/** Stable per-browser / per-desktop-PC id used to bind a gamenet code. */
export function useGamenetDevice() {
  const deviceId = useState<string>("gamenet-device-id", () => "");

  function ensureDeviceId(): string {
    if (!import.meta.client) return deviceId.value;
    if (deviceId.value && deviceId.value.length >= 8) return deviceId.value;
    try {
      const existing = localStorage.getItem(DEVICE_KEY)?.trim() || "";
      if (existing.length >= 8) {
        deviceId.value = existing;
        return existing;
      }
    } catch {
      /* ignore */
    }
    const created = randomDeviceId();
    deviceId.value = created;
    try {
      localStorage.setItem(DEVICE_KEY, created);
    } catch {
      /* ignore */
    }
    return created;
  }

  function deviceLabel(): string {
    if (!import.meta.client) return "";
    const ua = navigator.userAgent || "";
    if (ua.includes("YGuardClient/")) return "YGuard Desktop";
    const platform = navigator.platform || "";
    return platform ? `Browser (${platform})` : "Browser";
  }

  return { deviceId, ensureDeviceId, deviceLabel };
}

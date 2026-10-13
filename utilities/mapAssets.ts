import stageRadars from "./stageRadars.json";

// The map as played: lowercased, without any path in front of it.
function playedMapName(name: string): string {
  const played = (name || "").toLowerCase().trim();
  return played.slice(played.lastIndexOf("/") + 1);
}

export function normalizeMapName(name: string): string {
  return playedMapName(name).replace(/_night$/, "");
}

// 5stack's own coloured radars, baked from their replay stage's 2D map
// (upstream scripts/replay-stage/bake-radars.mjs --webp), by image name with a
// hash of the pixels for the URL. Same calibration as /radars/metadata.json.
const STAGE_RADARS: Record<string, string> = stageRadars;

/**
 * The radar image for a map: the coloured bake when there is one (a `_night`
 * map in its moonlit palette), else the generic radar. `retina` asks for the
 * 2048px bake, for a board shown large or zoomed.
 */
export function radarImageUrl(
  mapName: string,
  options: { retina?: boolean } = {},
): string {
  const played = playedMapName(mapName);
  const day = normalizeMapName(mapName);
  for (const name of [played, day]) {
    const hash = STAGE_RADARS[name];
    if (hash) {
      return `/radars/stage/${name}${options.retina ? "@2x" : ""}.webp?v=${hash}`;
    }
  }
  return `/radars/${day}.png`;
}

/** Which picture a radar URL is, whatever its resolution. */
export function radarImageKey(src: string | null | undefined): string | null {
  return src ? src.replace(/\?.*$/, "").replace(/@2x(?=\.\w+$)/, "") : null;
}

let radarSetPromise: Promise<Set<string>> | null = null;

export function loadRadarMaps(): Promise<Set<string>> {
  if (!radarSetPromise) {
    radarSetPromise = (async () => {
      try {
        const res = await fetch("/radars/metadata.json");
        if (!res.ok) {
          return new Set<string>();
        }
        const data = await res.json();
        const set = new Set<string>();
        for (const key of Object.keys(data)) {
          if (!key.startsWith("_")) {
            set.add(key.toLowerCase());
          }
        }
        return set;
      } catch {
        return new Set<string>();
      }
    })();
  }
  return radarSetPromise;
}

export async function hasRadarForMap(mapName: string): Promise<boolean> {
  const set = await loadRadarMaps();
  return set.has(normalizeMapName(mapName));
}

const meshMemo: Record<string, Promise<boolean>> = {};

/**
 * Meshes are published gzipped and decompressed HERE rather than by the CDN.
 * Measured: a Worker's `fetch` auto-decompresses a gzip subresponse and strips
 * the header, and Cloudflare's edge only re-compresses MIME types on its
 * compressible list, which `application/octet-stream` is not -- inferno arrived
 * 18.6 MB instead of 2.4 MB. Same shape as the replay blob (`fetchReplayBlob`).
 */
export const MESH_EXT = ".tri.gz";

export function meshUrlForMap(cdn: string, mapName: string): string | null {
  const norm = normalizeMapName(mapName);
  return cdn && norm ? `${cdn}/${norm}${MESH_EXT}` : null;
}

export async function fetchMeshBuffer(url: string): Promise<ArrayBuffer> {
  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(String(response.status));
  }
  if (!response.body) {
    throw new Error("mesh response had no body");
  }

  return await new Response(
    response.body.pipeThrough(new DecompressionStream("gzip")),
  ).arrayBuffer();
}

export function hasMeshForMap(cdn: string, mapName: string): Promise<boolean> {
  const norm = normalizeMapName(mapName);
  if (!cdn || !norm) {
    return Promise.resolve(false);
  }
  if (!meshMemo[norm]) {
    meshMemo[norm] = (async () => {
      const cacheKey = `mesh-asset:v1:${cdn}:${norm}`;
      try {
        const cached = localStorage.getItem(cacheKey);
        if (cached === "1") {
          return true;
        }
        if (cached === "0") {
          return false;
        }
      } catch {}
      let result: boolean | null = null;
      for (let attempt = 0; attempt < 3 && result === null; attempt++) {
        try {
          const res = await fetch(`${cdn}/${norm}${MESH_EXT}`, { method: "HEAD" });
          if (res.status === 404) {
            result = false;
          } else if (res.ok) {
            result = true;
          }
        } catch {
          result = null;
        }
      }
      if (result !== null) {
        try {
          localStorage.setItem(cacheKey, result ? "1" : "0");
        } catch {}
      }
      return result === true;
    })();
  }
  return meshMemo[norm];
}

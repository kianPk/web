<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import { ArrowDown } from "lucide-vue-next";
import { Button } from "@/components/ui/button";

const canvasEl = ref<HTMLCanvasElement | null>(null);
const hostEl = ref<HTMLElement | null>(null);

let disposeScene: (() => void) | undefined;

// Real CS2 inspect shots (Getaway Sally + Special Agent Ava), cropped.
const AGENTS = [
  { src: "/img/servers/ava.jpg", x: -1.35, z: 0.15, scale: 2.05, yaw: 0.14 },
  { src: "/img/servers/sally.jpg", x: 1.15, z: 0.45, scale: 2.2, yaw: -0.12 },
] as const;

onMounted(async () => {
  const canvas = canvasEl.value;
  const host = hostEl.value;
  if (!canvas || !host) return;

  const THREE = await import("three");
  const reduceMotion = window.matchMedia(
    "(prefers-reduced-motion: reduce)",
  ).matches;
  disposeScene = await mountAgentsScene(THREE, canvas, host, { reduceMotion });
});

onBeforeUnmount(() => {
  disposeScene?.();
  disposeScene = undefined;
});

function scrollToModes() {
  document.getElementById("server-modes")?.scrollIntoView({
    behavior: "smooth",
    block: "start",
  });
}

type ThreeNs = typeof import("three");

async function mountAgentsScene(
  THREE: ThreeNs,
  canvas: HTMLCanvasElement,
  host: HTMLElement,
  options: { reduceMotion: boolean },
) {
  const renderer = new THREE.WebGLRenderer({
    canvas,
    antialias: true,
    alpha: true,
    powerPreference: "high-performance",
  });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.75));
  renderer.setClearColor(0x000000, 0);
  renderer.outputColorSpace = THREE.SRGBColorSpace;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;

  const scene = new THREE.Scene();
  scene.fog = new THREE.FogExp2(0x07090c, 0.055);

  const camera = new THREE.PerspectiveCamera(30, 1, 0.1, 40);
  camera.position.set(0.15, 1.35, 5.4);

  scene.add(new THREE.AmbientLight(0xb8c4d4, 0.55));
  const key = new THREE.DirectionalLight(0xfff1d6, 1.1);
  key.position.set(2.8, 4.2, 3.5);
  scene.add(key);
  const rim = new THREE.DirectionalLight(0xe3d39a, 0.7);
  rim.position.set(-3.5, 2.2, -1.5);
  scene.add(rim);

  const ground = new THREE.Mesh(
    new THREE.CircleGeometry(7, 48),
    new THREE.MeshStandardMaterial({
      color: 0x0a0d12,
      roughness: 1,
      metalness: 0,
    }),
  );
  ground.rotation.x = -Math.PI / 2;
  scene.add(ground);

  const loader = new THREE.TextureLoader();
  const boards: Array<{
    mesh: InstanceType<typeof THREE.Mesh>;
    baseX: number;
    baseY: number;
    phase: number;
  }> = [];
  const disposables: Array<{ dispose: () => void }> = [
    ground.geometry,
    ground.material as { dispose: () => void },
  ];

  await Promise.all(
    AGENTS.map(async (agent, index) => {
      const texture = await loader.loadAsync(agent.src);
      texture.colorSpace = THREE.SRGBColorSpace;
      texture.anisotropy = Math.min(8, renderer.capabilities.getMaxAnisotropy());
      disposables.push(texture);

      const aspect = texture.image
        ? texture.image.width / Math.max(1, texture.image.height)
        : 0.75;
      const boardH = agent.scale;
      const boardW = boardH * aspect;
      const geo = new THREE.PlaneGeometry(boardW, boardH);
      const mat = new THREE.MeshBasicMaterial({
        map: texture,
        transparent: true,
        depthWrite: false,
      });
      disposables.push(geo, mat);

      const mesh = new THREE.Mesh(geo, mat);
      const baseY = boardH * 0.48;
      mesh.position.set(agent.x, baseY, agent.z);
      mesh.rotation.y = agent.yaw;
      scene.add(mesh);
      boards.push({
        mesh,
        baseX: agent.x,
        baseY,
        phase: index * 1.7,
      });
    }),
  );

  const dustCount = 36;
  const dustGeo = new THREE.BufferGeometry();
  const dustPos = new Float32Array(dustCount * 3);
  for (let i = 0; i < dustCount; i++) {
    dustPos[i * 3] = (Math.random() - 0.5) * 5.5;
    dustPos[i * 3 + 1] = 0.5 + Math.random() * 2.2;
    dustPos[i * 3 + 2] = (Math.random() - 0.5) * 3.5;
  }
  dustGeo.setAttribute("position", new THREE.BufferAttribute(dustPos, 3));
  const dustMat = new THREE.PointsMaterial({
    color: 0xe3d39a,
    size: 0.03,
    transparent: true,
    opacity: 0.45,
    depthWrite: false,
  });
  const dust = new THREE.Points(dustGeo, dustMat);
  scene.add(dust);
  disposables.push(dustGeo, dustMat);

  let width = 0;
  let height = 0;
  let visible = true;
  let raf = 0;
  const clock = new THREE.Clock();

  function resize() {
    const nextW = Math.max(1, host.clientWidth);
    const nextH = Math.max(1, host.clientHeight);
    if (nextW === width && nextH === height) return;
    width = nextW;
    height = nextH;
    renderer.setSize(width, height, false);
    camera.aspect = width / height;
    camera.updateProjectionMatrix();
    if (options.reduceMotion) frame(false);
  }

  const ro = new ResizeObserver(resize);
  ro.observe(host);
  resize();

  const io = new IntersectionObserver(
    ([entry]) => {
      visible = !!entry?.isIntersecting;
      if (visible && !options.reduceMotion && !raf) {
        raf = requestAnimationFrame(tick);
      }
    },
    { threshold: 0.05 },
  );
  io.observe(host);

  function frame(animate: boolean) {
    const t = animate ? clock.getElapsedTime() : 0;
    camera.position.x = 0.12 + (animate ? Math.sin(t * 0.2) * 0.28 : 0);
    camera.position.y = 1.35 + (animate ? Math.sin(t * 0.27) * 0.05 : 0);
    camera.lookAt(0.05, 1.05, 0);

    if (animate) {
      for (const board of boards) {
        board.mesh.position.y =
          board.baseY + Math.sin(t * 1.35 + board.phase) * 0.018;
        board.mesh.position.x =
          board.baseX + Math.sin(t * 0.45 + board.phase) * 0.03;
      }
      dust.rotation.y = t * 0.035;
      const positions = dustGeo.getAttribute("position");
      for (let i = 0; i < dustCount; i++) {
        const y = positions.getY(i) + 0.002;
        positions.setY(i, y > 2.6 ? 0.4 + Math.random() * 0.3 : y);
      }
      positions.needsUpdate = true;
    }

    renderer.render(scene, camera);
  }

  function tick() {
    raf = 0;
    if (!visible || options.reduceMotion) return;
    frame(true);
    raf = requestAnimationFrame(tick);
  }

  if (options.reduceMotion) frame(false);
  else raf = requestAnimationFrame(tick);

  return () => {
    cancelAnimationFrame(raf);
    ro.disconnect();
    io.disconnect();
    for (const item of disposables) item.dispose();
    renderer.dispose();
  };
}
</script>

<template>
  <section
    ref="hostEl"
    class="servers-agents-hero relative isolate flex min-h-[340px] flex-col overflow-hidden rounded-2xl bg-[#07090c] ring-1 ring-white/5 md:min-h-[460px]"
  >
    <canvas
      ref="canvasEl"
      class="pointer-events-none absolute inset-0 h-full w-full"
      aria-hidden="true"
    />

    <div
      class="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_65%_80%_at_72%_55%,transparent_0%,rgba(7,9,12,0.25)_40%,rgba(7,9,12,0.92)_100%)] rtl:bg-[radial-gradient(ellipse_65%_80%_at_28%_55%,transparent_0%,rgba(7,9,12,0.25)_40%,rgba(7,9,12,0.92)_100%)]"
      aria-hidden="true"
    />
    <div
      class="pointer-events-none absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-[#07090c] via-[#07090c]/60 to-transparent"
      aria-hidden="true"
    />
    <div
      class="pointer-events-none absolute inset-y-0 start-0 w-[52%] bg-gradient-to-r from-[#07090c] via-[#07090c]/85 to-transparent rtl:bg-gradient-to-l"
      aria-hidden="true"
    />

    <div
      class="relative z-10 flex flex-1 flex-col justify-end gap-5 p-6 md:max-w-xl md:justify-center md:p-10"
    >
      <p
        class="font-[Oxanium] text-sm font-semibold uppercase tracking-[0.28em] text-[#e3d39a]"
      >
        YGuard
      </p>
      <div class="space-y-3">
        <h1
          class="font-[Oxanium] text-3xl font-bold leading-[1.1] tracking-tight text-white md:text-5xl"
        >
          {{ $t("pages.servers.hero_title") }}
        </h1>
        <p class="max-w-md text-sm leading-relaxed text-white/75 md:text-base">
          {{ $t("pages.servers.subtitle") }}
        </p>
      </div>
      <div>
        <Button
          type="button"
          class="gap-2 bg-[#e3d39a] text-[#14110a] hover:bg-[#f0e4b8]"
          @click="scrollToModes"
        >
          {{ $t("pages.servers.hero_cta") }}
          <ArrowDown class="h-4 w-4" />
        </Button>
      </div>
    </div>
  </section>
</template>

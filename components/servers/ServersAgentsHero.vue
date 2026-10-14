<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import { ArrowDown } from "lucide-vue-next";
import { Button } from "@/components/ui/button";

const canvasEl = ref<HTMLCanvasElement | null>(null);
const hostEl = ref<HTMLElement | null>(null);

let disposeScene: (() => void) | undefined;

onMounted(async () => {
  const canvas = canvasEl.value;
  const host = hostEl.value;
  if (!canvas || !host) return;

  const THREE = await import("three");
  const reduceMotion = window.matchMedia(
    "(prefers-reduced-motion: reduce)",
  ).matches;
  disposeScene = mountAgentsScene(THREE, canvas, host, { reduceMotion });
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

function mountAgentsScene(
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
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
  renderer.setClearColor(0x000000, 0);
  renderer.outputColorSpace = THREE.SRGBColorSpace;

  const scene = new THREE.Scene();
  scene.fog = new THREE.FogExp2(0x07090c, 0.045);

  const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 80);
  camera.position.set(0.2, 1.55, 6.2);

  const ambient = new THREE.AmbientLight(0x9aa8b8, 0.35);
  scene.add(ambient);

  const key = new THREE.DirectionalLight(0xfff2d6, 1.35);
  key.position.set(3.5, 5.5, 4);
  scene.add(key);

  const rim = new THREE.DirectionalLight(0xe3d39a, 0.85);
  rim.position.set(-4, 2.5, -2);
  scene.add(rim);

  const fill = new THREE.DirectionalLight(0x4d6d9a, 0.45);
  fill.position.set(-2, 1.2, 3);
  scene.add(fill);

  const ground = new THREE.Mesh(
    new THREE.CircleGeometry(8, 48),
    new THREE.MeshStandardMaterial({
      color: 0x0c1016,
      roughness: 0.95,
      metalness: 0.05,
    }),
  );
  ground.rotation.x = -Math.PI / 2;
  ground.position.y = 0;
  scene.add(ground);

  const grid = new THREE.GridHelper(10, 20, 0xe3d39a, 0x1a222c);
  grid.position.y = 0.01;
  const gridMats = Array.isArray(grid.material) ? grid.material : [grid.material];
  for (const mat of gridMats) {
    mat.transparent = true;
    mat.opacity = 0.22;
  }
  scene.add(grid);

  const agents = [
    buildOperator(THREE, "ct", { x: -1.55, z: 0.35, yaw: 0.35, scale: 0.92 }),
    buildOperator(THREE, "t", { x: 0.15, z: 0.7, yaw: -0.15, scale: 1 }),
    buildOperator(THREE, "ct", { x: 1.7, z: 0.1, yaw: -0.45, scale: 0.88 }),
  ];
  for (const agent of agents) scene.add(agent.root);

  // Soft dust motes drifting through the rim light.
  const dustCount = 48;
  const dustGeo = new THREE.BufferGeometry();
  const dustPos = new Float32Array(dustCount * 3);
  for (let i = 0; i < dustCount; i++) {
    dustPos[i * 3] = (Math.random() - 0.5) * 6;
    dustPos[i * 3 + 1] = 0.4 + Math.random() * 2.4;
    dustPos[i * 3 + 2] = (Math.random() - 0.5) * 4;
  }
  dustGeo.setAttribute("position", new THREE.BufferAttribute(dustPos, 3));
  const dust = new THREE.Points(
    dustGeo,
    new THREE.PointsMaterial({
      color: 0xe3d39a,
      size: 0.035,
      transparent: true,
      opacity: 0.55,
      depthWrite: false,
    }),
  );
  scene.add(dust);

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

    camera.position.x = 0.15 + (animate ? Math.sin(t * 0.22) * 0.35 : 0);
    camera.position.y = 1.55 + (animate ? Math.sin(t * 0.31) * 0.06 : 0);
    camera.lookAt(0.1, 1.05, 0);

    if (animate) {
      for (const agent of agents) {
        const breath = Math.sin(t * 1.6 + agent.phase) * 0.012;
        agent.root.position.y = breath;
        agent.root.rotation.y =
          agent.baseYaw + Math.sin(t * 0.55 + agent.phase) * 0.04;
        if (agent.head) {
          agent.head.rotation.y = Math.sin(t * 0.7 + agent.phase) * 0.08;
        }
      }

      dust.rotation.y = t * 0.04;
      const positions = dustGeo.getAttribute("position");
      for (let i = 0; i < dustCount; i++) {
        const y = positions.getY(i) + 0.0025;
        positions.setY(i, y > 2.8 ? 0.35 + Math.random() * 0.4 : y);
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

  if (options.reduceMotion) {
    frame(false);
  } else {
    raf = requestAnimationFrame(tick);
  }

  return () => {
    cancelAnimationFrame(raf);
    ro.disconnect();
    io.disconnect();
    dustGeo.dispose();
    (dust.material as InstanceType<typeof THREE.PointsMaterial>).dispose();
    ground.geometry.dispose();
    (ground.material as InstanceType<typeof THREE.MeshStandardMaterial>).dispose();
    for (const agent of agents) agent.dispose();
    renderer.dispose();
  };
}

type AgentHandle = {
  root: InstanceType<typeof import("three").Group>;
  head: InstanceType<typeof import("three").Object3D> | null;
  baseYaw: number;
  phase: number;
  dispose: () => void;
};

function buildOperator(
  THREE: ThreeNs,
  side: "ct" | "t",
  opts: { x: number; z: number; yaw: number; scale: number },
): AgentHandle {
  const isCt = side === "ct";
  const suit = new THREE.MeshStandardMaterial({
    color: isCt ? 0x3d4f63 : 0x5c4a38,
    roughness: 0.72,
    metalness: 0.18,
  });
  const accent = new THREE.MeshStandardMaterial({
    color: isCt ? 0x6f879e : 0xc4a574,
    roughness: 0.55,
    metalness: 0.35,
  });
  const dark = new THREE.MeshStandardMaterial({
    color: 0x1a1d22,
    roughness: 0.85,
    metalness: 0.2,
  });
  const gun = new THREE.MeshStandardMaterial({
    color: 0x2a2e33,
    roughness: 0.4,
    metalness: 0.65,
  });

  const disposables: Array<{ dispose: () => void }> = [
    suit,
    accent,
    dark,
    gun,
  ];
  const track = <T extends InstanceType<typeof THREE.BufferGeometry>>(g: T) => {
    disposables.push(g);
    return g;
  };

  const root = new THREE.Group();
  root.position.set(opts.x, 0, opts.z);
  root.rotation.y = opts.yaw;
  root.scale.setScalar(opts.scale);

  const bootsL = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.22, 0.18, 0.34)),
    dark,
  );
  bootsL.position.set(-0.14, 0.09, 0.02);
  const bootsR = bootsL.clone();
  bootsR.position.x = 0.14;
  root.add(bootsL, bootsR);

  const legL = new THREE.Mesh(
    track(new THREE.CylinderGeometry(0.1, 0.12, 0.55, 8)),
    suit,
  );
  legL.position.set(-0.14, 0.45, 0);
  const legR = legL.clone();
  legR.position.x = 0.14;
  root.add(legL, legR);

  const hips = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.42, 0.22, 0.28)),
    suit,
  );
  hips.position.set(0, 0.78, 0);
  root.add(hips);

  const torso = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.48, 0.62, 0.3)),
    suit,
  );
  torso.position.set(0, 1.18, 0);
  root.add(torso);

  const vest = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.5, 0.42, 0.34)),
    accent,
  );
  vest.position.set(0, 1.22, 0.02);
  root.add(vest);

  const pouch = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.16, 0.12, 0.1)),
    dark,
  );
  pouch.position.set(0.18, 1.05, 0.2);
  root.add(pouch);

  const shoulderL = new THREE.Mesh(
    track(new THREE.SphereGeometry(0.12, 10, 10)),
    suit,
  );
  shoulderL.position.set(-0.3, 1.42, 0);
  const shoulderR = shoulderL.clone();
  shoulderR.position.x = 0.3;
  root.add(shoulderL, shoulderR);

  const armL = new THREE.Mesh(
    track(new THREE.CylinderGeometry(0.07, 0.08, 0.48, 8)),
    suit,
  );
  armL.position.set(-0.38, 1.12, 0.12);
  armL.rotation.z = 0.35;
  armL.rotation.x = -0.55;
  root.add(armL);

  const armR = new THREE.Mesh(
    track(new THREE.CylinderGeometry(0.07, 0.08, 0.42, 8)),
    suit,
  );
  armR.position.set(0.34, 1.18, 0.18);
  armR.rotation.z = -0.85;
  armR.rotation.x = -1.15;
  root.add(armR);

  const neck = new THREE.Mesh(
    track(new THREE.CylinderGeometry(0.08, 0.09, 0.12, 8)),
    suit,
  );
  neck.position.set(0, 1.55, 0);
  root.add(neck);

  const headGroup = new THREE.Group();
  headGroup.position.set(0, 1.72, 0);
  const helmet = new THREE.Mesh(
    track(new THREE.SphereGeometry(0.2, 14, 12)),
    dark,
  );
  helmet.scale.set(1, 0.95, 1.05);
  headGroup.add(helmet);
  const visor = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.28, 0.08, 0.12)),
    new THREE.MeshStandardMaterial({
      color: isCt ? 0x7ec8e3 : 0x2a241c,
      roughness: 0.25,
      metalness: 0.7,
      emissive: isCt ? 0x1a4050 : 0x000000,
      emissiveIntensity: isCt ? 0.25 : 0,
    }),
  );
  disposables.push(visor.material as InstanceType<typeof THREE.Material>);
  visor.position.set(0, 0.02, 0.14);
  headGroup.add(visor);
  root.add(headGroup);

  // Compact rifle silhouette held across the chest.
  const rifle = new THREE.Group();
  rifle.position.set(0.05, 1.2, 0.32);
  rifle.rotation.set(-0.15, 0.1, -0.05);
  const receiver = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.12, 0.12, 0.55)),
    gun,
  );
  const barrel = new THREE.Mesh(
    track(new THREE.CylinderGeometry(0.025, 0.03, 0.55, 8)),
    gun,
  );
  barrel.rotation.x = Math.PI / 2;
  barrel.position.z = 0.5;
  const stock = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.08, 0.14, 0.22)),
    dark,
  );
  stock.position.set(0, -0.02, -0.35);
  const mag = new THREE.Mesh(
    track(new THREE.BoxGeometry(0.08, 0.18, 0.1)),
    dark,
  );
  mag.position.set(0, -0.12, 0.05);
  rifle.add(receiver, barrel, stock, mag);
  root.add(rifle);

  return {
    root,
    head: headGroup,
    baseYaw: opts.yaw,
    phase: Math.random() * Math.PI * 2,
    dispose: () => {
      for (const item of disposables) item.dispose();
    },
  };
}
</script>

<template>
  <section
    ref="hostEl"
    class="servers-agents-hero relative isolate flex min-h-[340px] flex-col overflow-hidden rounded-2xl bg-[#07090c] ring-1 ring-white/5 md:min-h-[440px]"
  >
    <canvas
      ref="canvasEl"
      class="pointer-events-none absolute inset-0 h-full w-full"
      aria-hidden="true"
    />

    <div
      class="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_70%_80%_at_75%_55%,transparent_0%,rgba(7,9,12,0.35)_45%,rgba(7,9,12,0.92)_100%)] rtl:bg-[radial-gradient(ellipse_70%_80%_at_25%_55%,transparent_0%,rgba(7,9,12,0.35)_45%,rgba(7,9,12,0.92)_100%)]"
      aria-hidden="true"
    />
    <div
      class="pointer-events-none absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-[#07090c] via-[#07090c]/55 to-transparent"
      aria-hidden="true"
    />
    <div
      class="pointer-events-none absolute inset-y-0 start-0 w-[55%] bg-gradient-to-r from-[#07090c] via-[#07090c]/80 to-transparent rtl:bg-gradient-to-l"
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

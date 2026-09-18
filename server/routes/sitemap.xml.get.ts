import { SITE_URL } from "~/utils/siteSeo";

const PATHS = ["/", "/cs2", "/anticheat", "/servers", "/login"];

export default defineEventHandler((event) => {
  setHeader(event, "Content-Type", "application/xml; charset=utf-8");
  setHeader(event, "Cache-Control", "public, max-age=3600");

  const lastmod = new Date().toISOString().slice(0, 10);
  const urls = PATHS.map((path) => {
    const loc = path === "/" ? SITE_URL : `${SITE_URL}${path}`;
    const priority =
      path === "/" ? "1.0" : path === "/cs2" || path === "/anticheat" ? "0.9" : "0.7";
    return `  <url>
    <loc>${loc}</loc>
    <lastmod>${lastmod}</lastmod>
    <changefreq>daily</changefreq>
    <priority>${priority}</priority>
  </url>`;
  }).join("\n");

  return `<?xml version="1.0" encoding="UTF-8"?>
<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
${urls}
</urlset>
`;
});

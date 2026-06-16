const CACHE_NAME = "chitieu-shell-v5";
const SHELL_ASSETS = [
  "/css/app.css",
  "/js/location.js",
  "/js/pwa.js",
  "/manifest.webmanifest",
  "/icons/app-icon-180.png",
  "/icons/app-icon-192.png",
  "/icons/app-icon-512.png"
];

self.addEventListener("install", event => {
  event.waitUntil(
    caches.open(CACHE_NAME).then(cache =>
      Promise.allSettled(SHELL_ASSETS.map(asset => cache.add(asset)))
    )
  );
  self.skipWaiting();
});

self.addEventListener("activate", event => {
  event.waitUntil(
    caches.keys().then(keys => Promise.all(
      keys.filter(key => key !== CACHE_NAME).map(key => caches.delete(key))
    ))
  );
  self.clients.claim();
});

self.addEventListener("fetch", event => {
  const request = event.request;
  if (request.method !== "GET") return;

  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;
  if (url.protocol !== "http:" && url.protocol !== "https:") return;
  if (url.pathname.startsWith("/api") || url.pathname.startsWith("/_blazor")) return;
  if (request.mode === "navigate" || request.destination === "document") return;
  if (url.pathname.startsWith("/account")) return;
  if (!shouldCache(url.pathname)) return;

  event.respondWith(
    (async () => {
      try {
        const response = await fetch(request);
        if (!response || !response.ok) return response;
        const copy = response.clone();
        caches.open(CACHE_NAME).then(cache => cache.put(request, copy)).catch(() => {});
        return response;
      } catch {
        const cached = await caches.match(request);
        return cached || Response.error();
      }
    })()
  );
});

function shouldCache(pathname) {
  return SHELL_ASSETS.includes(pathname)
    || pathname.startsWith("/react/assets/")
    || pathname.startsWith("/icons/");
}

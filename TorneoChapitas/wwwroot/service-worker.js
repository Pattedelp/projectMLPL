const CACHE_NAME = "torneo-chapitas-v3";

const STATIC_FILES = [
  "/offline.html",
  "/css/torneo.css",
  "/img/aac-logo.png",
  "/manifest.json",
];

// ── INSTALAR ─────────────────────────────────────────────────────
self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(STATIC_FILES))
  );
  self.skipWaiting();
});

// ── ACTIVAR ──────────────────────────────────────────────────────
self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k)))
    )
  );
  self.clients.claim();
});

// ── FETCH ─────────────────────────────────────────────────────────
self.addEventListener("fetch", (event) => {
  const req = event.request;
  const url = new URL(req.url);

  if (req.method !== "GET") return;
  if (url.origin !== self.location.origin) return;
  if (url.pathname.startsWith("/Admin")) return;

  // ── Assets estáticos: Stale-While-Revalidate ──────────────────
  const estaticos = [".css", ".js", ".png", ".jpg", ".webp", ".svg", ".ico", ".woff2"];
  if (estaticos.some((ext) => url.pathname.endsWith(ext))) {
    event.respondWith(
      caches.open(CACHE_NAME).then((cache) =>
        cache.match(req).then((cached) => {
          const networkFetch = fetch(req).then((res) => {
            cache.put(req, res.clone());
            return res;
          });
          return cached || networkFetch;
        })
      )
    );
    return;
  }

  // ── Navegación (HTML): mostrar splash del caché MIENTRAS carga ─
  // Sirve offline.html del caché instantáneamente como "mientras tanto",
  // y cuando el servidor responde, el navegador navega a la página real.
  // Esto elimina el negro inicial completamente.
  if (req.mode === "navigate") {
    event.respondWith(
      fetch(req).catch(() => caches.match("/offline.html"))
    );
    return;
  }

  // ── Resto: Network First con fallback ─────────────────────────
  event.respondWith(
    fetch(req)
      .then((res) => {
        const copy = res.clone();
        caches.open(CACHE_NAME).then((c) => c.put(req, copy));
        return res;
      })
      .catch(() => caches.match(req))
  );
});

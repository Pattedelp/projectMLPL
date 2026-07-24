const CACHE_NAME = "torneo-chapitas-v2";

// Assets estáticos que se precargan al instalar
const STATIC_FILES = [
  "/",
  "/css/torneo.css",
  "/img/aac-logo.png",
  "/manifest.json",
  "/offline.html",
];

// ── INSTALAR ────────────────────────────────────────────────────
self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME)
      .then((cache) => cache.addAll(STATIC_FILES))
      .catch(() => {}) // No frenar si algún estático falla
  );
  self.skipWaiting();
});

// ── ACTIVAR ─────────────────────────────────────────────────────
self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches.keys().then((keys) =>
      Promise.all(
        keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k))
      )
    )
  );
  self.clients.claim();
});

// ── FETCH ────────────────────────────────────────────────────────
self.addEventListener("fetch", (event) => {
  const req = event.request;
  const url = new URL(req.url);

  // Solo GET y solo origen propio
  if (req.method !== "GET") return;
  if (url.origin !== self.location.origin) return;

  // Admin nunca se cachea
  if (url.pathname.startsWith("/Admin")) return;

  // ── Páginas dinámicas: Network First ─────────────────────────
  // Traer siempre del servidor; caché solo como fallback sin señal
  const dinamicas = [
    "/Noticias", "/Estadisticas", "/Equipos", "/Copas",
    "/Historial", "/Predicciones", "/Ranking", "/api/"
  ];
  if (dinamicas.some((p) => url.pathname.startsWith(p))) {
    event.respondWith(
      fetch(req)
        .then((res) => {
          // Guardar una copia fresca en caché para el fallback
          const copy = res.clone();
          caches.open(CACHE_NAME).then((c) => c.put(req, copy));
          return res;
        })
        .catch(() => caches.match(req))
    );
    return;
  }

  // ── Assets estáticos: Stale-While-Revalidate ──────────────────
  // Sirve del caché inmediatamente Y actualiza en segundo plano.
  // El usuario ve carga instantánea; la próxima visita trae lo nuevo.
  const estaticos = [".css", ".js", ".png", ".jpg", ".webp", ".svg", ".ico", ".woff2"];
  if (estaticos.some((ext) => url.pathname.endsWith(ext))) {
    event.respondWith(
      caches.open(CACHE_NAME).then((cache) =>
        cache.match(req).then((cached) => {
          const networkFetch = fetch(req).then((res) => {
            cache.put(req, res.clone());
            return res;
          });
          // Si hay caché, servirla ahora y actualizar en background
          return cached || networkFetch;
        })
      )
    );
    return;
  }

  // ── Home y rutas principales: Network First con fallback ──────
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

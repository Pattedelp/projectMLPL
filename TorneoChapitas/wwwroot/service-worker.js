const CACHE_NAME = "torneo-chapitas-v4";

const STATIC_FILES = [
  "/offline.html",
  "/css/torneo.css",
  "/img/aac-logo.png",
  "/manifest.json",
];

// ── INSTALAR ─────────────────────────────────────────────────────
self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(STATIC_FILES)),
  );
  self.skipWaiting();
});

// ── ACTIVAR ──────────────────────────────────────────────────────
self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) =>
        Promise.all(
          keys.filter((k) => k !== CACHE_NAME).map((k) => caches.delete(k)),
        ),
      ),
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
  const estaticos = [
    ".css",
    ".js",
    ".png",
    ".jpg",
    ".webp",
    ".svg",
    ".ico",
    ".woff2",
  ];
  if (estaticos.some((ext) => url.pathname.endsWith(ext))) {
    event.respondWith(
      caches.open(CACHE_NAME).then((cache) =>
        cache.match(req).then((cached) => {
          const networkFetch = fetch(req).then((res) => {
            cache.put(req, res.clone());
            return res;
          });
          return cached || networkFetch;
        }),
      ),
    );
    return;
  }

  // ── Navegación (HTML): mostrar splash del caché MIENTRAS carga ─
  // Sirve offline.html del caché instantáneamente como "mientras tanto",
  // y cuando el servidor responde, el navegador navega a la página real.
  // Esto elimina el negro inicial completamente.
  if (req.mode === "navigate") {
    event.respondWith(fetch(req).catch(() => caches.match("/offline.html")));
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
      .catch(() => caches.match(req)),
  );
});

// ── AGREGAR ESTO AL FINAL DE tu service-worker.js ──────────────────────────
// Maneja las notificaciones push entrantes y el click en ellas.

self.addEventListener("push", function (event) {
  if (!event.data) return;

  let data;
  try {
    data = event.data.json();
  } catch {
    data = { titulo: "Torneo Chapitas", cuerpo: event.data.text(), url: "/" };
  }

  event.waitUntil(
    self.registration.showNotification(data.titulo || "Torneo Chapitas", {
      body: data.cuerpo || "",
      icon: "/img/aac-logo.png",
      badge: "/icons/icon-192.png",
      data: { url: data.url || "/Noticias" },
      vibrate: [200, 100, 200],
    }),
  );
});

self.addEventListener("notificationclick", function (event) {
  event.notification.close();
  const url = event.notification.data?.url || "/Noticias";
  event.waitUntil(
    clients
      .matchAll({ type: "window", includeUncontrolled: true })
      .then(function (windowClients) {
        // Si ya hay una ventana abierta, enfocarla y navegar
        for (const client of windowClients) {
          if ("focus" in client) {
            client.navigate(url);
            return client.focus();
          }
        }
        // Si no hay ventana, abrir una nueva
        return clients.openWindow(url);
      }),
  );
});

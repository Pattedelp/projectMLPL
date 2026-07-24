const CACHE_NAME = "torneo-chapitas-v1";

const STATIC_FILES = [
  "/",
  "/css/site.css",
  "/img/aac-logo.png",
  "/manifest.json",
  "/offline.html",
];

// Instalar
self.addEventListener("install", (event) => {
  event.waitUntil(
    caches.open(CACHE_NAME).then((cache) => cache.addAll(STATIC_FILES)),
  );

  self.skipWaiting();
});

// Activar
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

// Interceptar requests
self.addEventListener("fetch", (event) => {
  const request = event.request;

  // Solo GET
  if (request.method !== "GET") return;

  // No cachear API ni páginas dinámicas
  if (
    request.url.includes("/Noticias") ||
    request.url.includes("/Estadisticas") ||
    request.url.includes("/Equipos") ||
    request.url.includes("/Torneo")
  ) {
    event.respondWith(fetch(request).catch(() => caches.match(request)));

    return;
  }

  // Recursos estáticos: Cache First
  event.respondWith(
    caches.match(request).then((response) => {
      return (
        response ||
        fetch(request).then((networkResponse) => {
          const copy = networkResponse.clone();

          caches.open(CACHE_NAME).then((cache) => cache.put(request, copy));

          return networkResponse;
        })
      );
    }),
  );
});

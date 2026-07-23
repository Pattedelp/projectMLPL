// Torneo Chapitas — Service Worker
// Estrategia: network-first. La app es dinámica (tablas, resultados),
// así que siempre se intenta traer lo fresco y el caché es solo el
// salvavidas para cuando no hay señal.

const CACHE = 'chapitas-v1';
const PRECARGA = ['/', '/manifest.json'];

self.addEventListener('install', evt => {
    evt.waitUntil(
        caches.open(CACHE)
            .then(c => c.addAll(PRECARGA))
            .then(() => self.skipWaiting())
            .catch(() => self.skipWaiting())
    );
});

self.addEventListener('activate', evt => {
    evt.waitUntil(
        caches.keys()
            .then(claves => Promise.all(
                claves.filter(k => k !== CACHE).map(k => caches.delete(k))
            ))
            .then(() => self.clients.claim())
    );
});

self.addEventListener('fetch', evt => {
    const req = evt.request;

    // Solo GET y solo del propio sitio
    if (req.method !== 'GET') return;
    if (!req.url.startsWith(self.location.origin)) return;

    // Las llamadas del admin nunca se cachean
    if (req.url.includes('/Admin/')) return;

    evt.respondWith(
        fetch(req)
            .then(res => {
                if (res && res.status === 200 && res.type === 'basic') {
                    const copia = res.clone();
                    caches.open(CACHE).then(c => c.put(req, copia)).catch(() => {});
                }
                return res;
            })
            .catch(() =>
                caches.match(req).then(cacheado =>
                    cacheado || caches.match('/')
                )
            )
    );
});

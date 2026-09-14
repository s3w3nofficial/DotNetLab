// In development, drop any previously installed worker and its caches so
// stale fingerprinted _framework files cannot 404 and fail SRI on boot.
self.addEventListener('install', function () {
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil((async function () {
        var keys = await caches.keys();
        await Promise.all(keys.map(function (key) { return caches.delete(key); }));
        await self.registration.unregister();
        var clients = await self.clients.matchAll({ type: 'window' });
        for (var i = 0; i < clients.length; i++) {
            clients[i].navigate(clients[i].url);
        }
    })());
});

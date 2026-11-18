// The threaded .NET export needs isolation headers. Scope them to this game.
const scope = new URL(self.registration.scope);

self.addEventListener('install', event => event.waitUntil(self.skipWaiting()));
self.addEventListener('activate', event => event.waitUntil(self.clients.claim()));
self.addEventListener('fetch', event => {
  const request = event.request;
  const url = new URL(request.url);
  if (request.method !== 'GET' || url.origin !== scope.origin || !url.pathname.startsWith(scope.pathname)) return;

  event.respondWith((async () => {
    let response;
    if (request.mode === 'navigate') {
      // GitHub Pages caches HTML, so use a unique request for the latest shell.
      url.searchParams.set('_shell', crypto.randomUUID());
      response = await fetch(url, { cache: 'no-store', credentials: request.credentials });
    } else {
      response = await fetch(request);
    }
    if (response.type === 'opaque' || response.status === 0) return response;
    const headers = new Headers(response.headers);
    headers.set('Cross-Origin-Opener-Policy', 'same-origin');
    headers.set('Cross-Origin-Embedder-Policy', 'require-corp');
    return new Response(response.body, { status: response.status, statusText: response.statusText, headers });
  })());
});

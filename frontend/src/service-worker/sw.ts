/// <reference lib="webworker" />
import { precacheAndRoute } from 'workbox-precaching'

declare const self: ServiceWorkerGlobalScope

precacheAndRoute(self.__WB_MANIFEST)

self.addEventListener('push', (event: PushEvent) => {
  if (!event.data) return
  const payload = event.data.json() as { title: string; body: string; url?: string }
  event.waitUntil(
    self.registration.showNotification(payload.title, {
      body: payload.body,
      data: { url: payload.url },
      icon: '/icons/icon-192.png',
      badge: '/icons/icon-192.png',
    }),
  )
})

self.addEventListener('notificationclick', (event: NotificationEvent) => {
  event.notification.close()
  const url = (event.notification.data as { url?: string } | null)?.url ?? '/'
  event.waitUntil(self.clients.openWindow(url))
})

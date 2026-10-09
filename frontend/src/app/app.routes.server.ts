import {RenderMode, ServerRoute} from '@angular/ssr';

export const serverRoutes: ServerRoute[] = [
  {
    path: 'admin/**',
    renderMode: RenderMode.Client,
  },
  {
    // OPAC public pages: render on the server per request (SEO-friendly,
    // avoids build-time backend calls for parameterized routes like book/:id)
    path: '**',
    renderMode: RenderMode.Server,
  },
];

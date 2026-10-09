import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import {join} from 'node:path';
import { createProxyMiddleware } from 'http-proxy-middleware';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

/**
 * Proxy OPAC data/files requests (images, uploads) to the data backend.
 * Must be registered before the generic /api proxy.
 */
app.use(
  createProxyMiddleware({
    pathFilter: '/data',
    target: 'http://103.97.134.58:8089',
    changeOrigin: true,
    pathRewrite: { '^/data': '' }
  })
);

/**
 * Proxy API requests to avoid blocked:mixed-content.
 * Default target is the public/OPAC backend (8090); admin endpoints are routed to 8091.
 * OPAC public endpoints (/api/public/*) fall through to the default 8090 target.
 */
app.use(
  createProxyMiddleware({
    pathFilter: '/api',
    target: 'http://103.97.134.58:8090',
    changeOrigin: true,
    router: {
      '/api/Auth': 'http://103.97.134.58:8091',
      '/api/LinkGroup': 'http://103.97.134.58:8091',
      '/api/Class': 'http://103.97.134.58:8091',
      '/api/DboDegree': 'http://103.97.134.58:8091',
      '/api/ReaderType': 'http://103.97.134.58:8091'
    }
  })
);

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) =>
      response ? writeResponseToNodeResponse(response, res) : next(),
    )
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);

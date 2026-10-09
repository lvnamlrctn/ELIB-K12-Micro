# syntax=docker/dockerfile:1.7
# SPA Angular trong src/Web (workspace nhiều app). Build context = gốc repo.
#   docker build -f deploy/docker/web.Dockerfile --build-arg APP=admin -t elib/admin-web .
#   docker build -f deploy/docker/web.Dockerfile --build-arg APP=opac --build-arg NGINX_CONF=web.opac.nginx.conf -t elib/opac-web .

FROM node:24-alpine AS build
ARG APP
WORKDIR /web
COPY src/Web/package.json src/Web/package-lock.json ./
RUN --mount=type=cache,id=npm,target=/root/.npm npm ci --no-audit --no-fund
COPY src/Web/ ./
RUN npx ng build "${APP}" --configuration production

FROM nginxinc/nginx-unprivileged:1.29-alpine
ARG APP
ARG NGINX_CONF=web.nginx.conf
COPY deploy/docker/${NGINX_CONF} /etc/nginx/conf.d/default.conf
COPY --from=build /web/dist/${APP}/browser /usr/share/nginx/html/${APP}
EXPOSE 8080

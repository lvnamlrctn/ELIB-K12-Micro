# syntax=docker/dockerfile:1.7
# Image chung cho mọi service .NET. Build context = gốc repo (xem .dockerignore).
#   docker build -f deploy/docker/service.Dockerfile \
#     --build-arg PROJECT=Services/Identity/Elib.Identity.Api/Elib.Identity.Api.csproj \
#     --build-arg ASSEMBLY=Elib.Identity.Api -t elib/identity .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT
WORKDIR /repo
COPY global.json ./
COPY src/ src/
# sharing=locked: compose build nhiều service song song cùng cache — tránh hai tiến trình cùng giải nén một gói NuGet.
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish "src/${PROJECT}" -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG ASSEMBLY
ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_gcServer=0
WORKDIR /app
COPY --from=build /app .
# Chạy thẳng apphost, KHÔNG qua "sh -c": dash bỏ các biến môi trường có tên chứa '-'
# (vd Identity__Clients__svc-gateway__ClientSecret) khi exec tiến trình con.
RUN ln -s "/app/${ASSEMBLY}" /app/elib-service
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["/app/elib-service"]

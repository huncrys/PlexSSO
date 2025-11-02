FROM --platform=$BUILDPLATFORM node:current-alpine as react-builder
COPY ./ui /ui
WORKDIR /ui
RUN rm /usr/local/bin/yarn* && \
    npm install -g corepack@latest && \
    corepack yarn && \
    corepack yarn build

FROM mcr.microsoft.com/dotnet/sdk:10.0 as aspnet-builder
COPY ./backend /backend
WORKDIR /backend
RUN dotnet restore && \
    dotnet publish PlexSSO.sln -c Release -o build /p:CopyOutputSymbolsToPublishDirectory=false /p:DebugType=None /p:DebugSymbols=false && \
    rm build/ui/index.html
RUN mkdir -p /rootfs/config && \
    chmod 777 /rootfs/config && \
    mv -T /backend/build /rootfs/app
COPY --from=react-builder /ui/build /rootfs/app/ui

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=aspnet-builder /rootfs/ /
ENTRYPOINT ["dotnet", "PlexSSO.dll", "--config", "/config/"]
EXPOSE 4200
VOLUME [ "/config" ]
HEALTHCHECK --interval=30s --timeout=30s --start-period=5s --retries=3 CMD [ "dotnet", "PlexSSO.dll", "--healthcheck" ]

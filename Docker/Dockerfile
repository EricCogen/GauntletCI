# SPDX-License-Identifier: Elastic-2.0
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/GauntletCI.Cli/GauntletCI.Cli.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=false \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime-deps:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["/app/GauntletCI.Cli"]

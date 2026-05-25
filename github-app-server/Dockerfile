FROM mcr.microsoft.com/dotnet/sdk:8.0 AS runtime

WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl git gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs \
    && dotnet tool install -g GauntletCI --version 2.7.1 \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

ENV PATH="/root/.dotnet/tools:${PATH}"

COPY package*.json tsconfig.json ./
RUN npm ci

COPY src ./src
RUN npm run build \
    && npm prune --omit=dev

ENV NODE_ENV=production

EXPOSE 8787

CMD ["npm", "start"]

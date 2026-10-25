# Build context is the repository root: the client is generated from
# contracts/openapi.json, which sits outside frontend/.
FROM node:22-alpine AS deps
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
# Skip lifecycle scripts here: Orval needs the contract, which arrives below.
RUN npm ci --ignore-scripts

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY contracts/openapi.json /contracts/openapi.json
COPY frontend/ ./
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
COPY --from=build /app/package.json /app/package-lock.json ./
COPY --from=build /app/node_modules ./node_modules
COPY --from=build /app/build ./build

# node:alpine's node user is UID 1000, matching the chart's securityContext.
USER node
EXPOSE 3000
CMD ["npm", "run", "start"]

FROM node:20-alpine

# Install wget for health checks
RUN apk add --no-cache wget

WORKDIR /app

# Copy package files
COPY package*.json ./

# Install production dependencies
RUN npm ci --only=production

# Copy application code
COPY src ./src

# Create logs directory
RUN mkdir -p /app/logs

EXPOSE 3000

# Run as non-root user
USER node

CMD ["node", "src/server.js"]


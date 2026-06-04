import { assertRunnableConfig, loadConfig } from "./config.js";
import { createGatewayApp } from "./server.js";

const config = loadConfig();
assertRunnableConfig(config);

const app = createGatewayApp(config);
const server = app.listen(config.port, config.host, () => {
  console.log(`Sage MCP gateway listening on http://${config.host}:${config.port}/mcp`);
});

server.on("error", (error) => {
  console.error("Failed to start Sage MCP gateway", error);
  process.exitCode = 1;
});

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);

function shutdown(): void {
  server.close(() => {
    process.exit(0);
  });
}

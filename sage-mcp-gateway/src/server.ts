import crypto from "node:crypto";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { createMcpExpressApp } from "@modelcontextprotocol/sdk/server/express.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import type { Express, Request, Response, NextFunction } from "express";
import { GatewayConfig } from "./config.js";
import { registerSageTools } from "./tools.js";
import { UafClient } from "./uafClient.js";

export function createGatewayApp(config: GatewayConfig, client = new UafClient({
  baseUrl: config.sageApiUrl,
  timeoutMs: config.timeoutMs,
  keys: config.keys
})): Express {
  const app = createMcpExpressApp({ host: config.host, allowedHosts: config.allowedHosts });

  app.get("/healthz", (_req, res) => {
    res.json({ ok: true, service: "sage-mcp-gateway" });
  });

  app.get("/readyz", async (_req, res) => {
    try {
      const upstream = await client.readiness();
      res.json({ ok: true, upstream });
    } catch (error) {
      res.status(503).json({
        ok: false,
        error: error instanceof Error ? error.message : "Upstream readiness check failed"
      });
    }
  });

  app.post("/mcp", requireBearerSecret(config), async (req, res) => {
    await handleMcpRequest(config, client, req, res);
  });

  app.get("/mcp", requireBearerSecret(config), (_req, res) => {
    methodNotAllowed(res);
  });

  app.delete("/mcp", requireBearerSecret(config), (_req, res) => {
    methodNotAllowed(res);
  });

  return app;
}

function createServer(config: GatewayConfig, client: UafClient): McpServer {
  const server = new McpServer({
    name: "uaf-sage-mcp-gateway",
    version: "0.1.0"
  });

  registerSageTools(server, client, config);
  return server;
}

async function handleMcpRequest(config: GatewayConfig, client: UafClient, req: Request, res: Response): Promise<void> {
  const server = createServer(config, client);
  const transport = new StreamableHTTPServerTransport({ sessionIdGenerator: undefined });

  try {
    await server.connect(transport);
    await transport.handleRequest(req, res, req.body);
    res.on("close", () => {
      void transport.close();
      void server.close();
    });
  } catch (error) {
    console.error("MCP request failed", error);
    if (!res.headersSent) {
      res.status(500).json({
        jsonrpc: "2.0",
        error: {
          code: -32603,
          message: "Internal server error"
        },
        id: null
      });
    }
  }
}

export function requireBearerSecret(config: GatewayConfig) {
  return (req: Request, res: Response, next: NextFunction): void => {
    if (!config.sharedSecret) {
      next();
      return;
    }

    const expected = `Bearer ${config.sharedSecret}`;
    const actual = req.header("authorization") ?? "";
    if (!timingSafeEqual(actual, expected)) {
      res.setHeader("WWW-Authenticate", "Bearer");
      res.status(401).json({ error: "Unauthorized" });
      return;
    }

    next();
  };
}

function timingSafeEqual(actual: string, expected: string): boolean {
  const actualBuffer = Buffer.from(actual);
  const expectedBuffer = Buffer.from(expected);

  return actualBuffer.length === expectedBuffer.length && crypto.timingSafeEqual(actualBuffer, expectedBuffer);
}

function methodNotAllowed(res: Response): void {
  res.status(405).json({
    jsonrpc: "2.0",
    error: {
      code: -32000,
      message: "Method not allowed."
    },
    id: null
  });
}

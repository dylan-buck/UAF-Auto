export interface GatewayConfig {
  host: string;
  port: number;
  allowedHosts: string[];
  allowedOrigins: string[];
  sharedSecret?: string;
  sageApiUrl: string;
  timeoutMs: number;
  keys: {
    read?: string;
    create?: string;
    finance?: string;
  };
  enabled: {
    createTools: boolean;
    financeTools: boolean;
  };
}

type Env = Record<string, string | undefined>;

function envFirst(env: Env, names: string[]): string | undefined {
  for (const name of names) {
    const value = env[name];
    if (value && value.trim().length > 0) {
      return value.trim();
    }
  }

  return undefined;
}

function envBool(env: Env, name: string, defaultValue = false): boolean {
  const value = env[name];
  if (!value) {
    return defaultValue;
  }

  return ["1", "true", "yes", "on"].includes(value.trim().toLowerCase());
}

function envInt(env: Env, name: string, defaultValue: number): number {
  const raw = env[name];
  if (!raw) {
    return defaultValue;
  }

  const parsed = Number.parseInt(raw, 10);
  return Number.isFinite(parsed) && parsed > 0 ? parsed : defaultValue;
}

function parseCsv(value: string | undefined, fallback: string[]): string[] {
  if (!value) {
    return fallback;
  }

  const parsed = value
    .split(",")
    .map((entry) => entry.trim())
    .filter(Boolean);

  return parsed.length > 0 ? parsed : fallback;
}

export function loadConfig(env: Env = process.env): GatewayConfig {
  const host = envFirst(env, ["MCP_HOST", "HOST"]) ?? "127.0.0.1";
  const port = envInt(env, "MCP_PORT", envInt(env, "PORT", 8787));
  const commonApiKey = envFirst(env, ["UAF_SAGE_API_KEY", "UAF_API_KEY", "SAGE_API_KEY"]);
  const readKey = envFirst(env, ["UAF_SAGE_READ_API_KEY", "UAF_API_KEY_READ"]) ?? commonApiKey;
  const createKey = envFirst(env, ["UAF_SAGE_CREATE_API_KEY", "UAF_API_KEY_CREATE"]) ?? commonApiKey;
  const financeKey = envFirst(env, ["UAF_SAGE_FINANCE_API_KEY", "UAF_API_KEY_FINANCE"]) ?? commonApiKey;

  return {
    host,
    port,
    allowedHosts: parseCsv(env.MCP_ALLOWED_HOSTS, ["127.0.0.1", "localhost"]),
    allowedOrigins: parseCsv(env.MCP_ALLOWED_ORIGINS, []),
    sharedSecret: envFirst(env, ["MCP_SHARED_SECRET", "MCP_BEARER_TOKEN"]),
    sageApiUrl: envFirst(env, ["UAF_SAGE_API_URL", "UAF_BASE_URL", "SAGE_MIDDLEWARE_URL"]) ?? "http://localhost:3000",
    timeoutMs: envInt(env, "UAF_SAGE_TIMEOUT_MS", 30000),
    keys: {
      read: readKey,
      create: createKey,
      finance: financeKey
    },
    enabled: {
      createTools: envBool(env, "ENABLE_CREATE_TOOLS", false),
      financeTools: envBool(env, "ENABLE_FINANCE_TOOLS", false)
    }
  };
}

export function assertRunnableConfig(config: GatewayConfig): void {
  if (!config.keys.read) {
    throw new Error("Missing UAF_SAGE_READ_API_KEY or UAF_SAGE_API_KEY for read tools.");
  }

  if (config.enabled.createTools && !config.keys.create) {
    throw new Error("ENABLE_CREATE_TOOLS=true requires UAF_SAGE_CREATE_API_KEY.");
  }

  if (config.enabled.financeTools && !config.keys.finance) {
    throw new Error("ENABLE_FINANCE_TOOLS=true requires UAF_SAGE_FINANCE_API_KEY.");
  }
}

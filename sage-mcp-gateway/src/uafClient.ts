export type QueryValue = string | number | boolean | undefined | null;
export type Query = Record<string, QueryValue>;

export class UafApiError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly details: unknown
  ) {
    super(message);
    this.name = "UafApiError";
  }
}

export interface UafClientOptions {
  baseUrl: string;
  timeoutMs: number;
  keys: {
    read?: string;
    create?: string;
    finance?: string;
  };
  fetchImpl?: typeof fetch;
}

export class UafClient {
  private readonly baseUrl: URL;
  private readonly timeoutMs: number;
  private readonly keys: UafClientOptions["keys"];
  private readonly fetchImpl: typeof fetch;

  constructor(options: UafClientOptions) {
    this.baseUrl = new URL(options.baseUrl.endsWith("/") ? options.baseUrl : `${options.baseUrl}/`);
    this.timeoutMs = options.timeoutMs;
    this.keys = options.keys;
    this.fetchImpl = options.fetchImpl ?? fetch;
  }

  async readiness(): Promise<unknown> {
    return this.request("read", "health/ready");
  }

  async searchItems(input: Query): Promise<unknown> {
    return this.request("read", "api/v1/items/search", { query: input });
  }

  async getItem(itemCode: string): Promise<unknown> {
    return this.request("read", `api/v1/items/${encodeURIComponent(itemCode)}`);
  }

  async getItemAvailability(input: { itemCode: string; warehouseCode?: string }): Promise<unknown> {
    return this.request("read", `api/v1/items/${encodeURIComponent(input.itemCode)}/availability`, {
      query: { warehouseCode: input.warehouseCode }
    });
  }

  async getBulkAvailability(input: unknown): Promise<unknown> {
    return this.request("read", "api/v1/items/availability", { method: "POST", body: input });
  }

  async getItemAliases(itemCode: string): Promise<unknown> {
    return this.request("read", `api/v1/items/${encodeURIComponent(itemCode)}/aliases`);
  }

  async getItemAlternates(itemCode: string): Promise<unknown> {
    return this.request("read", `api/v1/items/${encodeURIComponent(itemCode)}/alternates`);
  }

  async validateItems(input: unknown): Promise<unknown> {
    return this.request("read", "api/v1/inventory/validate", { method: "POST", body: input });
  }

  async checkItemExists(itemCode: string): Promise<unknown> {
    return this.request("read", `api/v1/inventory/check/${encodeURIComponent(itemCode)}`);
  }

  async searchCustomers(input: Query): Promise<unknown> {
    return this.request("read", "api/v1/customers/search", { query: input });
  }

  async getCustomer(customerNumber: string): Promise<unknown> {
    return this.request("read", `api/v1/customers/${encodeURIComponent(customerNumber)}`);
  }

  async validateShipTo(customerNumber: string, input: unknown): Promise<unknown> {
    return this.request("read", `api/v1/customers/${encodeURIComponent(customerNumber)}/validate-shipto`, {
      method: "POST",
      body: input
    });
  }

  async resolveCustomer(input: unknown): Promise<unknown> {
    return this.request("read", "api/v1/customers/resolve", { method: "POST", body: input });
  }

  async getCustomerAccountSummary(customerNumber: string, openInvoiceLimit?: number): Promise<unknown> {
    return this.request("finance", `api/v1/customers/${encodeURIComponent(customerNumber)}/account-summary`, {
      query: { openInvoiceLimit }
    });
  }

  async searchSalesOrders(input: Query): Promise<unknown> {
    return this.request("read", "api/v1/sales-orders/search", { query: input });
  }

  async getSalesOrderDetails(salesOrderNumber: string): Promise<unknown> {
    return this.request("read", `api/v1/sales-orders/${encodeURIComponent(salesOrderNumber)}/details`);
  }

  async createSalesOrder(input: unknown): Promise<unknown> {
    return this.request("create", "api/v1/sales-orders", { method: "POST", body: input });
  }

  async searchVendors(input: Query): Promise<unknown> {
    return this.request("read", "api/v1/vendors/search", { query: input });
  }

  async getVendor(vendorNumber: string): Promise<unknown> {
    return this.request("read", `api/v1/vendors/${encodeURIComponent(vendorNumber)}`);
  }

  async searchPurchaseOrders(input: Query): Promise<unknown> {
    return this.request("read", "api/v1/purchase-orders/search", { query: input });
  }

  async searchPurchaseOrderQuotes(input: Query): Promise<unknown> {
    return this.request("read", "api/v1/purchase-orders/quotes/search", { query: input });
  }

  async getPurchaseOrder(purchaseOrderNumber: string): Promise<unknown> {
    return this.request("read", `api/v1/purchase-orders/${encodeURIComponent(purchaseOrderNumber)}`);
  }

  async getReferenceData(type: string, limit?: number): Promise<unknown> {
    return this.request("read", `api/v1/reference/${encodeURIComponent(type)}`, { query: { limit } });
  }

  private async request(
    scope: "read" | "create" | "finance",
    path: string,
    options: { method?: "GET" | "POST"; query?: Query; body?: unknown } = {}
  ): Promise<unknown> {
    const apiKey = this.keys[scope];
    if (!apiKey) {
      throw new Error(`Missing upstream ${scope} API key.`);
    }

    const url = new URL(path, this.baseUrl);
    for (const [key, value] of Object.entries(options.query ?? {})) {
      if (value !== undefined && value !== null && value !== "") {
        url.searchParams.set(key, String(value));
      }
    }

    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), this.timeoutMs);

    try {
      const response = await this.fetchImpl(url, {
        method: options.method ?? "GET",
        headers: {
          "Accept": "application/json",
          "Content-Type": "application/json",
          "X-API-Key": apiKey
        },
        body: options.body === undefined ? undefined : JSON.stringify(options.body),
        signal: controller.signal
      });

      const details = await parseResponseBody(response);
      if (!response.ok) {
        throw new UafApiError(`UAFMiddleware returned ${response.status}`, response.status, details);
      }

      return details;
    } catch (error) {
      if (error instanceof UafApiError) {
        throw error;
      }

      if (error instanceof Error && error.name === "AbortError") {
        throw new UafApiError("UAFMiddleware request timed out", 504, { timeoutMs: this.timeoutMs });
      }

      throw error;
    } finally {
      clearTimeout(timeout);
    }
  }
}

async function parseResponseBody(response: Response): Promise<unknown> {
  const contentType = response.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return response.json();
  }

  const text = await response.text();
  return text.length > 0 ? text : null;
}

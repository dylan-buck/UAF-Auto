// PO Processing Types

export interface POLineItem {
  itemCode: string;
  quantity: number;
  unitPrice?: number;
  description?: string;
  extendedPrice?: number;
}

export interface ShipToAddress {
  name?: string;
  address1?: string;
  address2?: string;
  city?: string;
  state?: string;
  zipCode?: string;
}

export interface POData {
  customerName: string;
  poNumber: string;
  shipToAddress: ShipToAddress;
  lineItems: POLineItem[];
  orderDate?: string;
  specialInstructions?: string;
}

export interface CustomerMatch {
  customerNumber: string;
  customerName: string;
  score: number;
  matchedShipToCode?: string;
  isDefaultShipTo: boolean;
  warehouseCode?: string;
  shipVia?: string;
}

export interface SalesOrderDetails {
  salesOrderNumber: string;
  customerNumber: string;
  customerName: string;
  customerPONumber: string;
  shipToCode?: string;
  shipToAddress?: ShipToAddress;
  warehouseCode?: string;
  shipVia?: string;
  lineItems: POLineItem[];
  orderTotal?: number;
}

export interface ProcessingResult {
  status: 'processing' | 'success' | 'rejected' | 'error';
  recommendation?: 'PASS' | 'REJECTED' | 'AUTO_PROCESS'; // PASS is new, AUTO_PROCESS for legacy
  confidence?: number;
  message?: string;

  // Customer resolution data
  customerMatch?: CustomerMatch;

  // Extracted PO data
  extractedData?: POData;

  // Created sales order data (for PASS/AUTO_PROCESS)
  salesOrder?: SalesOrderDetails;

  // Legacy field for backwards compatibility
  salesOrderNumber?: string;

  // Scoring and reasoning
  scoringDetails?: string[];
  issues?: string[];
}

export interface UploadHistoryItem {
  id: string;
  fileName: string;
  timestamp: Date;
  result: ProcessingResult;
}

export type ProcessingStage =
  | 'uploading'
  | 'extracting'
  | 'parsing'
  | 'resolving'
  | 'creating'
  | 'complete';

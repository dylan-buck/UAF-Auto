import type { ProcessingResult, ProcessingStage } from '../types';

// n8n webhook URL - hardcoded for now (env var not loading reliably)
const N8N_WEBHOOK_URL = 'https://dbuck.app.n8n.cloud/webhook/po-upload';

// Toggle between mock and real API
const USE_MOCK = false;

// Debug: log which mode we're in
console.log('[PO Upload] Mode: LIVE | URL:', N8N_WEBHOOK_URL);

export interface UploadOptions {
  onStageChange?: (stage: ProcessingStage) => void;
}

export async function uploadPODocument(
  file: File,
  options?: UploadOptions
): Promise<ProcessingResult> {
  console.log('[PO Upload] uploadPODocument called, USE_MOCK =', USE_MOCK);

  if (USE_MOCK) {
    console.log('[PO Upload] Using MOCK path');
    return mockUploadPODocument(file, options);
  }

  console.log('[PO Upload] Using LIVE path, sending to:', N8N_WEBHOOK_URL);
  const { onStageChange } = options || {};

  try {
    onStageChange?.('uploading');

    const formData = new FormData();
    formData.append('file', file);

    onStageChange?.('extracting');

    const response = await fetch(N8N_WEBHOOK_URL, {
      method: 'POST',
      body: formData,
    });

    if (!response.ok) {
      throw new Error(`Server error: ${response.status}`);
    }

    onStageChange?.('parsing');
    const data = await response.json();

    onStageChange?.('complete');

    return transformN8nResponse(data);
  } catch (error) {
    console.error('Error uploading PO:', error);
    return {
      status: 'error',
      message: error instanceof Error ? error.message : 'Failed to process PO',
    };
  }
}

function transformN8nResponse(data: unknown): ProcessingResult {
  // Handle the response from n8n workflow
  // The workflow should return a structured response
  const response = data as Record<string, unknown>;

  if (response.recommendation) {
    // Normalize recommendation: PASS and AUTO_PROCESS both mean success
    const recommendation = response.recommendation as string;
    const isSuccess = recommendation === 'PASS' || recommendation === 'AUTO_PROCESS';

    return {
      status: isSuccess ? 'success' : 'rejected',
      recommendation: isSuccess ? 'PASS' : 'REJECTED',
      confidence: response.confidence as number,
      message: response.message as string,
      salesOrderNumber: response.salesOrderNumber as string,
      salesOrder: response.salesOrder as ProcessingResult['salesOrder'],
      customerMatch: response.bestMatch as ProcessingResult['customerMatch'],
      extractedData: response.extractedData as ProcessingResult['extractedData'],
      scoringDetails: response.scoringDetails as string[],
      issues: response.issues as string[],
    };
  }

  if (response.error) {
    return {
      status: 'error',
      message: response.error as string,
    };
  }

  return {
    status: 'error',
    message: 'Unexpected response from server',
  };
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// Mock API for testing
export async function mockUploadPODocument(
  _file: File,
  options?: UploadOptions
): Promise<ProcessingResult> {
  const { onStageChange } = options || {};

  onStageChange?.('uploading');
  await delay(400);

  onStageChange?.('extracting');
  await delay(600);

  onStageChange?.('parsing');
  await delay(500);

  onStageChange?.('resolving');
  await delay(600);

  onStageChange?.('creating');
  await delay(400);

  onStageChange?.('complete');

  return {
    status: 'success',
    recommendation: 'PASS',
    confidence: 0.92,
    message: 'Customer identified - order created',
    salesOrder: {
      salesOrderNumber: '0334502',
      customerNumber: '01-D3375',
      customerName: 'UNITED REFRIGERATION INC (NC)',
      customerPONumber: '85025618',
      shipToCode: '0000',
      shipToAddress: {
        name: 'United Refrigeration, Inc.',
        address1: '3707 ALLIANCE DR',
        city: 'GREENSBORO',
        state: 'NC',
        zipCode: '27407',
      },
      warehouseCode: '003',
      shipVia: 'UPS Ground',
      lineItems: [
        { itemCode: '14202', quantity: 2, unitPrice: 12.50, description: 'Filter 20x25x2', extendedPrice: 25.00 },
        { itemCode: '16251', quantity: 5, unitPrice: 8.75, description: 'Filter 16x25x1', extendedPrice: 43.75 },
        { itemCode: '20201', quantity: 10, unitPrice: 15.00, description: 'Filter 20x20x1', extendedPrice: 150.00 },
      ],
      orderTotal: 218.75,
    },
    customerMatch: {
      customerNumber: '01-D3375',
      customerName: 'UNITED REFRIGERATION INC (NC)',
      score: 0.92,
      matchedShipToCode: '0000',
      isDefaultShipTo: true,
      warehouseCode: '003',
      shipVia: 'UG',
    },
    extractedData: {
      customerName: 'United Refrigeration, Inc.',
      poNumber: '85025618',
      shipToAddress: {
        name: 'United Refrigeration, Inc.',
        address1: '3707 ALLIANCE DR',
        city: 'GREENSBORO',
        state: 'NC',
        zipCode: '27407',
      },
      lineItems: [
        { itemCode: '14202', quantity: 2, description: 'Filter 20x25x2' },
        { itemCode: '16251', quantity: 5, description: 'Filter 16x25x1' },
        { itemCode: '20201', quantity: 10, description: 'Filter 20x20x1' },
      ],
    },
    scoringDetails: [
      'Name match: 90% (United Refrigeration → UNITED REFRIGERATION INC)',
      'Ship-to address: 100% match (3707 ALLIANCE DR, GREENSBORO NC)',
      'Default ship-to: Yes (+10% bonus)',
      'Warehouse: 003 (configured)',
      'Ship via: UPS Ground (configured)',
    ],
  };
}

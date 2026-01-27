import type { ProcessingResult } from '../types';

interface ResultCardProps {
  result: ProcessingResult;
  fileName: string;
}

export function ResultCard({ result, fileName }: ResultCardProps) {
  // Handle PASS (and legacy AUTO_PROCESS) as success
  if (result.recommendation === 'PASS' || result.recommendation === 'AUTO_PROCESS') {
    const order = result.salesOrder;
    const shipTo = order?.shipToAddress;

    return (
      <div className="text-center pt-4">
        {/* Success Icon */}
        <div className="inline-flex items-center justify-center w-14 h-14 rounded-full bg-emerald-50 mb-6">
          <svg className="w-7 h-7 text-emerald-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </div>

        <h2 className="text-[22px] font-semibold text-gray-900 mb-1">Order Created</h2>
        <p className="text-[14px] text-gray-400 mb-6">{fileName}</p>

        {/* Order Summary Card */}
        <div className="bg-gray-50 rounded-xl p-5 text-left mb-4">
          <div className="grid grid-cols-2 gap-4 mb-4">
            <div>
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Order #</p>
              <p className="text-[18px] font-semibold text-gray-900 font-mono">{order?.salesOrderNumber || result.salesOrderNumber}</p>
            </div>
            <div>
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">PO #</p>
              <p className="text-[14px] font-medium text-gray-900">{order?.customerPONumber || result.extractedData?.poNumber}</p>
            </div>
          </div>

          <div className="pt-4 border-t border-gray-200">
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Customer</p>
            <p className="text-[14px] font-medium text-gray-900">{order?.customerName || result.customerMatch?.customerName}</p>
            <p className="text-[12px] text-gray-500 font-mono">{order?.customerNumber || result.customerMatch?.customerNumber}</p>
          </div>

          {shipTo && (
            <div className="pt-4 mt-4 border-t border-gray-200">
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Ship To</p>
              <p className="text-[13px] text-gray-700">{shipTo.name}</p>
              <p className="text-[13px] text-gray-600">{shipTo.address1}</p>
              {shipTo.address2 && <p className="text-[13px] text-gray-600">{shipTo.address2}</p>}
              <p className="text-[13px] text-gray-600">{shipTo.city}, {shipTo.state} {shipTo.zipCode}</p>
            </div>
          )}

          <div className="pt-4 mt-4 border-t border-gray-200 grid grid-cols-2 gap-4">
            <div>
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Warehouse</p>
              <p className="text-[13px] font-medium text-gray-900">{order?.warehouseCode || result.customerMatch?.warehouseCode || '—'}</p>
            </div>
            <div>
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Ship Via</p>
              <p className="text-[13px] font-medium text-gray-900">{order?.shipVia || result.customerMatch?.shipVia || '—'}</p>
            </div>
          </div>
        </div>

        {/* Line Items */}
        {order?.lineItems && order.lineItems.length > 0 && (
          <div className="bg-gray-50 rounded-xl p-5 text-left mb-4">
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-3">Line Items</p>
            <div className="space-y-2">
              {order.lineItems.map((item, i) => (
                <div key={i} className="flex justify-between items-center py-2 border-b border-gray-200 last:border-0">
                  <div className="flex-1">
                    <p className="text-[13px] font-medium text-gray-900 font-mono">{item.itemCode}</p>
                    {item.description && <p className="text-[12px] text-gray-500">{item.description}</p>}
                  </div>
                  <div className="text-right">
                    <p className="text-[13px] text-gray-700">Qty: {item.quantity}</p>
                    {item.unitPrice !== undefined && (
                      <p className="text-[12px] text-gray-500">${item.unitPrice.toFixed(2)} ea</p>
                    )}
                  </div>
                  {item.extendedPrice !== undefined && (
                    <div className="text-right ml-4 min-w-[70px]">
                      <p className="text-[13px] font-medium text-gray-900">${item.extendedPrice.toFixed(2)}</p>
                    </div>
                  )}
                </div>
              ))}
            </div>
            {order.orderTotal !== undefined && (
              <div className="flex justify-between items-center pt-3 mt-2 border-t border-gray-300">
                <p className="text-[13px] font-medium text-gray-700">Order Total</p>
                <p className="text-[16px] font-semibold text-gray-900">${order.orderTotal.toFixed(2)}</p>
              </div>
            )}
          </div>
        )}

        {/* Confidence Score */}
        {result.confidence !== undefined && (
          <div className="flex items-center justify-center gap-2 text-[13px] text-emerald-600">
            <svg className="w-4 h-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <span>{Math.round(result.confidence * 100)}% confidence match</span>
          </div>
        )}
      </div>
    );
  }

  // REJECTED - Show rejection reason and extracted data for manual processing
  const extracted = result.extractedData;
  const shipTo = extracted?.shipToAddress;

  return (
    <div className="text-center pt-4">
      {/* Error Icon */}
      <div className="inline-flex items-center justify-center w-14 h-14 rounded-full bg-rose-50 mb-6">
        <svg className="w-7 h-7 text-rose-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
          <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
        </svg>
      </div>

      <h2 className="text-[22px] font-semibold text-gray-900 mb-1">Order Rejected</h2>
      <p className="text-[14px] text-gray-400 mb-4">{fileName}</p>
      <p className="text-[14px] text-gray-600 mb-6">{result.message || 'Unable to process this order automatically.'}</p>

      {/* Rejection Details Card */}
      {result.scoringDetails && result.scoringDetails.length > 0 && (
        <div className="bg-rose-50 rounded-xl p-5 text-left mb-4">
          <p className="text-[11px] font-medium text-rose-700 uppercase tracking-wider mb-2">Rejection Details</p>
          {result.scoringDetails.map((detail, i) => (
            <p key={i} className="text-[13px] text-gray-600 mb-1 last:mb-0">
              {detail}
            </p>
          ))}
        </div>
      )}

      {/* Customer Match Info (if available) */}
      {result.customerMatch && (
        <div className="bg-gray-50 rounded-xl p-5 text-left mb-4">
          <div className="flex justify-between items-start">
            <div>
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Best Match Found</p>
              <p className="text-[14px] font-medium text-gray-900">{result.customerMatch.customerName}</p>
              <p className="text-[12px] text-gray-500 font-mono">{result.customerMatch.customerNumber}</p>
            </div>
            {result.customerMatch.matchedShipToCode && (
              <div className="text-right">
                <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Ship-To</p>
                <p className="text-[14px] font-medium text-gray-900">{result.customerMatch.matchedShipToCode}</p>
              </div>
            )}
          </div>
        </div>
      )}

      {/* Extracted PO Data (for manual entry reference) */}
      {extracted && (
        <div className="bg-gray-50 rounded-xl p-5 text-left mb-4">
          <p className="text-[11px] font-medium text-gray-500 uppercase tracking-wider mb-3">Extracted from PO (for manual entry)</p>

          <div className="grid grid-cols-2 gap-4 mb-3">
            <div>
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Customer Name</p>
              <p className="text-[13px] text-gray-900">{extracted.customerName}</p>
            </div>
            <div>
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">PO #</p>
              <p className="text-[13px] text-gray-900">{extracted.poNumber}</p>
            </div>
          </div>

          {shipTo && (
            <div className="pt-3 border-t border-gray-200">
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-1">Ship To Address</p>
              {shipTo.name && <p className="text-[13px] text-gray-700">{shipTo.name}</p>}
              {shipTo.address1 && <p className="text-[13px] text-gray-600">{shipTo.address1}</p>}
              {shipTo.address2 && <p className="text-[13px] text-gray-600">{shipTo.address2}</p>}
              <p className="text-[13px] text-gray-600">
                {[shipTo.city, shipTo.state, shipTo.zipCode].filter(Boolean).join(', ')}
              </p>
            </div>
          )}

          {extracted.lineItems && extracted.lineItems.length > 0 && (
            <div className="pt-3 mt-3 border-t border-gray-200">
              <p className="text-[11px] font-medium text-gray-400 uppercase tracking-wider mb-2">
                {extracted.lineItems.length} Line Items
              </p>
              {extracted.lineItems.slice(0, 5).map((item, i) => (
                <p key={i} className="text-[12px] text-gray-600">
                  {item.itemCode} × {item.quantity}
                </p>
              ))}
              {extracted.lineItems.length > 5 && (
                <p className="text-[12px] text-gray-400 italic">
                  +{extracted.lineItems.length - 5} more items
                </p>
              )}
            </div>
          )}
        </div>
      )}
    </div>
  );
}

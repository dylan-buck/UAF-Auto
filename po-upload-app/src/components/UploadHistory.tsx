import type { UploadHistoryItem } from '../types';

interface UploadHistoryProps {
  items: UploadHistoryItem[];
  onItemClick?: (item: UploadHistoryItem) => void;
}

export function UploadHistory({ items, onItemClick }: UploadHistoryProps) {
  if (items.length === 0) {
    return (
      <div className="text-center py-8 text-gray-400">
        <p className="text-sm">No uploads yet</p>
        <p className="text-xs">Upload a PO to get started</p>
      </div>
    );
  }

  const getStatusBadge = (item: UploadHistoryItem) => {
    const recommendation = item.result.recommendation;
    // PASS and AUTO_PROCESS (legacy) are both success
    if (recommendation === 'PASS' || recommendation === 'AUTO_PROCESS') {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-green-100 text-green-700">
          <svg xmlns="http://www.w3.org/2000/svg" className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
          </svg>
          Processed
        </span>
      );
    }

    if (recommendation === 'REJECTED') {
      return (
        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-red-100 text-red-700">
          <svg xmlns="http://www.w3.org/2000/svg" className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor">
            <path fillRule="evenodd" d="M4.293 4.293a1 1 0 011.414 0L10 8.586l4.293-4.293a1 1 0 111.414 1.414L11.414 10l4.293 4.293a1 1 0 01-1.414 1.414L10 11.414l-4.293 4.293a1 1 0 01-1.414-1.414L8.586 10 4.293 5.707a1 1 0 010-1.414z" clipRule="evenodd" />
          </svg>
          Rejected
        </span>
      );
    }

    return (
      <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-gray-100 text-gray-600">
        Error
      </span>
    );
  };

  const formatTime = (date: Date) => {
    const now = new Date();
    const diff = now.getTime() - date.getTime();
    const minutes = Math.floor(diff / 60000);
    const hours = Math.floor(diff / 3600000);

    if (minutes < 1) return 'Just now';
    if (minutes < 60) return `${minutes}m ago`;
    if (hours < 24) return `${hours}h ago`;
    return date.toLocaleDateString();
  };

  return (
    <div className="space-y-2">
      {items.map((item) => (
        <div
          key={item.id}
          onClick={() => onItemClick?.(item)}
          className={`bg-white rounded-lg p-3 border border-gray-200 transition-all ${
            onItemClick ? 'cursor-pointer hover:border-uaf-navy hover:shadow-sm' : ''
          }`}
        >
          <div className="flex items-start justify-between gap-2">
            <div className="flex-1 min-w-0">
              <p className="font-medium text-sm text-uaf-navy truncate">
                {item.result.extractedData?.poNumber
                  ? `PO #${item.result.extractedData.poNumber}`
                  : item.fileName}
              </p>
              <p className="text-xs text-gray-500 truncate">
                {item.result.customerMatch?.customerName || 'Unknown customer'}
              </p>
            </div>
            <div className="flex flex-col items-end gap-1">
              {getStatusBadge(item)}
              <span className="text-xs text-gray-400">{formatTime(item.timestamp)}</span>
            </div>
          </div>

          {(item.result.recommendation === 'PASS' || item.result.recommendation === 'AUTO_PROCESS') && item.result.salesOrderNumber && (
            <div className="mt-2 pt-2 border-t border-gray-100">
              <p className="text-xs text-gray-500">
                Sales Order: <span className="font-mono text-green-600">{item.result.salesOrderNumber}</span>
              </p>
            </div>
          )}
        </div>
      ))}
    </div>
  );
}

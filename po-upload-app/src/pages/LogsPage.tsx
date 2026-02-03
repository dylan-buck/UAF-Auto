import { useState, useEffect, useMemo } from 'react';
import type { POHistoryEntry } from '../types';
import {
  fetchPOHistory,
  getCachedHistory,
  setCachedHistory,
  clearHistoryCache,
} from '../services/historyApi';

export function LogsPage() {
  const [history, setHistory] = useState<POHistoryEntry[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [filterStatus, setFilterStatus] = useState<'all' | 'PASS' | 'REJECTED'>('all');

  useEffect(() => {
    async function loadHistory() {
      // Try cache first for instant display
      const cached = getCachedHistory();
      if (cached) {
        setHistory(cached);
        setIsLoading(false);
      }

      // Fetch fresh data
      try {
        const data = await fetchPOHistory();
        setHistory(data);
        setCachedHistory(data);
        setError(null);
      } catch (err) {
        if (!cached) {
          setError('Failed to load history. Please try again.');
        }
      } finally {
        setIsLoading(false);
      }
    }

    loadHistory();
  }, []);

  const filteredHistory = useMemo(() => {
    return history.filter((entry) => {
      // Filter by status
      if (filterStatus !== 'all' && entry.result !== filterStatus) {
        return false;
      }

      // Filter by search query
      if (searchQuery) {
        const query = searchQuery.toLowerCase();
        return (
          entry.poNumber?.toLowerCase().includes(query) ||
          entry.customer?.toLowerCase().includes(query) ||
          entry.salesOrderNumber?.toLowerCase().includes(query) ||
          entry.fileName?.toLowerCase().includes(query)
        );
      }

      return true;
    });
  }, [history, searchQuery, filterStatus]);

  const handleRefresh = async () => {
    setIsLoading(true);
    clearHistoryCache();
    try {
      const data = await fetchPOHistory();
      setHistory(data);
      setCachedHistory(data);
      setError(null);
    } catch {
      setError('Failed to refresh history.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex-1 px-6 py-8">
      <div className="max-w-4xl mx-auto">
        {/* Header */}
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-2xl font-semibold text-gray-900">PO Upload History</h1>
            <p className="text-sm text-gray-500 mt-1">
              {history.length} total uploads
            </p>
          </div>
          <button
            onClick={handleRefresh}
            disabled={isLoading}
            className="px-4 py-2 text-sm font-medium text-gray-600 hover:text-gray-900 hover:bg-gray-100 rounded-lg transition-colors disabled:opacity-50"
          >
            {isLoading ? 'Loading...' : 'Refresh'}
          </button>
        </div>

        {/* Filters */}
        <div className="flex gap-4 mb-6">
          <div className="flex-1">
            <input
              type="text"
              placeholder="Search by PO#, customer, or file name..."
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              className="w-full px-4 py-2.5 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-gray-200 focus:border-gray-300"
            />
          </div>
          <select
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value as 'all' | 'PASS' | 'REJECTED')}
            className="px-4 py-2.5 text-sm border border-gray-200 rounded-lg focus:outline-none focus:ring-2 focus:ring-gray-200 focus:border-gray-300 bg-white"
          >
            <option value="all">All Status</option>
            <option value="PASS">Passed</option>
            <option value="REJECTED">Rejected</option>
          </select>
        </div>

        {/* Error State */}
        {error && (
          <div className="mb-6 p-4 bg-red-50 border border-red-100 rounded-lg text-red-700 text-sm">
            {error}
          </div>
        )}

        {/* Loading State */}
        {isLoading && history.length === 0 && (
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block w-6 h-6 border-2 border-gray-300 border-t-gray-600 rounded-full animate-spin mb-3"></div>
            <p>Loading history...</p>
          </div>
        )}

        {/* Empty State */}
        {!isLoading && history.length === 0 && !error && (
          <div className="text-center py-12 text-gray-500">
            <p className="text-lg font-medium mb-2">No uploads yet</p>
            <p className="text-sm">Upload a PO to see history here</p>
          </div>
        )}

        {/* No Results State */}
        {!isLoading && history.length > 0 && filteredHistory.length === 0 && (
          <div className="text-center py-12 text-gray-500">
            <p className="text-lg font-medium mb-2">No matching results</p>
            <p className="text-sm">Try adjusting your search or filters</p>
          </div>
        )}

        {/* History Table */}
        {filteredHistory.length > 0 && (
          <div className="bg-white border border-gray-200 rounded-xl overflow-hidden">
            <table className="w-full">
              <thead>
                <tr className="border-b border-gray-100 bg-gray-50/50">
                  <th className="text-left text-xs font-medium text-gray-500 uppercase tracking-wider px-4 py-3">
                    Date
                  </th>
                  <th className="text-left text-xs font-medium text-gray-500 uppercase tracking-wider px-4 py-3">
                    PO #
                  </th>
                  <th className="text-left text-xs font-medium text-gray-500 uppercase tracking-wider px-4 py-3">
                    Customer
                  </th>
                  <th className="text-left text-xs font-medium text-gray-500 uppercase tracking-wider px-4 py-3">
                    Status
                  </th>
                  <th className="text-left text-xs font-medium text-gray-500 uppercase tracking-wider px-4 py-3">
                    Sales Order
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-gray-100">
                {filteredHistory.map((entry, index) => (
                  <HistoryRow key={`${entry.timestamp}-${index}`} entry={entry} />
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}

function HistoryRow({ entry }: { entry: POHistoryEntry }) {
  const [isExpanded, setIsExpanded] = useState(false);
  const isPass = entry.result === 'PASS';

  return (
    <>
      <tr
        onClick={() => setIsExpanded(!isExpanded)}
        className="hover:bg-gray-50 cursor-pointer transition-colors"
      >
        <td className="px-4 py-3 text-sm text-gray-600">
          {formatDate(entry.timestamp)}
        </td>
        <td className="px-4 py-3 text-sm font-medium text-gray-900">
          {entry.poNumber || '—'}
        </td>
        <td className="px-4 py-3 text-sm text-gray-600 max-w-[200px] truncate">
          {entry.customer || '—'}
        </td>
        <td className="px-4 py-3">
          <span
            className={`inline-flex items-center px-2 py-0.5 rounded-full text-xs font-medium ${
              isPass
                ? 'bg-emerald-50 text-emerald-700'
                : 'bg-rose-50 text-rose-700'
            }`}
          >
            {isPass ? 'Passed' : 'Rejected'}
          </span>
        </td>
        <td className="px-4 py-3 text-sm font-mono text-gray-600">
          {entry.salesOrderNumber || '—'}
        </td>
      </tr>

      {/* Expanded Details */}
      {isExpanded && (
        <tr className="bg-gray-50/50">
          <td colSpan={5} className="px-4 py-4">
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <p className="text-gray-500 text-xs uppercase tracking-wider mb-1">
                  File Name
                </p>
                <p className="text-gray-700">{entry.fileName || '—'}</p>
              </div>
              {!isPass && entry.rejectionReason && (
                <div>
                  <p className="text-gray-500 text-xs uppercase tracking-wider mb-1">
                    Rejection Reason
                  </p>
                  <p className="text-rose-600">{entry.rejectionReason}</p>
                </div>
              )}
            </div>
          </td>
        </tr>
      )}
    </>
  );
}

function formatDate(timestamp: string): string {
  try {
    const date = new Date(timestamp);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffHours = diffMs / (1000 * 60 * 60);

    if (diffHours < 24) {
      return date.toLocaleTimeString('en-US', {
        hour: 'numeric',
        minute: '2-digit',
        hour12: true,
      });
    }

    if (diffHours < 48) {
      return 'Yesterday';
    }

    return date.toLocaleDateString('en-US', {
      month: 'short',
      day: 'numeric',
      year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined,
    });
  } catch {
    return timestamp;
  }
}

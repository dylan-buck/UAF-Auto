import type { POHistoryEntry } from '../types';

// n8n webhook URL for fetching PO history
const HISTORY_API_URL = import.meta.env.VITE_N8N_HISTORY_URL?.trim() ?? '';

export interface HistoryResponse {
  success: boolean;
  count: number;
  data: POHistoryEntry[];
}

export async function fetchPOHistory(): Promise<POHistoryEntry[]> {
  if (!HISTORY_API_URL) {
    throw new Error('PO history endpoint is not configured. Set VITE_N8N_HISTORY_URL.');
  }

  const response = await fetch(HISTORY_API_URL);

  if (!response.ok) {
    throw new Error(`Failed to fetch history: ${response.status}`);
  }

  const result: HistoryResponse = await response.json();

  if (!result.success) {
    throw new Error('API returned unsuccessful response');
  }

  return result.data;
}

// localStorage key for caching history
const HISTORY_CACHE_KEY = 'uaf-po-history-cache';
const CACHE_DURATION_MS = 5 * 60 * 1000; // 5 minutes

interface CachedHistory {
  data: POHistoryEntry[];
  timestamp: number;
}

export function getCachedHistory(): POHistoryEntry[] | null {
  try {
    const cached = localStorage.getItem(HISTORY_CACHE_KEY);
    if (!cached) return null;

    const parsed: CachedHistory = JSON.parse(cached);
    const isExpired = Date.now() - parsed.timestamp > CACHE_DURATION_MS;

    if (isExpired) {
      localStorage.removeItem(HISTORY_CACHE_KEY);
      return null;
    }

    return parsed.data;
  } catch {
    return null;
  }
}

export function setCachedHistory(data: POHistoryEntry[]): void {
  try {
    const cached: CachedHistory = {
      data,
      timestamp: Date.now(),
    };
    localStorage.setItem(HISTORY_CACHE_KEY, JSON.stringify(cached));
  } catch {
    // Ignore localStorage errors
  }
}

export function clearHistoryCache(): void {
  localStorage.removeItem(HISTORY_CACHE_KEY);
}

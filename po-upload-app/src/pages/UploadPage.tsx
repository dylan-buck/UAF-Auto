import { useState, useCallback } from 'react';
import { DropZone } from '../components/DropZone';
import { ProcessingStatus } from '../components/ProcessingStatus';
import { ResultCard } from '../components/ResultCard';
import { uploadPODocument } from '../services/api';
import type { ProcessingResult, ProcessingStage, UploadHistoryItem } from '../types';

// localStorage key for recent uploads
const RECENT_UPLOADS_KEY = 'uaf-recent-uploads';
const MAX_RECENT_UPLOADS = 50;

function getRecentUploads(): UploadHistoryItem[] {
  try {
    const stored = localStorage.getItem(RECENT_UPLOADS_KEY);
    if (!stored) return [];
    const items = JSON.parse(stored);
    // Convert timestamp strings back to Date objects
    return items.map((item: UploadHistoryItem & { timestamp: string }) => ({
      ...item,
      timestamp: new Date(item.timestamp),
    }));
  } catch {
    return [];
  }
}

function saveRecentUpload(item: UploadHistoryItem): void {
  try {
    const existing = getRecentUploads();
    const updated = [item, ...existing].slice(0, MAX_RECENT_UPLOADS);
    localStorage.setItem(RECENT_UPLOADS_KEY, JSON.stringify(updated));
  } catch {
    // Ignore localStorage errors
  }
}

export function UploadPage() {
  const [isProcessing, setIsProcessing] = useState(false);
  const [currentStage, setCurrentStage] = useState<ProcessingStage>('uploading');
  const [currentFile, setCurrentFile] = useState<string>('');
  const [currentResult, setCurrentResult] = useState<ProcessingResult | null>(null);
  const [history, setHistory] = useState<UploadHistoryItem[]>(() => getRecentUploads());

  const handleFileSelect = useCallback(async (file: File) => {
    setIsProcessing(true);
    setCurrentFile(file.name);
    setCurrentResult(null);
    setCurrentStage('uploading');

    try {
      const result = await uploadPODocument(file, {
        onStageChange: setCurrentStage,
      });

      setCurrentResult(result);

      const historyItem: UploadHistoryItem = {
        id: Date.now().toString(),
        fileName: file.name,
        timestamp: new Date(),
        result,
      };

      // Save to state and localStorage
      setHistory((prev) => [historyItem, ...prev.slice(0, MAX_RECENT_UPLOADS - 1)]);
      saveRecentUpload(historyItem);
    } catch (error) {
      setCurrentResult({
        status: 'error',
        message: error instanceof Error ? error.message : 'Unknown error',
      });
    } finally {
      setIsProcessing(false);
    }
  }, []);

  return (
    <div className="flex-1 flex flex-col items-center px-6 py-12">
      <div className="w-full max-w-md">
        {/* Upload State */}
        {!isProcessing && !currentResult && (
          <>
            <div className="text-center mb-10">
              <h1 className="text-[28px] font-semibold text-gray-900 tracking-tight mb-3">
                Upload Purchase Order
              </h1>
              <p className="text-[15px] text-gray-500">
                Drop a PDF to create a sales order
              </p>
            </div>
            <DropZone onFileSelect={handleFileSelect} isProcessing={isProcessing} />
          </>
        )}

        {/* Processing State */}
        {isProcessing && (
          <ProcessingStatus stage={currentStage} fileName={currentFile} />
        )}

        {/* Result State */}
        {currentResult && !isProcessing && (
          <div className="space-y-6">
            <ResultCard result={currentResult} fileName={currentFile} />
            <button
              onClick={() => setCurrentResult(null)}
              className="w-full py-3.5 text-[15px] font-medium text-gray-500 hover:text-gray-700 rounded-xl hover:bg-gray-50 transition-colors"
            >
              Upload another
            </button>
          </div>
        )}

        {/* Recent - minimal */}
        {!isProcessing && !currentResult && history.length > 0 && (
          <div className="mt-16">
            <p className="text-[11px] font-medium text-gray-400 uppercase tracking-widest mb-4">
              Recent
            </p>
            <div className="space-y-1">
              {history.slice(0, 3).map((item) => (
                <div
                  key={item.id}
                  className="flex items-center justify-between py-2.5 text-[14px]"
                >
                  <div className="flex items-center gap-2.5">
                    <StatusDot status={item.result.recommendation} />
                    <span className="text-gray-600">
                      {item.result.extractedData?.poNumber
                        ? `PO #${item.result.extractedData.poNumber}`
                        : item.fileName}
                    </span>
                  </div>
                  <span className="text-gray-400 text-[13px]">
                    {formatTime(item.timestamp)}
                  </span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function StatusDot({ status }: { status?: string }) {
  // PASS and AUTO_PROCESS (legacy) are both success
  const color =
    status === 'PASS' || status === 'AUTO_PROCESS'
      ? 'bg-emerald-400'
      : 'bg-rose-400';
  return <div className={`w-1.5 h-1.5 rounded-full ${color}`} />;
}

function formatTime(date: Date) {
  const now = new Date();
  const diff = now.getTime() - date.getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'Just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(diff / 3600000);
  if (hours < 24) return `${hours}h ago`;
  return date.toLocaleDateString();
}

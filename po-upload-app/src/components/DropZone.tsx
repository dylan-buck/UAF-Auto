import { useCallback, useState } from 'react';

interface DropZoneProps {
  onFileSelect: (file: File) => void;
  isProcessing: boolean;
}

export function DropZone({ onFileSelect, isProcessing }: DropZoneProps) {
  const [isDragging, setIsDragging] = useState(false);

  const handleDrag = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
  }, []);

  const handleDragIn = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.dataTransfer.items && e.dataTransfer.items.length > 0) {
      setIsDragging(true);
    }
  }, []);

  const handleDragOut = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);
  }, []);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      e.stopPropagation();
      setIsDragging(false);

      if (isProcessing) return;

      const files = e.dataTransfer.files;
      if (files && files.length > 0) {
        const file = files[0];
        if (file.type === 'application/pdf') {
          onFileSelect(file);
        } else {
          alert('Please upload a PDF file');
        }
      }
    },
    [onFileSelect, isProcessing]
  );

  const handleFileInput = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      if (isProcessing) return;

      const files = e.target.files;
      if (files && files.length > 0) {
        const file = files[0];
        if (file.type === 'application/pdf') {
          onFileSelect(file);
        } else {
          alert('Please upload a PDF file');
        }
      }
      e.target.value = '';
    },
    [onFileSelect, isProcessing]
  );

  return (
    <div
      className={`
        group relative rounded-2xl transition-all duration-200 cursor-pointer
        ${isDragging
          ? 'bg-blue-50 border-2 border-blue-400 border-dashed'
          : 'bg-gray-50 border-2 border-gray-200 border-dashed hover:border-gray-300 hover:bg-gray-100/50'
        }
        ${isProcessing ? 'opacity-50 cursor-not-allowed' : ''}
      `}
      onDragEnter={handleDragIn}
      onDragLeave={handleDragOut}
      onDragOver={handleDrag}
      onDrop={handleDrop}
      onClick={() => {
        if (!isProcessing) {
          document.getElementById('file-input')?.click();
        }
      }}
    >
      <input
        id="file-input"
        type="file"
        accept=".pdf,application/pdf"
        onChange={handleFileInput}
        className="hidden"
        disabled={isProcessing}
      />

      <div className="flex flex-col items-center py-16 px-6">
        {/* Icon */}
        <div className={`mb-5 transition-colors ${isDragging ? 'text-blue-500' : 'text-gray-400 group-hover:text-gray-500'}`}>
          <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.5">
            <path d="M9 17v-6m3 6v-8m3 8v-4" strokeLinecap="round" />
            <rect x="3" y="3" width="18" height="18" rx="2" />
          </svg>
        </div>

        {/* Text */}
        <p className={`text-[15px] font-medium mb-1 transition-colors ${isDragging ? 'text-blue-600' : 'text-gray-700'}`}>
          {isDragging ? 'Drop to upload' : 'Drop PDF here'}
        </p>
        <p className="text-[13px] text-gray-400">
          or <span className="text-gray-500 group-hover:text-gray-600">browse</span>
        </p>
      </div>
    </div>
  );
}

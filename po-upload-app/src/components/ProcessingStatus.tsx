import type { ProcessingStage } from '../types';

interface ProcessingStatusProps {
  stage: ProcessingStage;
  fileName: string;
}

const stageLabels: Record<ProcessingStage, string> = {
  uploading: 'Uploading',
  extracting: 'Reading PDF',
  parsing: 'Extracting data',
  resolving: 'Finding customer',
  creating: 'Creating order',
  complete: 'Done',
};

export function ProcessingStatus({ stage, fileName }: ProcessingStatusProps) {
  return (
    <div className="flex flex-col items-center pt-8 pb-16">
      {/* Spinner */}
      <div className="relative mb-8">
        <div className="w-10 h-10 border-[3px] border-gray-100 rounded-full" />
        <div className="absolute inset-0 w-10 h-10 border-[3px] border-gray-900 border-t-transparent rounded-full animate-spin" />
      </div>

      {/* Text */}
      <p className="text-[17px] font-medium text-gray-900 mb-1">
        {stageLabels[stage]}
      </p>
      <p className="text-[13px] text-gray-400">{fileName}</p>
    </div>
  );
}

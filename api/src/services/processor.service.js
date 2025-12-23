import config from '../config/index.js';
import logger from '../utils/logger.js';
import queueService from './queue.service.js';
import boiClient from './boiClient.service.js';

let processorInterval = null;
let isProcessing = false;

/**
 * Process a single job from the queue
 */
async function processJob(job) {
  const { jobId, data, attempts } = job;
  
  try {
    logger.info('Processing job', {
      jobId,
      customer: data.customerNumber,
      attempts,
    });

    // Update job status to processing
    await queueService.updateJobStatus(jobId, 'processing', {
      attempts: attempts + 1,
    });

    // Call BOI service to create the sales order
    const result = await boiClient.createSalesOrder(data);

    if (result.success) {
      // Success - update job status
      await queueService.updateJobStatus(jobId, 'completed', {
        result,
      });
      
      logger.info('Job completed successfully', {
        jobId,
        salesOrderNumber: result.salesOrderNumber,
      });
    } else {
      // Failed - check if we should retry
      const newAttempts = attempts + 1;
      
      if (newAttempts >= config.queue.maxRetries) {
        // Max retries reached - mark as failed
        await queueService.updateJobStatus(jobId, 'failed', {
          error: result,
          attempts: newAttempts,
        });
        
        logger.error('Job failed after max retries', {
          jobId,
          attempts: newAttempts,
          errorCode: result.errorCode,
          errorMessage: result.errorMessage,
        });
      } else {
        // Retry - put back in queue
        logger.warn('Job failed, will retry', {
          jobId,
          attempts: newAttempts,
          errorCode: result.errorCode,
        });
        
        await queueService.updateJobStatus(jobId, 'queued', {
          error: result,
          attempts: newAttempts,
        });
        
        // Add back to queue after delay
        setTimeout(async () => {
          await queueService.redis.lpush(
            job.priority === 'high' ? 'sage:queue:high' : 'sage:queue:normal',
            jobId
          );
        }, config.queue.retryDelay);
      }
    }
  } catch (error) {
    logger.error('Error processing job', {
      jobId,
      error: error.message,
      stack: error.stack,
    });
    
    // Update job as failed
    await queueService.updateJobStatus(jobId, 'failed', {
      error: {
        errorCode: 'PROCESSOR_ERROR',
        errorMessage: error.message,
      },
      attempts: attempts + 1,
    });
  }
}

/**
 * Queue processor loop
 */
async function processQueue() {
  // Prevent concurrent processing
  if (isProcessing) {
    return;
  }

  isProcessing = true;

  try {
    // Check if BOI service is ready
    const isReady = await boiClient.isReady();
    
    if (!isReady) {
      logger.warn('BOI service not ready, skipping queue processing');
      isProcessing = false;
      return;
    }

    // Get next job from queue (priority first)
    const job = await queueService.getNextJob(true);
    
    if (job) {
      await processJob(job);
    }
  } catch (error) {
    logger.error('Error in queue processor', {
      error: error.message,
      stack: error.stack,
    });
  } finally {
    isProcessing = false;
  }
}

/**
 * Start the queue processor
 */
export function startQueueProcessor() {
  if (processorInterval) {
    logger.warn('Queue processor already running');
    return;
  }

  logger.info('Starting queue processor', {
    interval: config.queue.checkInterval,
    maxRetries: config.queue.maxRetries,
  });

  // Run immediately
  processQueue();

  // Then run on interval
  processorInterval = setInterval(processQueue, config.queue.checkInterval);
}

/**
 * Stop the queue processor
 */
export function stopQueueProcessor() {
  if (processorInterval) {
    clearInterval(processorInterval);
    processorInterval = null;
    logger.info('Queue processor stopped');
  }
}

export default {
  startQueueProcessor,
  stopQueueProcessor,
};


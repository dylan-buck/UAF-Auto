import { validateSalesOrder } from '../services/validation.service.js';
import queueService from '../services/queue.service.js';
import boiClient from '../services/boiClient.service.js';
import logger from '../utils/logger.js';

/**
 * Create a new sales order
 */
export const createSalesOrder = async (req, res, next) => {
  try {
    // Validate request body
    const { error, value } = validateSalesOrder(req.body);
    
    if (error) {
      logger.warn('Validation error', {
        errors: error.details.map(d => d.message),
      });
      
      return res.status(400).json({
        error: 'ValidationError',
        message: 'Invalid sales order data',
        details: error.details.map(d => ({
          field: d.path.join('.'),
          message: d.message,
        })),
      });
    }

    const orderData = value;
    const isAsync = orderData.async || false;

    // Create job in Redis
    const jobId = await queueService.createJob(orderData, orderData.priority);

    logger.info('Sales order request received', {
      jobId,
      customer: orderData.customerNumber,
      poNumber: orderData.poNumber,
      async: isAsync,
    });

    if (isAsync) {
      // Asynchronous processing - return immediately with job ID
      return res.status(202).json({
        jobId,
        status: 'queued',
        message: 'Order queued for processing',
      });
    } else {
      // Synchronous processing - process now and return result
      await queueService.updateJobStatus(jobId, 'processing');
      
      const result = await boiClient.createSalesOrderWithRetry(orderData);
      
      if (result.success) {
        await queueService.updateJobStatus(jobId, 'completed', {
          result: result,
        });
        
        logger.info('Sales order created successfully', {
          jobId,
          salesOrderNumber: result.salesOrderNumber,
        });
        
        return res.status(200).json({
          jobId,
          status: 'completed',
          salesOrderNumber: result.salesOrderNumber,
          message: 'Order created successfully',
        });
      } else {
        await queueService.updateJobStatus(jobId, 'failed', {
          error: result,
          attempts: 1,
        });
        
        logger.error('Failed to create sales order', {
          jobId,
          errorCode: result.errorCode,
          errorMessage: result.errorMessage,
        });
        
        return res.status(400).json({
          jobId,
          status: 'failed',
          errorCode: result.errorCode,
          errorMessage: result.errorMessage,
        });
      }
    }
  } catch (error) {
    next(error);
  }
};

/**
 * Get job status
 */
export const getJobStatus = async (req, res, next) => {
  try {
    const { jobId } = req.params;
    
    const job = await queueService.getJob(jobId);
    
    if (!job) {
      return res.status(404).json({
        error: 'NotFound',
        message: 'Job not found',
      });
    }

    const response = {
      jobId: job.jobId,
      status: job.status,
      createdAt: job.createdAt,
      processedAt: job.processedAt,
    };

    if (job.status === 'completed' && job.result) {
      response.result = job.result;
    } else if (job.status === 'failed' && job.error) {
      response.error = job.error;
      response.attempts = job.attempts;
    }

    res.json(response);
  } catch (error) {
    next(error);
  }
};

export default {
  createSalesOrder,
  getJobStatus,
};


import axios from 'axios';
import config from '../config/index.js';
import logger from '../utils/logger.js';

class BOIClientService {
  constructor() {
    this.baseURL = config.boi.serviceUrl;
    this.client = axios.create({
      baseURL: this.baseURL,
      timeout: 30000, // 30 seconds
      headers: {
        'Content-Type': 'application/json',
      },
    });
  }

  /**
   * Create a sales order via the BOI service
   * @param {Object} orderData - Sales order data
   * @returns {Promise<Object>} - BOI result
   */
  async createSalesOrder(orderData) {
    try {
      logger.info('Calling BOI service to create sales order', {
        customer: orderData.customerNumber,
        poNumber: orderData.poNumber,
      });

      const response = await this.client.post('/api/salesorder', orderData);
      
      logger.info('BOI service response received', {
        success: response.data.success,
        salesOrderNumber: response.data.salesOrderNumber,
      });

      return response.data;
    } catch (error) {
      if (error.response) {
        // Server responded with error status
        logger.error('BOI service returned error', {
          status: error.response.status,
          data: error.response.data,
        });
        
        return {
          success: false,
          errorCode: error.response.data?.errorCode || 'BOI_SERVICE_ERROR',
          errorMessage: error.response.data?.errorMessage || error.message,
        };
      } else if (error.request) {
        // Request made but no response
        logger.error('BOI service not responding', {
          error: error.message,
        });
        
        return {
          success: false,
          errorCode: 'BOI_SERVICE_UNAVAILABLE',
          errorMessage: 'BOI service is not responding',
        };
      } else {
        // Something else happened
        logger.error('Error calling BOI service', {
          error: error.message,
        });
        
        return {
          success: false,
          errorCode: 'BOI_CLIENT_ERROR',
          errorMessage: error.message,
        };
      }
    }
  }

  /**
   * Create sales order with retry logic
   * @param {Object} orderData - Sales order data
   * @param {number} maxRetries - Maximum retry attempts
   * @returns {Promise<Object>} - BOI result
   */
  async createSalesOrderWithRetry(orderData, maxRetries = config.queue.maxRetries) {
    let lastError;
    
    for (let attempt = 1; attempt <= maxRetries; attempt++) {
      logger.debug(`Attempt ${attempt}/${maxRetries} to create sales order`);
      
      const result = await this.createSalesOrder(orderData);
      
      if (result.success) {
        return result;
      }
      
      lastError = result;
      
      // Don't retry if it's a validation error
      if (result.errorCode && (
        result.errorCode.startsWith('MISSING_') ||
        result.errorCode.startsWith('INVALID_')
      )) {
        logger.warn('Not retrying due to validation error', {
          errorCode: result.errorCode,
        });
        break;
      }
      
      // Wait before retrying (exponential backoff)
      if (attempt < maxRetries) {
        const delay = Math.min(1000 * Math.pow(2, attempt - 1), 30000); // Max 30s
        logger.info(`Waiting ${delay}ms before retry...`);
        await new Promise(resolve => setTimeout(resolve, delay));
      }
    }
    
    return lastError;
  }

  /**
   * Check if BOI service is healthy
   * @returns {Promise<boolean>} - True if healthy
   */
  async isHealthy() {
    try {
      const response = await this.client.get('/health', { timeout: 5000 });
      return response.status === 200;
    } catch (error) {
      logger.error('BOI service health check failed', {
        error: error.message,
      });
      return false;
    }
  }

  /**
   * Check if BOI service is ready (can connect to Sage 100)
   * @returns {Promise<boolean>} - True if ready
   */
  async isReady() {
    try {
      const response = await this.client.get('/health/ready', { timeout: 10000 });
      return response.status === 200;
    } catch (error) {
      logger.error('BOI service readiness check failed', {
        error: error.message,
      });
      return false;
    }
  }
}

// Export singleton instance
export default new BOIClientService();


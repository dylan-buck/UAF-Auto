import queueService from '../services/queue.service.js';
import boiClient from '../services/boiClient.service.js';
import logger from '../utils/logger.js';

/**
 * Basic health check
 */
export const healthCheck = (req, res) => {
  res.json({
    status: 'healthy',
    service: 'uaf-sage-api',
    timestamp: new Date().toISOString(),
  });
};

/**
 * Readiness check - includes dependencies
 */
export const readinessCheck = async (req, res) => {
  const checks = {
    redis: false,
    boiService: false,
  };

  try {
    // Check Redis
    checks.redis = queueService.isConnected();

    // Check BOI Service
    checks.boiService = await boiClient.isHealthy();

    // Get queue depth
    const queueDepth = await queueService.getQueueDepth();

    const allHealthy = Object.values(checks).every(check => check === true);

    const response = {
      status: allHealthy ? 'ready' : 'not_ready',
      checks,
      queueDepth,
      timestamp: new Date().toISOString(),
    };

    if (allHealthy) {
      res.json(response);
    } else {
      res.status(503).json(response);
    }
  } catch (error) {
    logger.error('Readiness check failed', { error: error.message });
    
    res.status(503).json({
      status: 'not_ready',
      checks,
      error: error.message,
      timestamp: new Date().toISOString(),
    });
  }
};

export default {
  healthCheck,
  readinessCheck,
};


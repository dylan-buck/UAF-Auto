import config from '../config/index.js';
import logger from '../utils/logger.js';

/**
 * Simple API key authentication middleware
 */
export const authenticate = (req, res, next) => {
  const apiKey = req.headers['x-api-key'];
  
  if (!apiKey) {
    logger.warn('Request without API key', {
      ip: req.ip,
      path: req.path,
    });
    
    return res.status(401).json({
      error: 'Unauthorized',
      message: 'Missing API key',
    });
  }
  
  if (apiKey !== config.apiKey) {
    logger.warn('Request with invalid API key', {
      ip: req.ip,
      path: req.path,
      providedKey: apiKey.substring(0, 8) + '...',
    });
    
    return res.status(401).json({
      error: 'Unauthorized',
      message: 'Invalid API key',
    });
  }
  
  // API key is valid, continue
  next();
};

export default authenticate;


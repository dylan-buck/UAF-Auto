import logger from '../utils/logger.js';

/**
 * Global error handling middleware
 */
export const errorHandler = (err, req, res, next) => {
  // Log the error
  logger.error('Error processing request', {
    error: err.message,
    stack: err.stack,
    path: req.path,
    method: req.method,
    body: req.body,
  });
  
  // Determine status code
  const statusCode = err.statusCode || 500;
  
  // Build error response
  const response = {
    error: err.name || 'InternalServerError',
    message: err.message || 'An unexpected error occurred',
  };
  
  // Include additional details in development
  if (process.env.NODE_ENV === 'development') {
    response.stack = err.stack;
  }
  
  // Send error response
  res.status(statusCode).json(response);
};

/**
 * 404 Not Found handler
 */
export const notFoundHandler = (req, res) => {
  logger.warn('Route not found', {
    path: req.path,
    method: req.method,
  });
  
  res.status(404).json({
    error: 'NotFound',
    message: `Route ${req.method} ${req.path} not found`,
  });
};

export default errorHandler;


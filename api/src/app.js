import express from 'express';
import salesOrdersRoutes from './routes/salesOrders.routes.js';
import healthRoutes from './routes/health.routes.js';
import { errorHandler, notFoundHandler } from './middleware/errorHandler.middleware.js';
import logger from './utils/logger.js';

const app = express();

// Trust proxy (for proper IP detection behind Docker/nginx)
app.set('trust proxy', true);

// Parse JSON request bodies
app.use(express.json({ limit: '1mb' }));
app.use(express.urlencoded({ extended: true, limit: '1mb' }));

// Request logging middleware
app.use((req, res, next) => {
  const start = Date.now();
  
  res.on('finish', () => {
    const duration = Date.now() - start;
    logger.info('HTTP Request', {
      method: req.method,
      path: req.path,
      status: res.statusCode,
      duration: `${duration}ms`,
      ip: req.ip,
    });
  });
  
  next();
});

// Health check routes (no authentication required)
app.use('/health', healthRoutes);

// API routes
app.use('/api/v1/sales-orders', salesOrdersRoutes);

// 404 handler - must be after all routes
app.use(notFoundHandler);

// Global error handler - must be last
app.use(errorHandler);

export default app;


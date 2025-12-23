import app from './app.js';
import config from './config/index.js';
import logger from './utils/logger.js';
import queueService from './services/queue.service.js';
import { startQueueProcessor, stopQueueProcessor } from './services/processor.service.js';

const PORT = config.port;
let server;

// Start the server
const startServer = () => {
  server = app.listen(PORT, () => {
    logger.info(`🚀 UAF Sage API Server started`, {
      port: PORT,
      env: config.env,
    });
  });

  // Start the background queue processor
  startQueueProcessor();
  logger.info('✅ Queue processor started');
};

// Graceful shutdown
const gracefulShutdown = async (signal) => {
  logger.info(`${signal} received, starting graceful shutdown...`);

  // Stop accepting new connections
  if (server) {
    server.close(async () => {
      logger.info('HTTP server closed');

      try {
        // Stop queue processor
        stopQueueProcessor();
        logger.info('Queue processor stopped');

        // Close Redis connection
        await queueService.close();
        logger.info('Redis connection closed');

        logger.info('✅ Graceful shutdown completed');
        process.exit(0);
      } catch (error) {
        logger.error('Error during graceful shutdown', {
          error: error.message,
        });
        process.exit(1);
      }
    });

    // Force shutdown after 30 seconds
    setTimeout(() => {
      logger.error('❌ Forced shutdown after timeout');
      process.exit(1);
    }, 30000);
  } else {
    process.exit(0);
  }
};

// Handle shutdown signals
process.on('SIGTERM', () => gracefulShutdown('SIGTERM'));
process.on('SIGINT', () => gracefulShutdown('SIGINT'));

// Handle uncaught errors
process.on('uncaughtException', (error) => {
  logger.error('Uncaught exception', {
    error: error.message,
    stack: error.stack,
  });
  gracefulShutdown('uncaughtException');
});

process.on('unhandledRejection', (reason, promise) => {
  logger.error('Unhandled rejection', {
    reason: reason,
    promise: promise,
  });
  gracefulShutdown('unhandledRejection');
});

// Start the server
startServer();


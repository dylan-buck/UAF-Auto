import winston from 'winston';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';
import config from '../config/index.js';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const logDir = join(__dirname, '../../../logs/api');

// Define log format
const logFormat = winston.format.combine(
  winston.format.timestamp({ format: 'YYYY-MM-DD HH:mm:ss' }),
  winston.format.errors({ stack: true }),
  winston.format.splat(),
  winston.format.json()
);

// Console format for development
const consoleFormat = winston.format.combine(
  winston.format.colorize(),
  winston.format.timestamp({ format: 'HH:mm:ss' }),
  winston.format.printf(({ timestamp, level, message, ...meta }) => {
    let metaStr = '';
    if (Object.keys(meta).length > 0) {
      metaStr = ` ${JSON.stringify(meta)}`;
    }
    return `[${timestamp}] ${level}: ${message}${metaStr}`;
  })
);

// Create logger
const logger = winston.createLogger({
  level: config.logging.level,
  format: logFormat,
  defaultMeta: { service: 'uaf-sage-api' },
  transports: [
    // Write all logs to combined.log
    new winston.transports.File({
      filename: join(logDir, 'combined.log'),
      maxsize: 10 * 1024 * 1024, // 10MB
      maxFiles: 10,
    }),
    
    // Write errors to error.log
    new winston.transports.File({
      filename: join(logDir, 'error.log'),
      level: 'error',
      maxsize: 10 * 1024 * 1024, // 10MB
      maxFiles: 30,
    }),
    
    // Write access logs to access.log
    new winston.transports.File({
      filename: join(logDir, 'access.log'),
      maxsize: 10 * 1024 * 1024, // 10MB
      maxFiles: 7,
    }),
  ],
});

// If not in production, also log to console
if (config.env !== 'production') {
  logger.add(new winston.transports.Console({
    format: consoleFormat,
  }));
}

// Create a stream object for Morgan HTTP logging
logger.stream = {
  write: (message) => {
    logger.info(message.trim());
  },
};

export default logger;


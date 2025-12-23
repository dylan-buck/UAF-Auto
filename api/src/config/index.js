import dotenv from 'dotenv';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

// Load environment variables
dotenv.config({ path: join(__dirname, '../../../.env') });

const config = {
  env: process.env.NODE_ENV || 'development',
  port: parseInt(process.env.API_PORT, 10) || 3000,
  apiKey: process.env.API_KEY || 'your_api_key_here',
  
  boi: {
    serviceUrl: process.env.BOI_SERVICE_URL || 'http://localhost:5000',
  },
  
  redis: {
    host: process.env.REDIS_HOST || 'localhost',
    port: parseInt(process.env.REDIS_PORT, 10) || 6379,
    password: process.env.REDIS_PASSWORD || undefined,
    db: parseInt(process.env.REDIS_DB, 10) || 0,
  },
  
  logging: {
    level: process.env.LOG_LEVEL || 'info',
    dir: process.env.LOG_DIR || './logs',
  },
  
  queue: {
    checkInterval: parseInt(process.env.QUEUE_CHECK_INTERVAL, 10) || 10000,
    maxRetries: parseInt(process.env.QUEUE_MAX_RETRIES, 10) || 3,
    retryDelay: parseInt(process.env.QUEUE_RETRY_DELAY, 10) || 30000,
  },
  
  job: {
    completedTTL: parseInt(process.env.JOB_COMPLETED_TTL, 10) || 604800, // 7 days
    failedTTL: parseInt(process.env.JOB_FAILED_TTL, 10) || 0, // No expiry
  },
};

// Validate required configuration
const requiredVars = ['API_KEY'];
const missing = requiredVars.filter(varName => !process.env[varName]);

if (missing.length > 0) {
  console.warn(`⚠️  Warning: Missing required environment variables: ${missing.join(', ')}`);
}

export default config;


import Redis from 'ioredis';
import config from '../config/index.js';
import logger from '../utils/logger.js';

class QueueService {
  constructor() {
    this.redis = new Redis({
      host: config.redis.host,
      port: config.redis.port,
      password: config.redis.password,
      db: config.redis.db,
      retryStrategy: (times) => {
        const delay = Math.min(times * 50, 2000);
        return delay;
      },
    });

    this.redis.on('connect', () => {
      logger.info('Connected to Redis');
    });

    this.redis.on('error', (err) => {
      logger.error('Redis connection error', { error: err.message });
    });
  }

  /**
   * Generate a unique job ID
   * @returns {string} - Job ID
   */
  generateJobId() {
    const timestamp = new Date();
    const dateStr = timestamp.toISOString().slice(0, 10).replace(/-/g, '-');
    const timeStr = timestamp.toISOString().slice(11, 19).replace(/:/g, '');
    const random = Math.random().toString(36).substring(2, 8);
    return `job-${random}-${dateStr}-${timeStr}`;
  }

  /**
   * Create a new job
   * @param {Object} data - Job data
   * @param {string} priority - Job priority ('normal' or 'high')
   * @returns {Promise<string>} - Job ID
   */
  async createJob(data, priority = 'normal') {
    const jobId = this.generateJobId();
    
    const job = {
      jobId,
      status: 'queued',
      priority,
      attempts: 0,
      maxAttempts: config.queue.maxRetries,
      createdAt: new Date().toISOString(),
      processedAt: null,
      data,
      result: null,
      error: null,
    };

    // Store job data
    await this.redis.hmset(`job:${jobId}`, this.flattenObject(job));

    // Add to appropriate queue
    const queueKey = priority === 'high' ? 'sage:queue:high' : 'sage:queue:normal';
    await this.redis.lpush(queueKey, jobId);

    logger.info('Job created', {
      jobId,
      priority,
      customer: data.customerNumber,
    });

    return jobId;
  }

  /**
   * Get job status and data
   * @param {string} jobId - Job ID
   * @returns {Promise<Object|null>} - Job data or null if not found
   */
  async getJob(jobId) {
    const jobData = await this.redis.hgetall(`job:${jobId}`);
    
    if (!jobData || Object.keys(jobData).length === 0) {
      return null;
    }

    return this.unflattenObject(jobData);
  }

  /**
   * Update job status
   * @param {string} jobId - Job ID
   * @param {string} status - New status
   * @param {Object} updates - Additional fields to update
   * @returns {Promise<void>}
   */
  async updateJobStatus(jobId, status, updates = {}) {
    const updateData = {
      status,
      ...updates,
    };

    if (status === 'processing' && !updates.processedAt) {
      updateData.processedAt = new Date().toISOString();
    }

    await this.redis.hmset(`job:${jobId}`, this.flattenObject(updateData));

    // Set TTL for completed jobs
    if (status === 'completed' && config.job.completedTTL > 0) {
      await this.redis.expire(`job:${jobId}`, config.job.completedTTL);
    }

    logger.debug('Job status updated', {
      jobId,
      status,
    });
  }

  /**
   * Get next job from queue
   * @param {boolean} priorityFirst - Check high priority queue first
   * @returns {Promise<Object|null>} - Job data or null if queue is empty
   */
  async getNextJob(priorityFirst = true) {
    let jobId;

    if (priorityFirst) {
      // Try high priority queue first
      jobId = await this.redis.rpop('sage:queue:high');
    }

    // Fall back to normal priority
    if (!jobId) {
      jobId = await this.redis.rpop('sage:queue:normal');
    }

    if (!jobId) {
      return null;
    }

    const job = await this.getJob(jobId);
    
    if (!job) {
      logger.warn('Job not found in Redis', { jobId });
      return null;
    }

    return job;
  }

  /**
   * Get queue depth
   * @returns {Promise<Object>} - Queue depths
   */
  async getQueueDepth() {
    const highCount = await this.redis.llen('sage:queue:high');
    const normalCount = await this.redis.llen('sage:queue:normal');

    return {
      high: highCount,
      normal: normalCount,
      total: highCount + normalCount,
    };
  }

  /**
   * Check if Redis is connected
   * @returns {boolean} - Connection status
   */
  isConnected() {
    return this.redis.status === 'ready';
  }

  /**
   * Flatten nested objects for Redis hash storage
   * @param {Object} obj - Object to flatten
   * @returns {Object} - Flattened object
   */
  flattenObject(obj) {
    const flattened = {};
    for (const [key, value] of Object.entries(obj)) {
      if (value === null || value === undefined) {
        flattened[key] = '';
      } else if (typeof value === 'object') {
        flattened[key] = JSON.stringify(value);
      } else {
        flattened[key] = value.toString();
      }
    }
    return flattened;
  }

  /**
   * Unflatten object from Redis hash
   * @param {Object} obj - Flattened object
   * @returns {Object} - Unflattened object
   */
  unflattenObject(obj) {
    const unflattened = {};
    for (const [key, value] of Object.entries(obj)) {
      if (value === '') {
        unflattened[key] = null;
      } else if (key === 'attempts' || key === 'maxAttempts') {
        unflattened[key] = parseInt(value, 10);
      } else if (key === 'data' || key === 'result' || key === 'error') {
        try {
          unflattened[key] = value ? JSON.parse(value) : null;
        } catch {
          unflattened[key] = value;
        }
      } else {
        unflattened[key] = value;
      }
    }
    return unflattened;
  }

  /**
   * Close Redis connection
   * @returns {Promise<void>}
   */
  async close() {
    await this.redis.quit();
    logger.info('Redis connection closed');
  }
}

// Export singleton instance
export default new QueueService();


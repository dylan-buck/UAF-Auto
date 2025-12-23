import express from 'express';
import { healthCheck, readinessCheck } from '../controllers/health.controller.js';

const router = express.Router();

// GET /health - Basic health check
router.get('/', healthCheck);

// GET /health/ready - Readiness check
router.get('/ready', readinessCheck);

export default router;


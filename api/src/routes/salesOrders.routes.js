import express from 'express';
import { createSalesOrder, getJobStatus } from '../controllers/salesOrder.controller.js';
import authenticate from '../middleware/auth.middleware.js';

const router = express.Router();

// All sales order routes require authentication
router.use(authenticate);

// POST /api/v1/sales-orders - Create new sales order
router.post('/', createSalesOrder);

// GET /api/v1/sales-orders/:jobId - Get job status
router.get('/:jobId', getJobStatus);

export default router;


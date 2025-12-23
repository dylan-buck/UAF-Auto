import Joi from 'joi';

// Address schema
const addressSchema = Joi.object({
  name: Joi.string().required(),
  address1: Joi.string().required(),
  address2: Joi.string().allow('', null).optional(),
  city: Joi.string().required(),
  state: Joi.string().length(2).required(),
  zipCode: Joi.string().pattern(/^\d{5}(-\d{4})?$/).required(),
  country: Joi.string().length(2).optional(),
});

// Sales order line item schema
const salesOrderLineSchema = Joi.object({
  itemCode: Joi.string().required(),
  quantity: Joi.number().positive().required(),
  unitPrice: Joi.number().min(0).optional(),
  description: Joi.string().optional(),
  warehouseCode: Joi.string().optional(),
});

// Sales order schema
export const salesOrderSchema = Joi.object({
  customerNumber: Joi.string().required(),
  poNumber: Joi.string().required(),
  orderDate: Joi.string().pattern(/^\d{8}$/).optional(), // YYYYMMDD format
  shipDate: Joi.string().pattern(/^\d{8}$/).optional(),  // YYYYMMDD format
  comment: Joi.string().max(500).optional(),
  shipToAddress: addressSchema.optional(),
  lines: Joi.array().items(salesOrderLineSchema).min(1).required(),
  async: Joi.boolean().optional().default(false),
  priority: Joi.string().valid('normal', 'high').optional().default('normal'),
});

/**
 * Validate sales order data
 * @param {Object} data - The sales order data to validate
 * @returns {Object} - Validation result with error or value
 */
export const validateSalesOrder = (data) => {
  return salesOrderSchema.validate(data, {
    abortEarly: false,
    stripUnknown: true,
  });
};

/**
 * Validate job ID format
 * @param {string} jobId - The job ID to validate
 * @returns {boolean} - True if valid
 */
export const isValidJobId = (jobId) => {
  return /^job-[a-z0-9]+-\d{4}-\d{2}-\d{2}-\d{6}$/.test(jobId);
};

export default {
  validateSalesOrder,
  isValidJobId,
  salesOrderSchema,
};


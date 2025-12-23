using SageBOI.Api.Models;

namespace SageBOI.Api.Services;

public interface ISalesOrderService
{
    Task<BOIResult> CreateSalesOrderAsync(SalesOrderDTO order);
}


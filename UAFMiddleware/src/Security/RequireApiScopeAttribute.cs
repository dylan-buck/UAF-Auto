using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UAFMiddleware.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class RequireApiScopeAttribute : Attribute, IAsyncActionFilter
{
    private readonly string _scope;

    public RequireApiScopeAttribute(string scope)
    {
        _scope = scope;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.Items["ApiScopeResult"] is ApiScopeResult result &&
            result.HasScope(_scope))
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new
        {
            error = "Insufficient API scope",
            requiredScope = _scope
        })
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }
}

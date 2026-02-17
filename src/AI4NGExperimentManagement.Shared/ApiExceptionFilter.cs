using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AI4NGExperimentManagement.Shared;

public sealed class ApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.Result = ApiExceptionMapper.Map(context.Exception);
        context.ExceptionHandled = true;
    }
}
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AI4NGExperimentManagement.Shared;

[ApiController]
public abstract class BaseApiController : ControllerBase
{
    protected readonly IAuthenticationService _authService;

    protected BaseApiController(IAuthenticationService authService)
    {
        _authService = authService;
    }

    protected ActionResult HandleException(Exception ex, string operation)
    {
        Console.Error.WriteLine($"Error during {operation}: {ex}");
        return (ActionResult)ApiExceptionMapper.Map(ex);
    }

    protected string GetAuthenticatedUsername()
    {
        var username = _authService.GetUsernameFromRequest();
        if (string.IsNullOrEmpty(username))
            throw new UnauthorizedAccessException("Authentication required");
        return username;
    }

    protected ActionResult RequireResearcher()
    {
        if (!_authService.IsResearcher())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "Participants cannot perform this action" });

        return null!;
    }
}
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

    protected string GetAuthenticatedUsername()
    {
        var username = _authService.GetUsernameFromRequest();
        if (string.IsNullOrEmpty(username))
            throw new UnauthorizedAccessException("Authentication required");
        return username;
    }

    protected void RequireResearcher()
    {
        if (!_authService.IsResearcher())
            throw new ForbiddenException("Participants cannot perform this action");
    }
}
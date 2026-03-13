using Microsoft.AspNetCore.Mvc;
using AI4NGExperimentManagement.Shared;
using AI4NGExperimentsLambda.Interfaces.Researcher;
using AI4NGExperimentsLambda.Models;
using AI4NGExperimentsLambda.Models.Dtos;


namespace AI4NGExperimentsLambda.Controllers.Researcher;

[Route("api/researcher/users")]
public class UserLookupController : BaseApiController
{
    private readonly IUserLookupService _userLookupService;

    public UserLookupController(IUserLookupService userLookupService, IAuthenticationService authService)
        : base(authService)
    {
        _userLookupService = userLookupService;
    }

    [HttpGet("by-email")]
    public async Task<IActionResult> GetByEmail([FromQuery] string email, CancellationToken ct)
    {
        RequireResearcher();

        var user = await _userLookupService.GetByEmailAsync(email, ct);
        return user == null ? NotFound() : Ok(user);
    }

    [HttpGet("by-username")]
    public async Task<IActionResult> GetByUsername([FromQuery] string username, CancellationToken ct)
    {
        RequireResearcher();

        var user = await _userLookupService.GetByUsernameAsync(username, ct);
        return user == null ? NotFound() : Ok(user);
    }
}
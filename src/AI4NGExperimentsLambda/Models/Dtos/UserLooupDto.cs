namespace AI4NGExperimentsLambda.Models.Dtos;

public class UserLookupDto
{
    public string UserSub { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool? Enabled { get; set; }
    public string? UserStatus { get; set; }
}
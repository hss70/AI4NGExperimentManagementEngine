namespace AI4NGResponsesLambda.Models;

public class Response
{
    public string Id { get; set; } = string.Empty;
    public ResponseData Data { get; set; } = new();
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}


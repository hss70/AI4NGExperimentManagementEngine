using Xunit;

namespace AI4NGQuestionnaires.Tests;

[CollectionDefinition("QuestionnairesCollection")]
public class QuestionnairesCollectionFixture : ICollectionFixture<EnvironmentFixture>
{
}

public class EnvironmentFixture : IDisposable
{
    private readonly string? _originalEndpointUrl;

    public EnvironmentFixture()
    {
        _originalEndpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", _originalEndpointUrl);
    }
}
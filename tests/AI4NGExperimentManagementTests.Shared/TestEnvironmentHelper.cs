namespace AI4NGExperimentManagementTests.Shared;

public static class TestEnvironmentHelper
{
    public static IDisposable SetLocalTestingMode()
    {
        return new EnvironmentScope("http://localhost:8000");
    }
    
    public static IDisposable SetProductionMode()
    {
        return new EnvironmentScope(null);
    }
    
    private class EnvironmentScope : IDisposable
    {
        private readonly string? _originalValue;
        
        public EnvironmentScope(string? value)
        {
            _originalValue = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
            Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", value);
        }
        
        public void Dispose()
        {
            Environment.SetEnvironmentVariable("AWS_ENDPOINT_URL", _originalValue);
        }
    }
}
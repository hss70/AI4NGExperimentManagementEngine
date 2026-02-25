namespace AI4NGExperimentManagement.Shared;


    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message) : base(message) { }
    }
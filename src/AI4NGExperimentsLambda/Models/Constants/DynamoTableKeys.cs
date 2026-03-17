namespace AI4NGExperimentsLambda.Models.Constants;

public static class DynamoTableKeys
{
    public const string MetadataSk = "METADATA";
    public const string ConfigSk = "CONFIG";

    public const string ExperimentPkPrefix = "EXPERIMENT#";
    public const string ExperimentGsiPk = "EXPERIMENT";

    public const string UserPkPrefix = "USER#";

    public const string MemberSkPrefix = "MEMBER#";
    public const string MembershipType = "Membership";

    public const string ProtocolSkPrefix = "PROTOCOL_SESSION#";
    public const string ProtocolSessionSkPrefix = "PROTOCOL_SESSION#";

    public const string SessionSkPrefix = "SESSION#";

    public const string TaskPkPrefix = "TASK#";
    public const string TaskGsiPk = "TASK";

    public const string QuestionnairePkPrefix = "QUESTIONNAIRE#";
}

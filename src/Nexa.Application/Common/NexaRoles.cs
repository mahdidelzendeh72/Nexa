namespace Nexa.Application.Common;

public static class NexaRoles
{
    public const string Admin = nameof(Admin);
    public const string AgentAdmin = nameof(AgentAdmin);
    public const string Developer = nameof(Developer);
    public const string User = nameof(User);
    public const string Viewer = nameof(Viewer);

    public static readonly string[] All =
    [
        Admin,
        AgentAdmin,
        Developer,
        User,
        Viewer
    ];
}

public static class NexaPolicies
{
    public const string CanManageAgents = nameof(CanManageAgents);
    public const string CanManageModels = nameof(CanManageModels);
    public const string CanChat = nameof(CanChat);
    public const string CanView = nameof(CanView);
}

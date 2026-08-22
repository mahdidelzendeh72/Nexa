namespace Nexa.Application.Abstractions;

public sealed record ToolDescriptor(
    string Id,
    string Name,
    string Description,
    string Category,
    string Version,
    bool IsEnabled,
    bool RequiresApproval,
    string SecurityLevel);

public interface IToolRegistry
{
    IReadOnlyList<ToolDescriptor> List();
    IReadOnlyList<ToolDescriptor> ResolveEnabled(IReadOnlyList<string>? selectedIds);
}

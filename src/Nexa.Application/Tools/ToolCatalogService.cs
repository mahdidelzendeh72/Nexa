using Nexa.Application.Abstractions;
using Nexa.Contracts.Runtime;

namespace Nexa.Application.Tools;

public interface IToolCatalogService
{
    IReadOnlyList<ToolDescriptorDto> List();
}

public sealed class ToolCatalogService(IToolRegistry registry) : IToolCatalogService
{
    public IReadOnlyList<ToolDescriptorDto> List() =>
        registry.List().Select(t => new ToolDescriptorDto(
            t.Id,
            t.Name,
            t.Description,
            t.Category,
            t.Version,
            t.IsEnabled,
            t.RequiresApproval,
            t.SecurityLevel)).ToList();
}

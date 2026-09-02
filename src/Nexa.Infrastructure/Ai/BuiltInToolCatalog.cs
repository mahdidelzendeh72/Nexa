using System.ComponentModel;
using Microsoft.Extensions.AI;
using Nexa.Application.Abstractions;

namespace Nexa.Infrastructure.Ai;

internal static class BuiltInFunctions
{
    [Description("Returns the current UTC date and time in ISO-8601 format.")]
    public static string GetUtcNow() => DateTimeOffset.UtcNow.ToString("O");

    [Description("Adds two numbers and returns the sum.")]
    public static double AddNumbers(
        [Description("The first addend.")] double a,
        [Description("The second addend.")] double b) => a + b;
}

internal sealed class BuiltInToolCatalog : IToolRegistry
{
    public const string UtcNowId = "utc_now";
    public const string AddNumbersId = "add_numbers";

    private static readonly ToolDescriptor[] Descriptors =
    [
        new(UtcNowId, "utc_now", "Returns the current UTC date and time in ISO-8601 format.", "BuiltIn", "1.0.0", true, false, "Low"),
        new(AddNumbersId, "add_numbers", "Adds two numbers and returns the sum.", "BuiltIn", "1.0.0", true, false, "Low")
    ];

    public IReadOnlyList<ToolDescriptor> List() => Descriptors;

    public IReadOnlyList<ToolDescriptor> ResolveEnabled(IReadOnlyList<string>? selectedIds)
    {
        IEnumerable<ToolDescriptor> enabled = Descriptors.Where(d => d.IsEnabled);
        if (selectedIds is { Count: > 0 })
        {
            var set = selectedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            enabled = enabled.Where(d => set.Contains(d.Id));
        }

        return enabled.ToList();
    }

    public IList<AITool> ResolveAiTools(IReadOnlyList<string>? selectedIds)
    {
        var ids = ResolveEnabled(selectedIds).Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tools = new List<AITool>();
        if (ids.Contains(UtcNowId))
        {
            tools.Add(AIFunctionFactory.Create(BuiltInFunctions.GetUtcNow, UtcNowId));
        }

        if (ids.Contains(AddNumbersId))
        {
            tools.Add(AIFunctionFactory.Create(BuiltInFunctions.AddNumbers, AddNumbersId));
        }

        return tools;
    }
}

using Microsoft.SemanticKernel;

namespace ollama;

public static class ToolNameResolver
{
    private const string QualifiedNameSeparator = "-";

    public static string ToQualifiedName(KernelFunction function) =>
        $"{function.PluginName}{QualifiedNameSeparator}{function.Name}";

    public static IReadOnlyList<KernelFunction> Resolve(Kernel kernel, IEnumerable<string> names)
    {
        var result = new List<KernelFunction>();

        foreach (var name in names)
        {
            var trimmed = name.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            result.AddRange(ResolveName(kernel, trimmed));
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException("No valid tools were provided.");
        }

        return result;
    }

    public static void PrintAllTools(Kernel kernel)
    {
        if (kernel.Plugins.Count == 0)
        {
            Console.WriteLine("No tools registered.");
            return;
        }

        Console.WriteLine("\nAvailable tools (use 'tools set Plugin-Function,...'):");
        foreach (var plugin in kernel.Plugins)
        {
            foreach (var function in plugin)
            {
                var description = string.IsNullOrWhiteSpace(function.Description)
                    ? "(no description)"
                    : function.Description;
                Console.WriteLine($"  {ToQualifiedName(function)}: {description}");
            }
        }

        Console.WriteLine();
    }

    private static IEnumerable<KernelFunction> ResolveName(Kernel kernel, string name)
    {
        var separator = QualifiedNameSeparator;

        if (name.Contains(separator))
        {
            var separatorIndex = name.IndexOf(separator, StringComparison.Ordinal);
            var pluginName = name[..separatorIndex];
            var functionName = name[(separatorIndex + 1)..];

            if (kernel.Plugins.TryGetFunction(pluginName, functionName, out var function))
            {
                return [function];
            }

            throw new InvalidOperationException($"Tool '{name}' was not found.");
        }

        if (kernel.Plugins.TryGetPlugin(name, out var plugin))
        {
            return plugin.ToList();
        }

        var matches = kernel.Plugins
            .SelectMany(p => p)
            .Where(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches.Count switch
        {
            1 => matches,
            > 1 => throw new InvalidOperationException(
                $"Tool name '{name}' is ambiguous. Use Plugin{separator}Function format."),
            _ => throw new InvalidOperationException($"Tool '{name}' was not found.")
        };
    }
}

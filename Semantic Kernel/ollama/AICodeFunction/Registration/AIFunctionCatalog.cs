using AICodeFunction.Abstractions;
using AICodeFunction.Modules;
using Microsoft.SemanticKernel;

namespace AICodeFunction.Registration;

public static class AIFunctionCatalog
{
    private static readonly Type[] ModuleTypes =
    [
        typeof(CalculatorFunctions),
        typeof(DateTimeFunctions),
    ];

    public static IReadOnlyList<string> RegisteredPluginNames { get; private set; } = [];

    public static int RegisterAll(Kernel kernel)
    {
        var pluginNames = new List<string>();
        var functionCount = 0;

        foreach (var moduleType in ModuleTypes)
        {
            var module = (IAIFunctionModule)Activator.CreateInstance(moduleType)!;
            module.Register(kernel);
            pluginNames.Add(module.PluginName);

            var pluginFunctionCount = 0;
            foreach (var _ in kernel.Plugins[module.PluginName])
            {
                pluginFunctionCount++;
            }

            functionCount += pluginFunctionCount;
            Console.WriteLine($"Registered {pluginFunctionCount} native function(s) from '{module.PluginName}'.");
        }

        RegisteredPluginNames = pluginNames;
        return functionCount;
    }

    public static void PrintTools(Kernel kernel)
    {
        if (RegisteredPluginNames.Count == 0)
        {
            Console.WriteLine("No native C# functions registered.");
            return;
        }

        Console.WriteLine("\nAvailable native C# functions:");
        foreach (var pluginName in RegisteredPluginNames)
        {
            foreach (var function in kernel.Plugins[pluginName])
            {
                var description = string.IsNullOrWhiteSpace(function.Description)
                    ? "(no description)"
                    : function.Description;
                Console.WriteLine($"  [{pluginName}] {function.Name}: {description}");
            }
        }

        Console.WriteLine();
    }
}

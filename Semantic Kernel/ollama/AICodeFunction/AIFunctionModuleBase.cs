using Microsoft.SemanticKernel;
using AICodeFunction.Abstractions;

namespace AICodeFunction;

/// <summary>
/// Base class for native C# plugins. Inherit, add [KernelFunction] methods, and register in <see cref="AIFunctionCatalog"/>.
/// </summary>
public abstract class AIFunctionModuleBase : IAIFunctionModule
{
    public abstract string PluginName { get; }

    public virtual void Register(Kernel kernel) =>
        kernel.Plugins.AddFromObject(this, PluginName);
}

using Microsoft.SemanticKernel;

namespace AICodeFunction.Abstractions;

/// <summary>
/// Contract for a native C# function module that can be registered with Semantic Kernel.
/// </summary>
public interface IAIFunctionModule
{
    string PluginName { get; }

    void Register(Kernel kernel);
}

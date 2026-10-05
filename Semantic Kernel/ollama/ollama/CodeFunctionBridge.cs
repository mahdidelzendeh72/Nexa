using AICodeFunction.Registration;
using Microsoft.SemanticKernel;

namespace ollama;

public sealed class CodeFunctionBridge
{
    public int RegisterFunctions(Kernel kernel) =>
        AIFunctionCatalog.RegisterAll(kernel);

    public void PrintTools(Kernel kernel) =>
        AIFunctionCatalog.PrintTools(kernel);
}

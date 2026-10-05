using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace AICodeFunction.Modules;

public sealed class CalculatorFunctions : AIFunctionModuleBase
{
    public override string PluginName => "Calculator";

    [KernelFunction("add")]
    [Description("Adds two numbers and returns the result.")]
    public double Add(
        [Description("First number")] double a,
        [Description("Second number")] double b) =>
        a + b;

    [KernelFunction("subtract")]
    [Description("Subtracts the second number from the first number.")]
    public double Subtract(
        [Description("First number")] double a,
        [Description("Second number")] double b) =>
        a - b;

    [KernelFunction("multiply")]
    [Description("Multiplies two numbers and returns the result.")]
    public double Multiply(
        [Description("First number")] double a,
        [Description("Second number")] double b) =>
        a * b;
}

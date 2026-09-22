using System.ComponentModel;
using System.Globalization;
using Microsoft.SemanticKernel;

namespace AICodeFunction.Modules;

public sealed class DateTimeFunctions : AIFunctionModuleBase
{
    public override string PluginName => "DateTime";

    [KernelFunction("get_utc_now")]
    [Description("Returns the current UTC date and time in ISO 8601 format.")]
    public string GetUtcNow() =>
        DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

    [KernelFunction("format_date")]
    [Description("Formats a date string using the specified .NET format pattern.")]
    public string FormatDate(
        [Description("Date/time value in ISO 8601 format")] string value,
        [Description(".NET format pattern, for example yyyy-MM-dd or dd MMM yyyy")] string format)
    {
        var date = DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return date.ToString(format, CultureInfo.InvariantCulture);
    }
}

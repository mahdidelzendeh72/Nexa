using Microsoft.Extensions.AI;
using Microsoft.JSInterop;

namespace Maf.Client.Chat.Tools;

public static class PageCaptureAgentTool
{
    public const string Name = "capture_page_screenshot";

    public const string SavedResult = "اسکرین‌شات صفحه ذخیره شد. اگر به تصویر نیاز داری از کاربر بخواه تا فایل را برایت بفرستد.";

    public static AITool Create(PageCaptureJsInterop capture, Action<byte[]> keepForNextSend) =>
        AIFunctionFactory.Create(
            (CancellationToken cancellationToken) => CaptureAsync(capture, keepForNextSend, cancellationToken),
            Name,
            "از زبانهٔ جاری مرورگر یک تصویر PNG می‌گیرد و آن را برای ارسال بعدی کاربر نگه می‌دارد. تصویر در نتیجهٔ ابزار نیست. فقط وقتی کاربر اسکرین‌شات یا ذخیرهٔ تصویر صفحه را بخواهد این ابزار را صدا بزن.");

    private static async Task<string> CaptureAsync(
        PageCaptureJsInterop capture,
        Action<byte[]> keepForNextSend,
        CancellationToken cancellationToken)
    {
        try
        {
            string image = await capture.CaptureScreenshotBrowserBase64Async(cancellationToken);
            if (image.Length > 0)
            {
                keepForNextSend(Convert.FromBase64String(image));
            }

            return SavedResult;
        }
        catch (JSException exception) when (IsDismissed(exception))
        {
            return "کاربر گرفتن تصویر را لغو کرد.";
        }
        catch (JSException)
        {
            return "گرفتن تصویر صفحه انجام نشد.";
        }
    }

    private static bool IsDismissed(JSException exception)
    {
        string message = exception.Message;
        return message.Contains("NotAllowedError", StringComparison.OrdinalIgnoreCase)
            || message.Contains("Permission denied", StringComparison.OrdinalIgnoreCase)
            || message.Contains("not allowed", StringComparison.OrdinalIgnoreCase);
    }
}

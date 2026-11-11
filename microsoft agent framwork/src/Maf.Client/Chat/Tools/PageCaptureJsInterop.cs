using Microsoft.JSInterop;

namespace Maf.Client.Chat.Tools;

/// <summary>
/// Calls <c>window.pageCapture</c> (wwwroot/assets/js/pageCapture.js).
/// </summary>
public sealed class PageCaptureJsInterop(IJSRuntime jsRuntime)
{
    public Task CaptureScreenshotBrowserAsync(CancellationToken cancellationToken = default) =>
        InvokeVoidAsync("pageCapture.captureScreenshotBrowser", cancellationToken);

    public async Task<string> CaptureScreenshotBrowserBase64Async(CancellationToken cancellationToken = default)
    {
        try
        {
            return await jsRuntime.InvokeAsync<string>(
                "pageCapture.captureScreenshotBrowserBase64",
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await jsRuntime.InvokeVoidAsync("console.error", $"pageCapture failed: {ex.Message}");
            throw;
        }
    }

    public Task CaptureScreenshotScreenAsync() =>
        InvokeVoidAsync("pageCapture.captureScreenshotScreen");

    public Task StartRecordingBrowserAsync() =>
        InvokeVoidAsync("pageCapture.startRecordingBrowser");

    public Task StartRecordingScreenAsync() =>
        InvokeVoidAsync("pageCapture.startRecordingScreen");

    public Task StopRecordingAsync() =>
        InvokeVoidAsync("pageCapture.stopRecording");

    public async Task<bool> IsRecordingAsync() =>
        await jsRuntime.InvokeAsync<bool>("pageCapture.isRecording");

    private async Task InvokeVoidAsync(string identifier, CancellationToken cancellationToken = default)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync(identifier, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await jsRuntime.InvokeVoidAsync("console.error", $"pageCapture failed: {ex.Message}");
            throw;
        }
    }
}

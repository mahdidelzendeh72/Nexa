namespace Maf.Client.Chat;

public sealed class ChatModelHandler(ChatSelection selection) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (selection.ProviderId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(selection.ModelName) &&
            request.RequestUri?.AbsolutePath.Contains("/ag-ui", StringComparison.OrdinalIgnoreCase) == true)
        {
            request.Headers.Remove("X-Chat-Provider");
            request.Headers.Remove("X-Chat-Model");
            request.Headers.TryAddWithoutValidation("X-Chat-Provider", selection.ProviderId.ToString("D"));
            request.Headers.TryAddWithoutValidation("X-Chat-Model", Uri.EscapeDataString(selection.ModelName));
            request.Headers.Remove("X-Chat-Tools");
            foreach (string key in selection.ToolKeys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    request.Headers.TryAddWithoutValidation("X-Chat-Tools", key);
                }
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}

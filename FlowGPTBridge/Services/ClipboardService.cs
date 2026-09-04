using System.Windows;

namespace FlowGPTBridge.Services;

/// <summary>
/// 在独立 STA 线程写入 Unicode 文本。不会模拟 Ctrl+V。
/// </summary>
public sealed class ClipboardService
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(80);

    public Task<bool> SetUnicodeTextAsync(string text, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var thread = new Thread(() =>
        {
            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                    return;
                }

                try
                {
                    Clipboard.SetText(text, TextDataFormat.UnicodeText);
                    completion.TrySetResult(true);
                    return;
                }
                catch (System.Runtime.InteropServices.ExternalException)
                {
                    if (attempt < MaxAttempts)
                    {
                        Thread.Sleep(RetryDelay);
                    }
                    else
                    {
                        completion.TrySetResult(false);
                        return;
                    }
                }
            }

            completion.TrySetResult(false);
        })
        {
            IsBackground = true,
            Name = "FlowGPTBridge.Clipboard"
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}

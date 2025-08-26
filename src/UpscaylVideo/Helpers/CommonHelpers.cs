using System;

namespace UpscaylVideo.Helpers;

public static class CommonHelpers
{
    public static void TryPostToMainThread(Action action)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(action);
        }
    }
}
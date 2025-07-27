using System;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace UpscaylVideo.Helpers;

public static class StorageExtensions
{
    public static async Task<Uri?> GetUriAsync(this Task<IStorageFolder?> folderTask)
    {
        var folder = await folderTask;
        if (folder is null)
            return null;

        return folder.Path;
    }
    
    public static async Task<Uri?> GetUriAsync(this Task<IStorageFile?> fileTask)
    {
        var file = await fileTask;
        if (file is null)
            return null;

        return file.Path;
    }

    public static async Task<IStorageFolder?> TryGetStorageFolderAsync(this Uri? uri, IStorageProvider provider)
    {
        if (uri is null)
            return null;
        return await provider.TryGetFolderFromPathAsync(uri);
    }
    
    public static string? ToUnescapedAbsolutePath(this Uri? uri)
    {
        if (uri is null)
            return null;
        return Uri.UnescapeDataString(uri.AbsolutePath);
    }
}
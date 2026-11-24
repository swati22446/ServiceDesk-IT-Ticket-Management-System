using Microsoft.Extensions.Options;

namespace ServiceDesk.Api.Services;

public class StorageSettings
{
    public string UploadPath { get; set; } = "Uploads";
    public int MaxFileSizeMb { get; set; } = 5;
}

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string extension);
    string GetFullPath(string storedName);
    void Delete(string storedName);
}

/// <summary>Stores uploads on local disk under random GUID names (never the user-supplied name).</summary>
public class LocalFileStorage : IFileStorage
{
    private readonly string _root;

    public LocalFileStorage(IOptions<StorageSettings> options, IHostEnvironment env)
    {
        var configured = options.Value.UploadPath;
        _root = Path.IsPathRooted(configured) ? configured : Path.Combine(env.ContentRootPath, configured);
        Directory.CreateDirectory(_root);
    }

    public async Task<string> SaveAsync(Stream content, string extension)
    {
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await using var target = File.Create(Path.Combine(_root, storedName));
        await content.CopyToAsync(target);
        return storedName;
    }

    // Path.GetFileName strips any directory part, preventing path traversal.
    public string GetFullPath(string storedName) => Path.Combine(_root, Path.GetFileName(storedName));

    public void Delete(string storedName)
    {
        var path = GetFullPath(storedName);
        if (File.Exists(path)) File.Delete(path);
    }
}

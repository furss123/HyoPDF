using HyoPDF.Core.Models;

namespace HyoPDF.Core.Services;

public interface IRecentFilesService
{
    int MaxCount { get; }
    Task EnsureLoadedAsync();
    void Add(string path);
    IReadOnlyList<RecentFile> GetAll();
    void Remove(string path);
    void Clear();
}

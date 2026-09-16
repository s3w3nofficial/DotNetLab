using DotNetLab.Features.Compilation;

namespace DotNetLab.Infrastructure.Caching;

internal interface ICompilationCacheStore
{
    ValueTask<CachedCompilation?> GetAsync(string key, CancellationToken cancellationToken);

    ValueTask StoreAsync(string key, CachedCompilation value, CancellationToken cancellationToken);
}

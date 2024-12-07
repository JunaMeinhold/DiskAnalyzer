namespace DiskAnalyzer
{
    using System.Collections.Frozen;
    using System.Collections.Generic;

    public class FileTreeLookupCache
    {
        private readonly FileTree fileTree;
        private readonly Dictionary<string, FileTreeNode?> cache = new();

        private FrozenDictionary<string, FileTreeNode?> frozenCache;
        private bool frozen = false;

#nullable disable

        public FileTreeLookupCache(FileTree fileTree)
        {
            this.fileTree = fileTree;
        }

# nullable restore

        public void Warmup(bool freeze = false)
        {
            var files = fileTree.FindFiles();
            foreach (var file in files)
            {
                if (file.FullPath == null)
                {
                    continue;
                }
                cache.TryAdd(file.FullPath, file);
            }

            if (freeze)
            {
                frozenCache = cache.ToFrozenDictionary();
                frozen = true;
            }
        }

        public FileTreeNode? Find(string path)
        {
            if (frozen && frozenCache.TryGetValue(path, out FileTreeNode? node))
            {
                return node;
            }

            if (frozen)
            {
                throw new KeyNotFoundException();
            }

            return FindInternal(path);
        }

        public FileTreeNode? FindInternal(string path)
        {
            if (cache.TryGetValue(path, out var node))
            {
                return node;
            }

            node = fileTree.Find(path);
            cache.Add(path, node);
            return node;
        }
    }
}
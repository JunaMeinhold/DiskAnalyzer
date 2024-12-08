namespace DiskAnalyzer
{
    using Hexa.NET.Utilities.IO;
    using System;
    using System.Collections.Generic;

    public class FileTree
    {
        private readonly FileTreeNode root;
        internal long files;
        internal long folders;

        public FileTree(FileTreeNode root)
        {
            this.root = root;
        }

        public FileTree(string folder)
        {
            folder = Path.GetFullPath(folder);
            root = new(this, folder, null, FileUtils.GetFileMetadata(folder));
        }

        public FileTreeNode Root => root;

        private static FileTreeNode AddChild(FileTreeNode parent, string name, FileMetadata metadata, bool automaticSizeCalc)
        {
            FileTreeNode node = new(parent.Tree, name, parent, metadata);
            parent.AddChild(node, automaticSizeCalc);
            return node;
        }

        private static FileTreeNode AddOrGetNode(FileTreeNode parent, ReadOnlySpan<char> name, FileMetadata metadata, bool automaticSizeCalc)
        {
            lock (parent._lock)
            {
                var node = parent.GetChild(name);
                if (node == null)
                {
                    return AddChild(parent, name.ToString(), metadata, automaticSizeCalc);
                }
                return node;
            }
        }

        public void AddFileCount(int files)
        {
            Interlocked.Add(ref this.files, files);
        }

        public void AddFolderCount(int folders)
        {
            Interlocked.Add(ref this.folders, folders);
        }

        private static FileTreeNode? GetNode(FileTreeNode parent, ReadOnlySpan<char> name)
        {
            return parent.GetChild(name);
        }

        public static FileTreeNode AddFolder(FileTreeNode parent, string path, FileMetadata metadata)
        {
            var name = Path.GetFileName(path);
            return AddOrGetNode(parent, name, metadata, false);
        }

        public static FileTreeNode AddFileToFolder(FileTreeNode folder, string path, FileMetadata metadata, bool automaticSizeCalc = true)
        {
            var fileName = Path.GetFileName(path);
            var length = (metadata.Attributes & FileAttributes.ReparsePoint) != 0 ? 0 : metadata.Size;
            var node = AddOrGetNode(folder, fileName, metadata, automaticSizeCalc);
            node.FullPath = path;
            return node;
        }

        public bool Remove(string path)
        {
            return Remove(Find(path));
        }

        public static bool Remove(FileTreeNode? node)
        {
            if (node == null || node.Parent == null)
            {
                return false;
            }

            lock (node.Parent)
            {
                node.Parent.RemoveChild(node);
            }

            node.Children.Clear();
            node.Parent = null;

            return true;
        }

        public FileTreeNode? Find(string path)
        {
            var relative = Path.GetRelativePath(root.Name, path);
            FileTreeNode? node = root;
            foreach (var span in GetPathParts(relative))
            {
                if (node == null)
                {
                    return null;
                }
                var part = span.ToSpan(relative);
                node = GetNode(node, part);
            }
            return node;
        }

        public void ComputeLocal()
        {
            ComputeLocal(root);
        }

        private static void ComputeLocal(FileTreeNode parent)
        {
            Parallel.ForEach(parent.Children, node =>
            {
                if (!node.IsFile)
                {
                    ComputeLocal(node);
                }

#pragma warning disable CS8602 // Can't be null or the parameter is null too.
                node.PercentUsage = node.Size / (float)node.Parent.Size * 100f;
#pragma warning restore CS8602 // Can't be null or the parameter is null too.
            });
        }

        public void ComputeGlobal()
        {
            ComputeGlobal(root, root.Size);
        }

        private static void ComputeGlobal(FileTreeNode parent, long size)
        {
            Parallel.ForEach(parent.Children, node =>
            {
                if (!node.IsFile)
                {
                    ComputeGlobal(node, size);
                }

                node.PercentUsageGlobal = node.Size / (float)size * 100f;
            });
        }

        public void Clear()
        {
            root.Children.Clear();
            root.Metadata.Size = 0;
        }

        public List<FileTreeNode> FindFiles()
        {
            List<FileTreeNode> edges = [];
            Stack<FileTreeNode> walkStack = new();
            walkStack.Push(root);
            while (walkStack.TryPop(out var node))
            {
                lock (node._lock)
                {
                    for (var i = 0; i < node.Children.Count; i++)
                    {
                        var child = node.Children[i];
                        walkStack.Push(child);
                    }

                    if (node.IsFile)
                    {
                        edges.Add(node);
                    }
                }
            }

            return edges;
        }

        private static IEnumerable<TextSpan> GetPathParts(string path)
        {
            int start = 0;
            int index = path.IndexOf(Path.DirectorySeparatorChar);

            while (index != -1)
            {
                yield return new TextSpan(start, index - start);
                start = index + 1;
                index = path.AsSpan(start).IndexOf(Path.DirectorySeparatorChar);
                if (index != -1)
                {
                    index += start;
                }
            }

            if (start < path.Length)
            {
                yield return new TextSpan(start, path);
            }
        }
    }
}
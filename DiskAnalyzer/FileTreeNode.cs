namespace DiskAnalyzer
{
    using Hexa.NET.ImGui.Widgets;
    using Hexa.NET.Utilities.IO;
    using System.Collections.Generic;
    using System.Text;

    public class FileTreeNode
    {
        public readonly FileTree Tree;
        public string Name;
        public int HashCode;
        public char Icon;
        public string? FullPath;
        public FileTreeNode? Parent;
        public List<FileTreeNode> Children;

        public volatile bool IsHardLink;
        public FileMetadata Metadata;

        public float PercentUsage;
        public float PercentUsageGlobal;
        public readonly Lock _lock = new();

        public FileTreeNode(FileTree tree, string name, FileTreeNode? parent, FileMetadata metadata)
        {
            Tree = tree;
            Name = name;
            var isFile = (metadata.Attributes & FileAttributes.Directory) == 0;
            Icon = isFile ? GetIcon(name) : MaterialIcons.Folder;
            HashCode = name.GetHashCode();
            Parent = parent;
            Children = [];
            Metadata = metadata;
        }

        public bool IsFile => (Metadata.Attributes & FileAttributes.Directory) == 0;

        public bool IsDirectory => (Metadata.Attributes & FileAttributes.Directory) != 0;

        public DateTime LastModified => Metadata.LastWriteTime;

        public long Size => Metadata.Size;

        private static char GetIcon(ReadOnlySpan<char> name)
        {
            var extension = Path.GetExtension(name);
            return extension switch
            {
                ".zip" => MaterialIcons.FolderZip,
                ".dds" or ".png" or ".jpg" or ".ico" => MaterialIcons.Image,
                _ => MaterialIcons.Draft,
            };
        }

        public void Lock()
        {
            _lock.Enter();
        }

        public void ReleaseLock()
        {
            _lock.Exit();
        }

        public void SetSize(long size)
        {
            long old = Size;
            Metadata.Size = size;
            if (IsFile)
            {
                if (old != 0)
                {
                    Parent?.RemoveSizeTraverse(old);
                }

                Parent?.AddSizeTraverse(size);
            }
        }

        public FileTreeNode? GetChild(string name)
        {
            int hc = string.GetHashCode(name);
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child.HashCode == hc)
                {
                    return child;
                }
            }

            return null;
        }

        public FileTreeNode? GetChild(ReadOnlySpan<char> name)
        {
            int hc = string.GetHashCode(name);

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (child.HashCode == hc)
                {
                    return child;
                }
            }

            return null;
        }

        internal void AddChild(FileTreeNode child, bool automaticSizeCalc)
        {
            Children.Add(child);

            if (automaticSizeCalc && child.IsFile)
            {
                AddSizeTraverse(child.Size);
            }
        }

        internal void RemoveChild(FileTreeNode child)
        {
            bool result;

            result = Children.Remove(child);

            if (result && child.IsFile)
            {
                RemoveSizeTraverse(child.Size);
            }
        }

        public void AddSizeTraverse(long size)
        {
            Interlocked.Add(ref Metadata.Size, size);
            var node = Parent;
            while (node != null)
            {
                Interlocked.Add(ref node.Metadata.Size, size);
                node.UpdateChildren();

                node = node.Parent;
            }
        }

        public void RemoveSizeTraverse(long size)
        {
            Interlocked.Add(ref Metadata.Size, -size);
            var node = Parent;
            while (node != null)
            {
                Interlocked.Add(ref node.Metadata.Size, -size);
                node.UpdateChildren();

                node = node.Parent;
            }
        }

        private void UpdateChildren()
        {
            if (IsFile)
            {
                return;
            }

            lock (_lock)
            {
                for (int i = 0; i < Children.Count; i++)
                {
                    Children[i].PercentUsage = Children[i].Size / (float)Size * 100f;
                }
            }
        }

        public string GetFullPath()
        {
            StringBuilder sb = new();
            var node = this;

            while (node != null)
            {
                sb.Insert(0, Path.DirectorySeparatorChar);
                sb.Insert(0, node.Name);
                node = node.Parent;
            }

            return sb.ToString();
        }

        public override string ToString()
        {
            return Name;
        }

        public void SortAZ()
        {
            Sort(this, AZComparer.Asc);
        }

        public void SortZA()
        {
            Sort(this, AZComparer.Desc);
        }

        public void SortDescPercent()
        {
            Sort(this, PercentageComparer.Desc);
        }

        public void SortAscPercent()
        {
            Sort(this, PercentageComparer.Asc);
        }

        private static void Sort(FileTreeNode root, IComparer<FileTreeNode> comparer)
        {
            Stack<FileTreeNode> stack = new();
            stack.Push(root);

            while (stack.TryPop(out var node))
            {
                lock (node._lock)
                {
                    node.Children.Sort(comparer);
                    foreach (FileTreeNode child in node.Children)
                    {
                        if (child.IsDirectory)
                        {
                            stack.Push(child);
                        }
                    }
                }
            }
        }
    }
}
namespace DiskAnalyzer
{
    using System;
    using System.Collections.Generic;

    public class BaseComparer : IComparer<FileTreeNode>
    {
        public static readonly BaseComparer Instance = new();

        public int Compare(FileTreeNode? x, FileTreeNode? y)
        {
            if (x == null || y == null)
            {
                throw new ArgumentException("Cannot compare null FileTreeNode objects.");
            }

            if (x.IsFile == y.IsFile)
            {
                return 0;
            }

            if (x.IsFile)
            {
                return 1;
            }

            return -1;
        }
    }

    public class AZComparer : IComparer<FileTreeNode>
    {
        public static readonly AZComparer Asc = new(true);
        public static readonly AZComparer Desc = new(false);

        private readonly int order;

        public AZComparer(bool ascending)
        {
            order = ascending ? 1 : -1;
        }

        public int Compare(FileTreeNode? x, FileTreeNode? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            int compare = BaseComparer.Instance.Compare(x, y);
            if (compare != 0)
            {
                return order * compare;
            }

            return order * string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }
    }
}
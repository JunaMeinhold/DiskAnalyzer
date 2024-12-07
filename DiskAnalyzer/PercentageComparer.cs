namespace DiskAnalyzer
{
    using System;
    using System.Collections.Generic;

    public class PercentageComparer : IComparer<FileTreeNode>
    {
        public static readonly PercentageComparer Asc = new(true);
        public static readonly PercentageComparer Desc = new(false);

        private readonly int order;

        public PercentageComparer(bool ascending)
        {
            order = ascending ? 1 : -1;
        }

        public int Compare(FileTreeNode? x, FileTreeNode? y)
        {
            if (x == null || y == null)
            {
                throw new ArgumentException("Cannot compare null percentages.");
            }

            int compare = BaseComparer.Instance.Compare(x, y);
            if (compare != 0)
            {
                return order * compare;
            }

            compare = x.PercentUsage.CompareTo(y.PercentUsage);
            if (compare != 0)
            {
                return order * compare;
            }

            return order * string.Compare(x.Name, y.Name, StringComparison.Ordinal);
        }
    }
}
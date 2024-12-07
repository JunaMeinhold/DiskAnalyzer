namespace DiskAnalyzer
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using System.Text;

    public static partial class HardLinkHelper
    {
        #region WinAPI P/Invoke declarations

        [LibraryImport("kernel32.dll", SetLastError = true)]
        private static unsafe partial nint FindFirstFileNameW(char* lpFileName, uint dwFlags, uint* StringLength, char* LinkName);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool FindNextFileNameW(nint hFindStream, uint* StringLength, char* LinkName);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool FindClose(nint hFindFile);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern unsafe bool GetVolumePathName(char* lpszFileName, char* lpszVolumePathName, uint cchBufferLength);

        private static readonly nint INVALID_HANDLE_VALUE = -1; // 0xffffffff;
        private const int MAX_PATH = 65535; // Max. NTFS path length.

        #endregion WinAPI P/Invoke declarations

        /// <summary>
        /// Checks for hard links on a Windows NTFS drive associated with the given path.
        /// </summary>
        /// <param name="filepath">Fully qualified path of the file to check for shared hard links</param>
        /// <returns>
        ///     Empty list is returned for non-existing path or unsupported path.
        ///     Single hard link paths returns empty list if ReturnEmptyListIfOnlyOne is true. If false, returns single item list.
        ///     For multiple shared hard links, returns list of all the shared hard links.
        /// </returns>
        public static unsafe List<string> GetHardLinks(string filepath)
        {
            List<string> links = [];

            try
            {
                fixed (char* fp = filepath)
                {
                    char* sbPath = (char*)Marshal.AllocHGlobal((MAX_PATH + 1) * sizeof(char));
                    ZeroMemoryT(sbPath, MAX_PATH + 1);

                    uint charCount = MAX_PATH;

                    GetVolumePathName(fp, sbPath, MAX_PATH); // Must use GetVolumePathName, because Path.GetPathRoot fails on a mounted drive on an empty folder.

                    string volume = new(sbPath);
                    volume = volume[..^1];

                    ZeroMemoryT(sbPath, MAX_PATH); // Reset the array because these API's can leave garbage at the end of the buffer.

                    nint findHandle;
                    if (INVALID_HANDLE_VALUE == (findHandle = FindFirstFileNameW(fp, 0, &charCount, sbPath)))
                    {
                        Marshal.FreeHGlobal((nint)sbPath);
                        return links;
                    }

                    do
                    {
                        links.Add(volume + new string(sbPath)); // Add the full path to the result list.

                        charCount = MAX_PATH;
                        ZeroMemoryT(sbPath, MAX_PATH);
                    }
                    while
                    (FindNextFileNameW(findHandle, &charCount, sbPath));

                    FindClose(findHandle);
                    Marshal.FreeHGlobal((nint)sbPath);
                }
            }
            catch (Exception)
            {
                //Logger.Instance.Info($"GetHardLinks: Exception, file: {filepath}, reason: {ex.Message}, stacktrace {ex.StackTrace}");
            }

            return links;
        }

        /// <summary>
        /// Checks for hard links on a Windows NTFS drive associated with the given path.
        /// </summary>
        /// <param name="filepath">Fully qualified path of the file to check for shared hard links</param>
        /// <returns>
        ///     Empty list is returned for non-existing path or unsupported path.
        ///     Single hard link paths returns empty list if ReturnEmptyListIfOnlyOne is true. If false, returns single item list.
        ///     For multiple shared hard links, returns list of all the shared hard links.
        /// </returns>
        public static unsafe List<string> GetHardLinks(string filepath, string volume)
        {
            List<string> links = [];

            try
            {
                fixed (char* fp = filepath)
                {
                    char* sbPath = (char*)Marshal.AllocHGlobal((MAX_PATH + 1) * sizeof(char));
                    ZeroMemoryT(sbPath, MAX_PATH + 1);

                    uint charCount = MAX_PATH;

                    nint findHandle;
                    if (INVALID_HANDLE_VALUE == (findHandle = FindFirstFileNameW(fp, 0, &charCount, sbPath)))
                    {
                        Marshal.FreeHGlobal((nint)sbPath);
                        return links;
                    }

                    do
                    {
                        links.Add(volume + new string(sbPath)); // Add the full path to the result list.

                        charCount = MAX_PATH;
                        ZeroMemoryT(sbPath, MAX_PATH);
                    }
                    while
                    (FindNextFileNameW(findHandle, &charCount, sbPath));

                    FindClose(findHandle);
                    Marshal.FreeHGlobal((nint)sbPath);
                }
            }
            catch (Exception)
            {
                //Logger.Instance.Info($"GetHardLinks: Exception, file: {filepath}, reason: {ex.Message}, stacktrace {ex.StackTrace}");
            }

            return links;
        }

        public static unsafe void GetHardLinks(string filepath, List<string> links)
        {
            try
            {
                fixed (char* fp = filepath)
                {
                    char* sbPath = (char*)Marshal.AllocHGlobal((MAX_PATH + 1) * sizeof(char));
                    ZeroMemoryT(sbPath, MAX_PATH + 1);

                    uint charCount = MAX_PATH;

                    GetVolumePathName(fp, sbPath, MAX_PATH); // Must use GetVolumePathName, because Path.GetPathRoot fails on a mounted drive on an empty folder.

                    string volume = new(sbPath);
                    volume = volume[..^1];

                    ZeroMemoryT(sbPath, MAX_PATH); // Reset the array because these API's can leave garbage at the end of the buffer.

                    nint findHandle;
                    if (INVALID_HANDLE_VALUE == (findHandle = FindFirstFileNameW(fp, 0, &charCount, sbPath)))
                    {
                        Marshal.FreeHGlobal((nint)sbPath);
                        return;
                    }

                    do
                    {
                        links.Add(volume + new string(sbPath)); // Add the full path to the result list.

                        charCount = MAX_PATH;
                        ZeroMemoryT(sbPath, MAX_PATH);
                    }
                    while
                    (FindNextFileNameW(findHandle, &charCount, sbPath));

                    FindClose(findHandle);
                    Marshal.FreeHGlobal((nint)sbPath);
                }
            }
            catch (Exception)
            {
                //Logger.Instance.Info($"GetHardLinks: Exception, file: {filepath}, reason: {ex.Message}, stacktrace {ex.StackTrace}");
            }
        }

        public static unsafe void GetHardLinks(string filepath, string volume, List<string> links)
        {
            try
            {
                fixed (char* fp = filepath)
                {
                    char* sbPath = (char*)Marshal.AllocHGlobal((MAX_PATH + 1) * sizeof(char));
                    ZeroMemoryT(sbPath, MAX_PATH + 1);

                    uint charCount = MAX_PATH;

                    nint findHandle;
                    if (INVALID_HANDLE_VALUE == (findHandle = FindFirstFileNameW(fp, 0, &charCount, sbPath)))
                    {
                        Marshal.FreeHGlobal((nint)sbPath);
                        return;
                    }

                    do
                    {
                        links.Add(volume + new string(sbPath));

                        charCount = MAX_PATH;
                        ZeroMemoryT(sbPath, MAX_PATH);
                    }
                    while
                    (FindNextFileNameW(findHandle, &charCount, sbPath));

                    FindClose(findHandle);
                    Marshal.FreeHGlobal((nint)sbPath);
                }
            }
            catch (Exception)
            {
            }
        }

        public static unsafe void GetHardLinks(string filepath, string volume, StringBuilder stringBuilder, List<string> links)
        {
            try
            {
                fixed (char* fp = filepath)
                {
                    char* sbPath = (char*)Marshal.AllocHGlobal((MAX_PATH + 1) * sizeof(char));
                    ZeroMemoryT(sbPath, MAX_PATH + 1);

                    uint charCount = MAX_PATH;

                    nint findHandle;
                    if (INVALID_HANDLE_VALUE == (findHandle = FindFirstFileNameW(fp, 0, &charCount, sbPath)))
                    {
                        Marshal.FreeHGlobal((nint)sbPath);
                        return;
                    }

                    do
                    {
                        stringBuilder.Clear();
                        stringBuilder.Append(volume);
                        stringBuilder.Append(sbPath, (int)charCount - 1);
                        links.Add(stringBuilder.ToString());

                        charCount = MAX_PATH;
                        ZeroMemoryT(sbPath, MAX_PATH);
                    }
                    while
                    (FindNextFileNameW(findHandle, &charCount, sbPath));

                    FindClose(findHandle);
                    Marshal.FreeHGlobal((nint)sbPath);
                }
            }
            catch (Exception)
            {
            }
        }

        private static unsafe void GetHardLinksFolder(List<string> files, HashSet<string> links)
        {
            ConcurrentQueue<string> queue = new(files);

            void WorkerVoid()
            {
                while (queue.TryDequeue(out var file))
                {
                    lock (links)
                    {
                        if (links.Contains(file))
                            continue;
                    }

                    var foundLinks = GetHardLinks(file);
                    if (foundLinks.Count > 1)
                    {
                        foundLinks.Remove(file);
                        lock (links)
                        {
                            for (int i = 0; i < foundLinks.Count; i++)
                            {
                                links.Add(foundLinks[i]);
                            }
                        }
                    }
                }
            }

            Task[] threads = new Task[Environment.ProcessorCount];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new(WorkerVoid);
                threads[i].Start();
            }

            Task.WaitAll(threads);
        }

        public static void UpdateTree(FileTree tree, List<string> files, CancellationToken cancellationToken)
        {
#nullable disable
            string volume = Path.GetPathRoot(tree.Root.Name)[..^1];
#nullable restore
            FileTreeLookupCache cache = new(tree);
            cache.Warmup(freeze: true);

            void WorkerVoid(object? param)
            {
                if (param is not (int index, int threadCount, List<string> files, CancellationToken cancellationToken))
                {
                    return;
                }

                int start = index * files.Count / threadCount;
                int batchSize = files.Count / threadCount;
                int end = (index == threadCount - 1) ? files.Count : start + batchSize;

                List<string> links = new();
                List<FileTreeNode> nodes = new();
                StringBuilder sb = new(volume.Length + MAX_PATH);

                for (int iFile = start; iFile < end; iFile++)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    string file = files[iFile];
                    GetHardLinks(file, volume, sb, links);
                    if (links.Count > 1)
                    {
                        bool alreadyMarked = false;
                        for (int i = 0; i < links.Count; i++)
                        {
                            var node = cache.Find(links[i]);
                            if (node == null)
                            {
                                continue;
                            }

                            node.Lock();
                            nodes.Add(node);
                            if (node.IsHardLink)
                            {
                                alreadyMarked = true;
                                break;
                            }
                        }

                        if (!alreadyMarked)
                        {
                            for (int i = 0; i < nodes.Count; i++)
                            {
                                var node = nodes[i];
                                node.IsHardLink = true;
                                if (node.FullPath == file)
                                {
                                    continue;
                                }
                                node.SetSize(0);
                            }
                        }

                        for (int i = 0; i < nodes.Count; i++)
                        {
                            nodes[i].ReleaseLock();
                        }
                        nodes.Clear();
                    }
                    links.Clear();
                }
            }

            Thread[] threads = new Thread[Environment.ProcessorCount];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new(WorkerVoid);
                threads[i].Start((i, threads.Length, files, cancellationToken));
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }
        }
    }

    /// <summary>
    /// A thread-safe object pool for <see cref="List{T}"/> instances.
    /// </summary>
    /// <typeparam name="T">The type of elements in the lists.</typeparam>
    public class ListPool<T>
    {
        private readonly ConcurrentStack<List<T>> pool = new();

        /// <summary>
        /// Gets a shared instance of the <see cref="ListPool{T}"/> for convenient use.
        /// </summary>
        public static ListPool<T> Shared { get; } = new();

        /// <summary>
        /// Rents a <see cref="List{T}"/> instance from the pool. If the pool is empty, a new instance is created.
        /// </summary>
        /// <returns>A <see cref="List{T}"/> instance from the pool or a new instance if the pool is empty.</returns>
        public List<T> Rent()
        {
            if (pool.IsEmpty)
            {
                return new();
            }
            else
            {
                if (pool.TryPop(out var list))
                {
                    return list;
                }
                return [];
            }
        }

        /// <summary>
        /// Returns a rented <see cref="List{T}"/> instance to the pool after clearing its contents.
        /// </summary>
        /// <param name="list">The <see cref="List{T}"/> instance to return to the pool.</param>
        public void Return(List<T> list)
        {
            list.Clear();
            if (pool.Count < 1024)
            {
                pool.Push(list);
            }
        }

        /// <summary>
        /// Clears the pool, removing all <see cref="List{T}"/> instances from it.
        /// </summary>
        public void Clear()
        {
            pool.Clear();
        }
    }
}
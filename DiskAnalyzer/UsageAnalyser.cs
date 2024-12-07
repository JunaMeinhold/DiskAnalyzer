namespace DiskAnalyzer
{
    using Hexa.NET.ImGui.Widgets.IO;
    using Hexa.NET.KittyUI.Debugging;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    public static class UsageAnalyser
    {
        public static Task AnalyzeAsync(FileTree tree, bool ignoreHardLinks, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                string folder = tree.Root.Name;

                List<string> files = [];
                IndexFilesAsync(tree, folder, files, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                tree.Root.SortAZ();
                files.Sort();

                if (ignoreHardLinks)
                {
                    HardLinkHelper.UpdateTree(tree, files, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                tree.ComputeLocal();
                tree.ComputeGlobal();
            }, cancellationToken);
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern FileAttributes GetFileAttributes(string lpFileName);

        private static void IndexFilesAsync(FileTree tree, string folder, List<string> files, CancellationToken cancellationToken)
        {
            ConcurrentQueue<(string folder, FileMetadata meta, FileTreeNode parent)> folderQueue = new();

            foreach (var subFolder in Directory.EnumerateDirectories(folder))
            {
                folderQueue.Enqueue((subFolder, FileUtils.GetFileMetadata(subFolder), tree.Root));
            }

            long start = Stopwatch.GetTimestamp();
            ConcurrentBag<string> filesBag = new();
            Barrier barrier = new(Environment.ProcessorCount);

            Thread[] threads = new Thread[Environment.ProcessorCount];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new(WorkerVoid);
                threads[i].Start((folderQueue, filesBag, tree, barrier, cancellationToken));
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            long end = Stopwatch.GetTimestamp();

            TimeSpan duartion = TimeSpan.FromSeconds((end - start) / (double)Stopwatch.Frequency);

            ImGuiDebugTools.WriteLine(duartion);

            files.AddRange(filesBag);
        }

        private static void WorkerVoid(object? param)
        {
            if (param is not (ConcurrentQueue<(string folder, FileMetadata meta, FileTreeNode parent)> folderQueue, ConcurrentBag<string> files, FileTree tree, Barrier barrier, CancellationToken cancellationToken))
            {
                return;
            }

            Queue<(string folder, FileMetadata meta, FileTreeNode parent)> localQueue = new();
            const int localThreshold = 16;
            const int starvationThreshold = 16;

            int foldersCount = 0;
            int filesCount = 0;

            while (true)
            {
                while (localQueue.TryDequeue(out (string folder, FileMetadata meta, FileTreeNode parent) item) || folderQueue.TryDequeue(out item))
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var folderNode = FileTree.AddFolder(item.parent, item.folder, item.meta);

                    var flags = item.meta.Attributes;

                    // skip sym-links
                    if ((flags & FileAttributes.ReparsePoint) != 0) continue;

                    try
                    {
                        long folderSize = 0;
                        foreach (var metadata in FileUtils.EnumerateEntries(item.folder, "", SearchOption.TopDirectoryOnly))
                        {
                            if (cancellationToken.IsCancellationRequested)
                            {
                                return;
                            }

                            var path = metadata.Path.ToString();
                            var attributes = metadata.Attributes;
                            if ((attributes & FileAttributes.Directory) != 0)
                            {
                                if (localQueue.Count > localThreshold && folderQueue.Count <= starvationThreshold)
                                {
                                    localQueue.Enqueue((path, metadata, folderNode));
                                }
                                else
                                {
                                    folderQueue.Enqueue((path, metadata, folderNode));
                                }
                                foldersCount++;
                            }
                            else
                            {
                                files.Add(path);
                                var fileNode = FileTree.AddFileToFolder(folderNode, path, metadata, automaticSizeCalc: false);
                                filesCount++;
                                folderSize += fileNode.Size;
                            }
                        }

                        folderNode.AddSizeTraverse(folderSize);

                        if (filesCount > 1024)
                        {
                            tree.AddFileCount(filesCount);
                            filesCount = 0;
                        }

                        if (foldersCount > 1024)
                        {
                            tree.AddFolderCount(foldersCount);
                            foldersCount = 0;
                        }
                    }
                    catch
                    {
                    }
                }

                try
                {
                    if (barrier.SignalAndWait(50, cancellationToken))
                    {
                        tree.AddFileCount(filesCount);
                        filesCount = 0;

                        tree.AddFolderCount(foldersCount);
                        foldersCount = 0;

                        return; // All threads reached the barrier; terminate.
                    }
                }
                catch (OperationCanceledException)
                {
                    return; // Graceful cancellation.
                }
            }
        }
    }
}
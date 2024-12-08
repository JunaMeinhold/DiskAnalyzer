namespace DiskAnalyzer
{
    using Hexa.NET.KittyUI.Debugging;
    using Hexa.NET.Utilities.IO;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    public static class UsageAnalyser
    {
        public static Task AnalyzeAsync(FileTree tree, bool ignoreHardLinks, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                string folder = tree.Root.Name;

                List<string>? files = ignoreHardLinks ? [] : null;
                IndexFilesAsync(tree, folder, files, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                tree.Root.SortAZ();

                if (ignoreHardLinks)
                {
                    files!.Sort();
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

        private static void IndexFilesAsync(FileTree tree, string folder, List<string>? files, CancellationToken cancellationToken)
        {
            ConcurrentQueue<(string folder, FileMetadata meta, FileTreeNode parent)> folderQueue = new();

            foreach (var subFolder in Directory.EnumerateDirectories(folder))
            {
                folderQueue.Enqueue((subFolder, FileUtils.GetFileMetadata(subFolder), tree.Root));
            }

            long start = Stopwatch.GetTimestamp();
            ConcurrentBag<string>? filesBag = files != null ? new() : null;
            Barrier barrier = new(Environment.ProcessorCount);

            Thread[] threads = new Thread[Environment.ProcessorCount];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new(WorkerVoid);
                threads[i].Start(new TaskParams(folderQueue, filesBag, tree, barrier, cancellationToken));
            }

            for (int i = 0; i < threads.Length; i++)
            {
                threads[i].Join();
            }

            long end = Stopwatch.GetTimestamp();

            TimeSpan duartion = TimeSpan.FromSeconds((end - start) / (double)Stopwatch.Frequency);

            ImGuiDebugTools.WriteLine(duartion);

            files?.AddRange(filesBag!);
        }

        private struct TaskParams
        {
            public ConcurrentQueue<(string folder, FileMetadata meta, FileTreeNode parent)> folderQueue;
            public ConcurrentBag<string>? files;
            public FileTree tree;
            public Barrier barrier;
            public CancellationToken cancellationToken;

            public TaskParams(ConcurrentQueue<(string folder, FileMetadata meta, FileTreeNode parent)> folderQueue, ConcurrentBag<string>? files, FileTree tree, Barrier barrier, CancellationToken cancellationToken)
            {
                this.folderQueue = folderQueue;
                this.files = files;
                this.tree = tree;
                this.barrier = barrier;
                this.cancellationToken = cancellationToken;
            }

            public readonly void Deconstruct(out ConcurrentQueue<(string folder, FileMetadata meta, FileTreeNode parent)> folderQueue, out ConcurrentBag<string>? files, out FileTree tree, out Barrier barrier, out CancellationToken cancellationToken)
            {
                folderQueue = this.folderQueue;
                files = this.files;
                tree = this.tree;
                barrier = this.barrier;
                cancellationToken = this.cancellationToken;
            }
        }

        private static void WorkerVoid(object? param)
        {
            if (param is not TaskParams taskParams)
            {
                return;
            }

            (ConcurrentQueue<(string folder, FileMetadata meta, FileTreeNode parent)> folderQueue, ConcurrentBag<string>? files, FileTree tree, Barrier barrier, CancellationToken cancellationToken) = taskParams;

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
                        foreach (var metadata in FileUtils.EnumerateEntries(item.folder, "", SearchOption.TopDirectoryOnly)) // Uses native calls instead of c# ones which create file handles and gc pressure.
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
                                files?.Add(path);
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
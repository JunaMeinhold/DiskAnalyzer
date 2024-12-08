namespace DiskAnalyzer
{
    using Hexa.NET.ImGui;
    using Hexa.NET.ImGui.Widgets;
    using Hexa.NET.ImGui.Widgets.Dialogs;
    using Hexa.NET.ImPlot;
    using Hexa.NET.Utilities.Text;
    using System.Diagnostics;
    using System.Numerics;
    using System.Threading.Tasks;

    public class MainWindow : ImWindow
    {
        private FileTree? tree;
        private FileTreeNode? root;
        private FileTreeNode? _selected;
        private string folder = string.Empty;
        private Task? task;
        private CancellationTokenSource? cancellationTokenSource;
        private bool ignoreHardlinks;

        private long analysisStart;
        private long analysisEnd;
        private bool sortDirty = false;

        public MainWindow()
        {
            IsShown = true;
            IsEmbedded = true;
            Flags = ImGuiWindowFlags.MenuBar;
        }

        protected override string Name { get; } = "Disk Usage";

        private void DrawMenuBar()
        {
            if (ImGui.BeginMenuBar())
            {
                if (ImGui.BeginMenu("Options"u8))
                {
                    ImGui.Checkbox("Ignore Hardlinks"u8, ref ignoreHardlinks);
                    ImGui.EndMenu();
                }

                ImGui.EndMenuBar();
            }
        }

        public override void Dispose()
        {
            cancellationTokenSource?.Dispose();
        }

        private float widthLeftPanel = 600;

        public FileTreeNode? Selection
        {
            get => _selected;
            set
            {
                if (_selected == value) return;
                pieChartCache.Release();
                _selected = value;
            }
        }

        public override unsafe void DrawContent()
        {
            DrawMenuBar();
            ImGui.BeginDisabled(TaskManager.IsAnyRunning);
            ImGui.InputText("##Path"u8, ref folder, 1024);
            ImGui.SameLine();
            if ((task == null || task.IsCompleted) && ImGui.Button("..."u8))
            {
                OpenFileDialog dialog = new()
                {
                    OnlyAllowFolders = true
                };
                dialog.Show((sender, result) =>
                {
                    if (result != DialogResult.Ok || sender is not OpenFileDialog dialog) return;
                    folder = dialog.SelectedFile!;
                });
            }
            ImGui.SameLine();
            if ((task == null || task.IsCompleted) && ImGui.Button("Analyze"u8))
            {
                RunAnalysis();
            }

            ImGui.EndDisabled();

            if (task != null && !task.IsCompleted && ImGui.Button("Cancel"u8))
            {
                cancellationTokenSource?.Cancel();
            }

            if (TaskManager.IsAnyRunning)
            {
                ImGui.SameLine();
                ImGuiSpinner.Spinner(6, 3, 0xffefcd32);
            }

            if (tree != null)
            {
                const int bufferSize = 256;
                byte* buffer = stackalloc byte[bufferSize];
                StrBuilder sb = new(buffer, bufferSize);
                sb.Append("Total Items: "u8);
                sb.Append(tree.files + tree.folders);
                sb.Append(", Files: "u8);
                sb.Append(tree.files);
                sb.Append(", Folders: "u8);
                sb.Append(tree.folders);

                long end = analysisEnd == 0 ? Stopwatch.GetTimestamp() : analysisEnd;
                TimeSpan duration = TimeSpan.FromSeconds((end - analysisStart) / (double)Stopwatch.Frequency);

                sb.Append(", Elapsed: "u8);
                sb.Append(duration, "m'min' s's'");
                sb.End();
                ImGui.Text(sb);
            }

            if (root == null)
                return;

            float footerHeightToReserve = ImGui.GetStyle().ItemSpacing.Y + ImGui.GetFrameHeightWithSpacing();

            Vector2 avail = ImGui.GetContentRegionAvail();

            if (ImGui.BeginChild(1, new Vector2(widthLeftPanel, 0), 0, ImGuiWindowFlags.HorizontalScrollbar))
            {
                ImGuiTableFlags flags =
                 ImGuiTableFlags.Reorderable |
                 ImGuiTableFlags.Resizable |
                 ImGuiTableFlags.Hideable |
                 ImGuiTableFlags.Sortable |
                 ImGuiTableFlags.SizingFixedFit |
                 ImGuiTableFlags.ScrollX |
                 ImGuiTableFlags.ScrollY |
                 ImGuiTableFlags.PadOuterX | ImGuiTableFlags.ContextMenuInBody | ImGuiTableFlags.NoSavedSettings;
                if (ImGui.BeginTable("Table", 5, flags))
                {
                    ImGui.TableSetupColumn("Name");
                    ImGui.TableSetupColumn("Date Modified");
                    ImGui.TableSetupColumn("Size");
                    ImGui.TableSetupColumn("Percent");
                    ImGui.TableSetupColumn("Percent (Global)");

                    ImGui.TableHeadersRow();

                    ImGuiTableSortSpecsPtr sortSpecs = ImGui.TableGetSortSpecs();

                    if (!sortSpecs.IsNull && sortSpecs.SpecsDirty || sortDirty)
                    {
                        int sortColumnIndex = sortSpecs.Specs.ColumnIndex;
                        bool ascending = sortSpecs.Specs.SortDirection == ImGuiSortDirection.Ascending;

                        TaskManager.Run(Task.Run(() =>
                        {
                            switch (sortColumnIndex)
                            {
                                case 0:
                                    if (ascending)
                                    {
                                        root.SortAZ();
                                    }
                                    else
                                    {
                                        root.SortZA();
                                    }
                                    break;

                                case 2 or 3 or 4:
                                    if (ascending)
                                    {
                                        root.SortAscPercent();
                                    }
                                    else
                                    {
                                        root.SortDescPercent();
                                    }
                                    break;
                            }
                        }));

                        sortDirty = false;
                        sortSpecs.SpecsDirty = false;
                    }

                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);

                    uint id = ImGui.GetID(root.Name);
                    ImGuiP.TreeNodeSetOpen(id, true);
                    bool rootIsOpen = ImGui.TreeNodeEx(root.Name, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.Leaf);

                    if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                    {
                        Selection = null;
                    }

                    ImGui.SameLine();

                    byte* buffer = stackalloc byte[128];

                    Utf8Formatter.FormatByteSize(buffer, 128, root.Size, true, 2);

                    ImGui.TableSetColumnIndex(2);
                    ImGui.Text(buffer);

                    if (rootIsOpen)
                    {
                        for (int i = 0; i < root.Children.Count; i++)
                        {
                            var item = root.Children[i];
                            DisplayNode(item);
                        }

                        ImGui.TreePop();
                    }

                    ImGui.EndTable();
                }
            }
            ImGui.EndChild();

            ImGuiSplitter.VerticalSplitter("S1"u8, ref widthLeftPanel, 0, avail.X);

            ImGui.SameLine();
            if (ImGui.BeginChild(2, default, 0, 0))
            {
                var plotAvail = ImGui.GetContentRegionAvail();
                if (Selection != null && Selection.Children.Count != 0)
                {
                    PlotPie(Selection, plotAvail, pieChartCache);
                }
                else if (root.Children.Count != 0)
                {
                    PlotPie(root, plotAvail, pieChartCache);
                }
            }
            ImGui.EndChild();
        }

        public unsafe struct PieChartCache
        {
            public byte** Names;
            public float* Values;
            public int Length;

            public readonly bool IsNull => Names == null;

            public void Allocate(int length)
            {
                Names = AllocArrayT<byte>(length);
                Values = AllocT<float>(length);
                Length = length;
            }

            public void Release()
            {
                if (Names != null)
                {
                    for (int i = 0; i < Length; i++)
                    {
                        Free(Names[i]);
                    }

                    Free(Names);
                    Names = null;
                }

                if (Values != null)
                {
                    Free(Values);
                    Values = null;
                }
            }
        }

        private PieChartCache pieChartCache;
        private float legendWidth = 200;

        private unsafe void PlotPie(FileTreeNode node, Vector2 size, PieChartCache chartCache)
        {
            var plot = ImPlot.GetPlot("Usage"u8);
            if (!plot.IsNull)
            {
                var pos = ImGui.GetCursorScreenPos();

                var draw = ImGui.GetWindowDrawList();
                var style = ImPlot.GetStyle();

                ImRect bb = new(pos, pos + new Vector2(legendWidth, size.Y));

                ImGuiP.ItemSize(bb);

                draw.AddRectFilled(bb.Min, bb.Max, ImPlot.GetStyleColorU32(ImPlotCol.LegendBg));
                draw.AddRect(bb.Min, bb.Max, ImPlot.GetStyleColorU32(ImPlotCol.LegendBorder));

                ImGui.PushClipRect(bb.Min, bb.Max, true);
                ImPlot.ShowLegendEntries(&plot.Handle->Items, bb, true, style.LegendInnerPadding, style.LegendSpacing, true, draw);
                ImGui.PopClipRect();
            }

            ImGuiSplitter.VerticalSplitter("PieSplit"u8, ref legendWidth, 0, size.X);

            if (!ImPlot.BeginPlot("Usage"u8, new Vector2(-1), ImPlotFlags.Equal | ImPlotFlags.NoLegend))
            {
                return;
            }
            ImPlot.SetupAxis(ImAxis.X1, (byte*)null, ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoTickLabels | ImPlotAxisFlags.NoTickMarks | ImPlotAxisFlags.AutoFit);
            ImPlot.SetupAxis(ImAxis.Y1, (byte*)null, ImPlotAxisFlags.NoLabel | ImPlotAxisFlags.NoTickLabels | ImPlotAxisFlags.NoTickMarks | ImPlotAxisFlags.AutoFit);

            if (chartCache.IsNull || chartCache.Length != node.Children.Count)
            {
                chartCache.Release();
                lock (node._lock)
                {
                    chartCache.Allocate(node.Children.Count);

                    int i = 0;
                    foreach (var child in node.Children)
                    {
                        chartCache.Names[i] = child.Name.ToUTF8Ptr();
                        chartCache.Values[i] = child.PercentUsage;
                        i++;
                    }
                }
            }

            ImPlot.PlotPieChart(chartCache.Names, chartCache.Values, chartCache.Length, 0, 0, 50);
            ImPlot.EndPlot();
        }

        private void RunAnalysis()
        {
            analysisEnd = 0;
            analysisStart = Stopwatch.GetTimestamp();
            Selection = null;
            tree = null;
            root = null;
            tree = new(folder);
            cancellationTokenSource = new();
            task = TaskManager.Run(UsageAnalyser.AnalyzeAsync(tree, ignoreHardlinks, cancellationTokenSource.Token));
            root = tree.Root;
            task = task.ContinueWith(task =>
            {
                analysisEnd = Stopwatch.GetTimestamp();
                sortDirty = true;
            });
        }

        private unsafe void DisplayNode(FileTreeNode node)
        {
            ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAllColumns;
            if (node == Selection)
            {
                flags |= ImGuiTreeNodeFlags.Selected;
            }

            if (node.Children.Count == 0 || node.IsFile)
            {
                flags |= ImGuiTreeNodeFlags.Leaf;
            }

            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);

            const int bufferSize = 128;
            byte* buffer = stackalloc byte[bufferSize];
            StrBuilder builder = new(buffer, bufferSize);
            builder.Append("   "u8); // add space for the icon.
            builder.Append(node.Name);
            builder.End();
            var c = ImGui.GetCursorScreenPos();
            var draw = ImGui.GetWindowDrawList();
            bool isOpen = ImGui.TreeNodeEx(builder, flags);
            builder.Reset();

            var style = ImGui.GetStyle();

            c.X += style.FramePadding.X + ImGui.GetFontSize();

            ulong iconString;
            Utf8Formatter.ConvertUtf16ToUtf8(node.Icon, (byte*)&iconString, 8);
            uint color = node.IsFile ? 0xFFFFFFFF : 0xFF5EDEFF;
            draw.AddText(c, color, (byte*)&iconString);

            if (ImGui.BeginPopupContextItem())
            {
                if (node.IsFile)
                {
                    if (ImGui.MenuItem("Open Containing Folder in Explorer"u8))
                    {
                        Process.Start("explorer.exe", node.Parent!.GetFullPath());
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Delete File"u8))
                    {
                        MessageBox.Show("Delete file", "Are you sure?", node.GetFullPath(), (msg, user) =>
                        {
                            File.Delete((string)user!);
                            node.Parent!.RemoveChild(node);
                        }, MessageBoxType.YesCancel);
                    }
                }
                else
                {
                    if (ImGui.MenuItem("Open Folder in Explorer"u8))
                    {
                        Process.Start("explorer.exe", node.GetFullPath());
                    }
                    ImGui.Separator();
                    if (ImGui.MenuItem("Delete Folder"u8))
                    {
                        MessageBox.Show("Delete folder recursive", "Are you sure?", node.GetFullPath(), (msg, user) =>
                        {
                            Directory.Delete((string)user!, true);
                            node.Parent!.RemoveChild(node);
                        }, MessageBoxType.YesCancel);
                    }
                }
                ImGui.EndPopup();
            }

            if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            {
                Selection = node;
            }

            ImGui.SameLine();

            ImGui.TableSetColumnIndex(1);
            builder.Append(node.LastModified);
            builder.End();
            ImGui.Text(builder);
            builder.Reset();

            ImGui.TableSetColumnIndex(2);
            builder.AppendByteSize(node.Size, true, 2);
            builder.End();
            ImGui.Text(builder);
            builder.Reset();

            ImGui.TableSetColumnIndex(3);
            builder.Append(node.PercentUsage, 2);
            builder.End();
            ImGui.Text(builder);
            builder.Reset();

            ImGui.TableSetColumnIndex(4);
            builder.Append(node.PercentUsageGlobal, 2);
            builder.End();
            ImGui.Text(builder);
            builder.Reset();

            if (isOpen)
            {
                for (int j = 0; j < node.Children.Count; j++)
                {
                    DisplayNode(node.Children[j]);
                }
                ImGui.TreePop();
            }
        }

        private unsafe void DisplayContextMenu()
        {
            if (root == null)
                return;

            if (ImGui.BeginPopupContextWindow("LayoutContextMenu"u8))
            {
                const int bufferSize = 128;
                byte* buffer = stackalloc byte[bufferSize];
                StrBuilder builder = new(buffer, bufferSize);
                builder.Append(MaterialIcons.Sort);
                builder.Append(" Sort A-Z"u8);
                builder.End();
                if (ImGui.MenuItem(builder))
                {
                    root.SortAZ();
                }

                builder.Reset();
                builder.Append(MaterialIcons.Sort);
                builder.Append(" Sort Size Desc"u8);
                builder.End();
                if (ImGui.MenuItem(builder))
                {
                    root.SortDescPercent();
                }

                builder.Reset();
                builder.Append(MaterialIcons.Sort);
                builder.Append(" Sort Size Asc"u8);
                builder.End();
                if (ImGui.MenuItem(builder))
                {
                    root.SortAscPercent();
                }

                ImGui.EndPopup();
            }
        }
    }
}
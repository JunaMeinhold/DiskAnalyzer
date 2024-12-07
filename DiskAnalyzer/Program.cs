using DiskAnalyzer;
using Hexa.NET.KittyUI;
using Hexa.NET.KittyUI.UI;

AppBuilder builder = new();
builder
    .AddWindow<MainWindow>(true, true)
    .EnableDebugTools(true)
    .EnableLogging(true)
    .AddTitleBar<TitleBar>()
    .SetTitle("Disk Analyzer")
    .Style(style =>
    {
        style.IndentSpacing = 5;
        style.CellPadding = new(5);
    })
    .Run();
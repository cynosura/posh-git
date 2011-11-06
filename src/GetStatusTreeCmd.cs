namespace PoshGit {
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Management.Automation;
    using Microsoft.PowerShell.Commands;

    [Cmdlet(VerbsCommon.Show, "GitTree", SupportsShouldProcess = true)]
    public class GetStatusTreeCmd : PSCmdlet {
        enum WorkingStatus : byte {
            None = 0, Added, Removed, Modified, Unmerged
        }

        enum IndexStatus : byte {
            None = 0, Added, Removed, Modified, Unmerged
        }

        class ItemStatus {
            public static ItemStatus Default = new ItemStatus(
                IndexStatus.None, WorkingStatus.None);

            public IndexStatus IndexStatus;
            public WorkingStatus WorkingStatus;

            public ItemStatus(IndexStatus index, WorkingStatus working) {
                IndexStatus = index; WorkingStatus = working;
            }

            public bool IsDefault {
                get {
                    return IndexStatus == IndexStatus.None &&
                        WorkingStatus == WorkingStatus.None;
                }
            }
        }

        [Parameter(Position = 0, Mandatory = false)]
        public PSObject Working { get; set; }

        [Parameter(Position = 1, Mandatory = false)]
        public PSObject Index { get; set; }

        [Parameter(Position = 2, Mandatory = true)]
        public string GitDir { get; set; }

        [Parameter(Position = 3, Mandatory = false)]
        public bool ShowAllFiles { get; set; }

        DirectoryInfo GetRepoRoot() {
            if (GitDir == null) return null;
            return new DirectoryInfo(GitDir).Parent;
        }

        protected override void ProcessRecord() {
            PathInfo pi = base.CurrentProviderLocation(FileSystemProvider.ProviderName);
            DirectoryInfo di = new DirectoryInfo(pi.Path);

            // file paths on windows are case insensitive, 
            // ensure any path comparisons are also.
            var lookup = new Dictionary<string, ItemStatus>(
                StringComparer.OrdinalIgnoreCase);

            bool hasIndexItems = ProcessIndexItems(di, lookup);
            bool hasWorkingItems = ProcessWorkingItems(di, lookup);

            var printRowFunc = GetPrinter(hasIndexItems, hasWorkingItems);

            if (di.Exists) {
                base.Host.UI.WriteLine(di.FullName);

                GitTree.DrawTree(di, lookup.Keys, /* printer */ (item, path) => {
                    if (path != null) {
                        string subpath = path.FullName.Substring(di.FullName.Length + 1);

                        ItemStatus status;
                        if (lookup.TryGetValue(subpath, out status))
                            printRowFunc(status, item);
                        else
                            printRowFunc(ItemStatus.Default, item);
                    }
                    return false;
                }, ShowAllFiles);

                base.Host.UI.WriteLine();
            }
        }

        bool ProcessWorkingItems(DirectoryInfo di, Dictionary<string, ItemStatus> lookup) {
            if (Working == null) return false;

            var proc = new Func<IEnumerable<string>, WorkingStatus, bool>((c, x) => {
                bool hasItems = false;
                foreach (string path in c) {
                    hasItems = true;
                    ItemStatus ws;

                    if (lookup.TryGetValue(path, out ws))
                        ws.WorkingStatus = x;
                    else
                        lookup[path] = new ItemStatus(IndexStatus.None, x);
                }
                return hasItems;
            });

            var a = proc(ProcessStrings(di, Working, "Added"), WorkingStatus.Added);
            var m = proc(ProcessStrings(di, Working, "Modified"), WorkingStatus.Modified);
            var d = proc(ProcessStrings(di, Working, "Deleted"), WorkingStatus.Removed);
            var u = proc(ProcessStrings(di, Working, "Unmerged"), WorkingStatus.Unmerged);

            return a || m || d || u;
        }

        bool ProcessIndexItems(DirectoryInfo di, Dictionary<string, ItemStatus> lookup) {
            if (Index == null) return false;

            var proc = new Func<IEnumerable<string>, IndexStatus, bool>((c, x) => {
                bool hasItems = false;
                foreach (string path in c) {
                    hasItems = true;
                    ItemStatus ws;

                    if (lookup.TryGetValue(path, out ws))
                        ws.IndexStatus = x;
                    else
                        lookup[path] = new ItemStatus(x, WorkingStatus.None);
                }
                return hasItems;
            });

            var a = proc(ProcessStrings(di, Index, "Added"), IndexStatus.Added);
            var m = proc(ProcessStrings(di, Index, "Modified"), IndexStatus.Modified);
            var d = proc(ProcessStrings(di, Index, "Deleted"), IndexStatus.Removed);
            var u = proc(ProcessStrings(di, Index, "Unmerged"), IndexStatus.Unmerged);

            return a || m || d || u;
        }

        IEnumerable<string> ProcessStrings(DirectoryInfo root, PSObject pso, string propertyName) {

            const string REL_PARENT = @"..\";
            var items = (IEnumerable<object>)pso.Members[propertyName].Value;
            DirectoryInfo reporoot = GetRepoRoot();

            foreach (string item in items) {
                if (item.Length > 0) {
                    // normalize
                    var path = item.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

                    // BUG: msysgit 1.7.3.1 : "git status --short" seems to obey
                    //      case-sensitive file paths, which on Windwos results in
                    //      in relative paths from the repo root, rather than from
                    //      the current directory.  This also applies if 
                    //      "git config core.ignorecase" is set to true.
                    if (path.StartsWith(REL_PARENT)) {
                        // turn a path like: 
                        //     ..\..\..\AAA\BBB\afile.txt
                        // into:
                        //     \BBB\afile.txt
                        // if current dir is:
                        //     \AAA

                        var combined = Path.Combine(root.FullName, path);
                        var fullPath = path = Path.GetFullPath(combined);

                        if (fullPath.StartsWith(root.FullName, StringComparison.OrdinalIgnoreCase))
                            path = fullPath.Substring(root.FullName.Length);
                        else
                            continue;
                    }

                    yield return path;
                }
            }
        }

        Action<ItemStatus, string> GetPrinter(bool showIndex, bool showWorking) {
            const string WORKING = "Working";
            const string INDEX = "Index";

            var padding = new ColumnDefinition(string.Empty, 1, Alignment.Left);

            int totWidth = base.Host.UI.RawUI.WindowSize.Width;
            PSTable table = null;
            Action<ItemStatus, string> result;

            if (showIndex && showWorking) { // show the "index" and "working" columns
                ColumnDefinition[] cols = new ColumnDefinition[] {
					// the tree
					new ColumnDefinition(string.Empty, totWidth - (WORKING.Length + INDEX.Length) - 3 /* padding */),

					padding,
					
					// the index/staged items
					new ColumnDefinition(INDEX, INDEX.Length, Alignment.Center),

					padding,

					// the working/unstaged items
					new ColumnDefinition(WORKING, WORKING.Length, Alignment.Center),

					padding
				};

                table = new PSTable(cols, base.Host);
                result = new Action<ItemStatus, string>(
                    (x, y) => table.PrintLine(
                        PrepPrintPathCell(x.IsDefault, cols[0], y),
                        string.Empty, PrepPrintIndexCell(x.IndexStatus, cols[2]),
                        string.Empty, PrepPrintWorkingCell(x.WorkingStatus, cols[4]),
                        string.Empty));

            } else if (showIndex) { // show the "index" column only
                ColumnDefinition[] cols = new ColumnDefinition[] {
					// the tree
					new ColumnDefinition(string.Empty, totWidth - INDEX.Length - 2),

					padding,
					
					// the index/staged items
					new ColumnDefinition(INDEX, INDEX.Length, Alignment.Center),

					padding
				};

                table = new PSTable(cols, base.Host);
                result = new Action<ItemStatus, string>(
                    (x, y) => table.PrintLine(
                        PrepPrintPathCell(x.IsDefault, cols[0], y),
                        string.Empty, PrepPrintIndexCell(x.IndexStatus, cols[2]),
                        string.Empty));

            } else if (showWorking) { // show the working column only
                ColumnDefinition[] cols = new ColumnDefinition[] {
					// the tree
					new ColumnDefinition(string.Empty, totWidth - WORKING.Length - 2),

					padding,

					// the working/unstaged items
					new ColumnDefinition(WORKING, WORKING.Length, Alignment.Center),

					padding
				};

                table = new PSTable(cols, base.Host);
                result = new Action<ItemStatus, string>(
                    (x, y) => table.PrintLine(
                        PrepPrintPathCell(x.IsDefault, cols[0], y),
                        string.Empty, PrepPrintWorkingCell(x.WorkingStatus, cols[2]),
                        string.Empty));

            } else
                result = new Action<ItemStatus, string>((x, y) => { });

            if (table != null) table.PrintHeaders();
            return result;
        }

        private static string PrepPrintPathCell(bool isPhysical, ColumnDefinition col, string path) {
            //			col.Foreground = isPhysical ? ConsoleColor.Gray : ConsoleColor.White;
            return isPhysical ? path : CreateDottedPadding(path, col.Width);
        }

        string PrepPrintWorkingCell(WorkingStatus stats, ColumnDefinition col) {
            ConsoleColor color;
            string symbol;

            switch (stats) {
                case WorkingStatus.Added:
                    symbol = "+"; color = ConsoleColor.Green; break;

                case WorkingStatus.Modified:
                    symbol = "~"; color = ConsoleColor.Cyan; break;

                case WorkingStatus.Removed:
                    symbol = "-"; color = ConsoleColor.Yellow; break;

                case WorkingStatus.Unmerged:
                    symbol = "!"; color = ConsoleColor.White; break;

                default:
                    symbol = string.Empty;
                    color = ConsoleColor.Gray;
                    break;
            }

            col.Foreground = color;
            return symbol;
        }

        string PrepPrintIndexCell(IndexStatus stats, ColumnDefinition col) {
            ConsoleColor color;
            string symbol;

            switch (stats) {
                case IndexStatus.Added:
                    symbol = "+"; color = ConsoleColor.Green; break;

                case IndexStatus.Modified:
                    symbol = "~"; color = ConsoleColor.Cyan; break;

                case IndexStatus.Removed:
                    symbol = "-"; color = ConsoleColor.Yellow; break;

                case IndexStatus.Unmerged:
                    symbol = "!"; color = ConsoleColor.White; break;

                default:
                    symbol = string.Empty;
                    color = ConsoleColor.Gray;
                    break;
            }

            col.Foreground = color;
            return symbol;
        }

        static char[] DOTTED_PAD = { '.', ' ' };
        static string CreateDottedPadding(string path, int colWidth) {
            // fills the rest of the column with dots
            if (path.Length > colWidth)
                colWidth = colWidth - (path.Length % colWidth);
            else
                colWidth = colWidth - path.Length;

            var padding = new char[colWidth + path.Length];

            if (colWidth > 0) {
                int padLength = DOTTED_PAD.Length;
                int e = padding.Length - 1;

                // aligns the padding to the right
                for (int i = 0; i < colWidth; i++)
                    padding[e - i] = DOTTED_PAD[i % padLength];

                padding[path.Length] = DOTTED_PAD[1];
            }

            path.CopyTo(0, padding, 0, path.Length);
            return new string(padding);
        }
    }
}

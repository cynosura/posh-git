namespace PoshGit {
    using System;
    using System.Management.Automation;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Management.Automation.Host;

    [CLSCompliant(false)]
    public class ColumnDefinition {
        readonly public string Name;
        readonly public int Width;
        readonly public Alignment Alignment;

        public ConsoleColor? Foreground { get; set; }

        public ColumnDefinition(string name, int width)
            : this(name, width, Alignment.Left) {
        }

        public ColumnDefinition(string name, int width, Alignment alignment) {
            Name = name; Width = width; Alignment = alignment;
        }
    }

    [CLSCompliant(false)]
    public class PSTable {
        public static string PadCenter(string s, int width) {
            if (s == null || width <= s.Length) return s;

            int padding = width - s.Length;
            return s.PadLeft(s.Length + padding / 2).PadRight(width);
        }

        PSHost mHost;

        public ColumnDefinition[] mColumns;
        public int Width { get { return mColumns.Aggregate(0, (c, x) => c + x.Width); } }

        public PSTable(ColumnDefinition[] columns, PSHost host) {
            mColumns = columns;
            mHost = host;
        }

        public void PrintHeaders() {
            string[] colHeaders = new string[mColumns.Length];
            string[] colBorders = new string[mColumns.Length];

            for (int i = 0; i < mColumns.Length; i++) {
                var col = mColumns[i];

                colHeaders[i] = col.Name ?? string.Empty;

                if (col.Name != null)
                    colBorders[i] = new String('-', Math.Min(col.Name.Length, col.Width));
                else
                    colBorders[i] = String.Empty;
            }

            PrintLine(colHeaders);
            PrintSingleLineInternal(colBorders);
        }

        public void PrintLine(params string[] cells) {
            if (cells.Length != mColumns.Length)
                throw new InvalidOperationException("incorrect number of cells");

            double numLines = 1;
            for (int i = 0; i < cells.Length; i++) {
                // for each cell, check how many lines are required
                // to fit the cell in to its column
                var col = mColumns[i];
                numLines = Math.Max(numLines, Math.Ceiling((double)cells[i].Length / (double)col.Width));
            }

            if (numLines == 1) {
                PrintSingleLineInternal(cells);
            } else {
                string[,] lines = new string[mColumns.Length, (int)numLines];

                // for each cell
                for (int i = 0; i < cells.Length; i++) {
                    var col = mColumns[i];
                    var contents = cells[i];

                    int rem = contents.Length;

                    // for each line in the cell
                    for (int j = 0; j < numLines && rem > 0; j++) {
                        if (rem <= col.Width) {
                            // the remaining text fits in this row
                            lines[i, j] = contents.Substring(j * col.Width, rem);
                            break;
                        } else {
                            lines[i, j] = contents.Substring(j * col.Width, col.Width);
                            rem -= col.Width;
                        }
                    }
                }

                // print for each line
                for (int i = 0; i < numLines; i++) {
                    string[] row = new string[mColumns.Length];

                    for (int j = 0; j < mColumns.Length; j++)
                        row[j] = lines[j, i] ?? String.Empty;

                    PrintSingleLineInternal(row);
                }
            }
        }

        void PrintSingleLineInternal(string[] cells) {
            for (int i = 0; i < cells.Length; i++) {
                var col = mColumns[i];
                string content = null;

                switch (col.Alignment) {
                    case Alignment.Left:
                    case Alignment.Undefined:
                        content = cells[i].PadRight(col.Width);
                        break;

                    case Alignment.Right:
                        content = cells[i].PadLeft(col.Width);
                        break;

                    case Alignment.Center:
                        content = PadCenter(cells[i], col.Width);
                        break;
                }

                if (col.Foreground.HasValue) {
                    mHost.UI.Write(col.Foreground.Value, mHost.UI.RawUI.BackgroundColor, content);
                } else
                    mHost.UI.Write(content);
            }

            if (mHost.UI.RawUI.CursorPosition.X > 0)
                mHost.UI.Write(Environment.NewLine);
        }
    }
}

namespace PoshGit {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.IO;

    public static class DirectoryTree {
        const string BRANCH = "├───";
        const string LAST_IN_BRANCH = "└───";
        const string EMPTY_BRANCH = "    ";
        const string VERT_LINE = "│   ";

        public static void DrawTree(IDirectoryInfo di, Func<string, IFileSystemInfo, bool> printer) {
            var indentStack = new List<string>(8);
            DrawTreeRec(di, printer, indentStack);
        }

        private static void DrawTreeRec(IDirectoryInfo di, Func<string, IFileSystemInfo, bool> printer,
                List<string> indentStack) {

            IDirectoryInfo[] subdirs = di.GetDirectories();
            int lim = subdirs.Length - 1;

            string currIndent = BuildIndent(indentStack);
            bool cancel;

            var files = di.GetFiles();
            if (files.Length > 0) {
                foreach (var file in files) {
                    var line = new string[] { currIndent, subdirs.Length == 0 ? EMPTY_BRANCH : VERT_LINE, file.Name };
                    cancel = printer(String.Join(String.Empty, line), file);
                }

                // print a blank line
                var blankLine = new string[] { currIndent, subdirs.Length == 0 ? EMPTY_BRANCH : VERT_LINE };
                cancel = printer(String.Join(String.Empty, blankLine), null);
            }

            for (int i = 0; i <= lim; i++) {
                var subdir = subdirs[i];
                if (subdir == null) continue;

                bool isLast = i == lim;


                if (isLast) {
                    // last sub dir
                    var line = new string[] { currIndent, LAST_IN_BRANCH, subdir.Name };
                    cancel = printer(String.Join(String.Empty, line), subdir);

                } else {
                    var line = new string[] { currIndent, BRANCH, subdir.Name };
                    cancel = printer(String.Join(String.Empty, line), subdir);
                }

                if (cancel)
                    return;
                else {
                    indentStack.Add(isLast ? EMPTY_BRANCH : VERT_LINE);
                    DrawTreeRec(subdir, printer, indentStack);
                    indentStack.RemoveAt(indentStack.Count - 1);
                }

            }
        }

        private static string[] BuildOutputLine(string output, string indent, int depth) {
            string[] result = new string[depth + 1];

            for (int i = 0; i < depth; i++)
                result[i] = (depth - 1 == i ? indent : new String(' ', indent.Length));

            result[depth] = output;
            return result;
        }

        static string BuildIndent(List<String> indentStack) {
            var arr = indentStack.ToArray();
            return String.Join(String.Empty, arr);
        }
    }

    public class VirtualFileSystemInfo : IFileSystemInfo {
        public string Name {
            get;
            protected set;
        }

        public string FullName {
            get;
            protected set;
        }

        public FileAttributes Attributes {
            get;
            protected set;
        }
    }

    public class VirtualDirectory : VirtualFileSystemInfo, IDirectoryInfo {
        static readonly IDirectoryInfo[] sZeroDirArr = new IDirectoryInfo[0];
        static readonly IFileInfo[] sZeroFileArr = new IFileInfo[0];

        readonly DirectoryInfo mPhysicalDir;
        protected readonly bool VirtualItemsOnly;

        public VirtualDirectory(DirectoryInfo physicalDir, bool virtualItemsOnly) {
            mPhysicalDir = physicalDir;
            VirtualItemsOnly = virtualItemsOnly;

            if (physicalDir != null) {
                // ensure a directory path terminates with a slash
                string fName = physicalDir.FullName.Last().Equals(Path.DirectorySeparatorChar) ?
                    physicalDir.FullName : physicalDir.FullName + Path.DirectorySeparatorChar;

                Name = physicalDir.Name;
                FullName = fName;
                Attributes = physicalDir.Attributes;
            }
        }

        protected virtual VirtualFile CreateFile(FileInfo fileInfo) {
            return new VirtualFile(fileInfo);
        }

        protected virtual VirtualDirectory CreateDirectory(DirectoryInfo directoryInfo) {
            return new VirtualDirectory(directoryInfo, VirtualItemsOnly);
        }

        protected virtual IFileInfo[] GetVirtualFiles() {
            return sZeroFileArr;
        }

        protected virtual IDirectoryInfo[] GetVirtualDirectories() {
            return sZeroDirArr;
        }

        public virtual IFileInfo[] GetFiles() {
            if (mPhysicalDir != null && !VirtualItemsOnly) {
                var files = mPhysicalDir.GetFiles();
                return MergeFileSystemItems(files, GetVirtualFiles(), CreateFile);
            } else
                return GetVirtualFiles();
        }

        public virtual IDirectoryInfo[] GetDirectories() {
            if (mPhysicalDir != null && !VirtualItemsOnly) {
                var dirs = mPhysicalDir.GetDirectories();
                return MergeFileSystemItems(dirs, GetVirtualDirectories(), CreateDirectory);
            } else
                return GetVirtualDirectories();
        }

        static T[] MergeFileSystemItems<T, TPhysical>(TPhysical[] existingItems,
                T[] ghostItems, Func<TPhysical, T> createWrapper) {

            var resultLength = existingItems.Length + ghostItems.Length;
            var result = new T[resultLength];

            int i = 0;
            for (; i < existingItems.Length; i++)
                result[i] = createWrapper(existingItems[i]);

            if (ghostItems.Length > 0)
                ghostItems.CopyTo(result, i);

            return result;
        }
    }

    public class VirtualFile : VirtualFileSystemInfo, IFileInfo {
        public VirtualFile(FileInfo file) {
            Name = file.Name;
            FullName = file.FullName;
            Attributes = file.Attributes;
        }

        public VirtualFile(string name, string fullName) {
            Name = name;
            FullName = fullName;
        }
    }

    public interface IFileSystemInfo {
        string Name { get; }
        string FullName { get; }
        FileAttributes Attributes { get; }
    }

    public interface IDirectoryInfo : IFileSystemInfo {
        IFileInfo[] GetFiles();
        IDirectoryInfo[] GetDirectories();
    }

    public interface IFileInfo : IFileSystemInfo {
    }
}

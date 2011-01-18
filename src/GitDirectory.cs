namespace PoshGit
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Text;
	using System.IO;


	public static class Helpers
	{
		public static char Last(this string target) {
			return target[target.Length - 1];
		}

		public static T Last<T>(this T[] target) {
			return target[target.Length - 1];
		}
	}
	
	public static class GitTree
	{
		public static void DrawTree(DirectoryInfo di, IEnumerable<string> removedPaths, 
				Func<string, IFileSystemInfo, bool> printer, bool includeAllFiles) {

			GitDirectory gitd = new GitDirectory(di, new List<string>(removedPaths), !includeAllFiles);
			DirectoryTree.DrawTree(gitd, printer);
		}
	}
	
	public class GitDirectory : VirtualDirectory
	{
		#region nested types
		class FlattenedSubTree
		{
			public readonly VirtualPath Node;
			public readonly IList<VirtualPath> Children;

			public FlattenedSubTree(VirtualPath node, IList<VirtualPath> children) {
				Node = node;
				Children = children;
			}
		}

		class VirtualPath
		{
			private static char[] PATH_SPLIT = new Char[] { Path.DirectorySeparatorChar };

			public readonly Uri Uri;
			public readonly string[] Components;

			public VirtualPath(string path) {
				path = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
				Uri = new Uri(path, UriKind.Relative);
				Components = path.Split(PATH_SPLIT, StringSplitOptions.RemoveEmptyEntries);
			}

			public VirtualPath(Uri originalUri, string[] components) {
				Components = components;
				Uri = originalUri;
			}

			public bool IsFile {
				get { return Uri.OriginalString.Last().Equals(Path.DirectorySeparatorChar) == false; }
			}
		} 
		#endregion

		#region class members
		static readonly IDirectoryInfo[] NULL_DIRS = new IDirectoryInfo[0];

		static T[] RemoveFirst<T>(T[] items) {
			if (items.Length > 1) {
				T[] result = new T[items.Length - 1];
				Array.Copy(items, 1, result, 0, items.Length - 1);
				return result;
			} else
				return new T[0];
		}

		static List<FlattenedSubTree> MapToSubTrees(IList<VirtualPath> paths) {
			List<VirtualPath> items = new List<VirtualPath>();
			List<FlattenedSubTree> subTrees = new List<FlattenedSubTree>(paths.Count / 3);
			
			var slash = Path.DirectorySeparatorChar.ToString();

			for (int i = 0; i < paths.Count; i++) {
				var curr = paths[i];
				var subTree = GetSubComponents(paths, ref i);

				subTrees.Add(subTree);
			}

			return subTrees;
		}

		static FlattenedSubTree GetSubComponents(IList<VirtualPath> components, ref int fromIndex) {
			// get logical children to the item at the specified index
			// assumes that the components are ordered
			VirtualPath target = components[fromIndex];
			List<VirtualPath> subComponents = null;

			if (target.Components.Length > 1) {
				subComponents = new List<VirtualPath>() {
					new VirtualPath(target.Uri, RemoveFirst(target.Components))
				};

				string prefix = target.Components[0];
				for (++fromIndex; fromIndex < components.Count; fromIndex++) {
					var curr = components[fromIndex];
					if (curr.Components.Length > 1 && curr.Components[0] == prefix)
						subComponents.Add(new VirtualPath(curr.Uri, RemoveFirst(curr.Components)));
					else {
						fromIndex--;
						break;
					}
				}
			}
				
			return new FlattenedSubTree(target, subComponents ?? 
				(IList<VirtualPath>)new VirtualPath[0]{});
		}

		static void SortPaths(List<VirtualPath> paths) {
			paths.Sort((x,y) => Uri.Compare(x.Uri, y.Uri, 
		 		UriComponents.Path, UriFormat.Unescaped, 
				StringComparison.OrdinalIgnoreCase));
		}

		
		#endregion

		readonly IList<FlattenedSubTree> mMissingVirtualPaths;
		readonly IList<IDirectoryInfo> mMissingPhysicalPaths;

		public GitDirectory(DirectoryInfo physicalDirectory, IList<string> virtualItems, bool virtualOnly) :
			base(physicalDirectory, virtualOnly) {

			if (virtualItems != null && virtualItems.Count > 0) {
				var removedComponents = new List<VirtualPath>(
					from x in virtualItems
					select new VirtualPath(x));

				SortPaths(removedComponents);
				var subDirs = physicalDirectory.GetDirectories();

				mMissingVirtualPaths = MapToSubTrees(removedComponents);
				mMissingPhysicalPaths = SplitToPhysical(subDirs, mMissingVirtualPaths);
			} else
				mMissingPhysicalPaths = NULL_DIRS;
		}

		GitDirectory(IDirectoryInfo parent, FlattenedSubTree node) : base(null, true) {
			Name = node.Node.Components[0];
			FullName = string.Join(string.Empty, new string[] { parent.FullName, Name, Path.DirectorySeparatorChar.ToString() });
			Attributes = node.Node.Components.Length > 1 ? FileAttributes.Directory : FileAttributes.Normal;

			mMissingVirtualPaths = MapToSubTrees(node.Children);
			mMissingPhysicalPaths = NULL_DIRS;
		}

		GitDirectory(DirectoryInfo di, FlattenedSubTree node, bool virtualItemsOnly) : base(di, virtualItemsOnly) {
			if (node != null) {
				var subDirs = di.GetDirectories();
				
				mMissingVirtualPaths  = MapToSubTrees(node.Children);
				mMissingPhysicalPaths = SplitToPhysical(subDirs, mMissingVirtualPaths);
			}
		}

		protected override IDirectoryInfo[] GetVirtualDirectories() {
			if (mMissingVirtualPaths != null && mMissingVirtualPaths.Count > 0) {
				var result = new List<IDirectoryInfo>(mMissingVirtualPaths.Count);

				for (int i = 0; i < mMissingVirtualPaths.Count; i++) {
					var vPath = mMissingVirtualPaths[i];
					var components = vPath.Node.Components;

					if (components.Length > 1 || !vPath.Node.IsFile)
						result.Add(new GitDirectory(this, vPath));
				}

				return result.ToArray();
			} else
				return base.GetVirtualDirectories();
		}

		List<IDirectoryInfo> SplitToPhysical(DirectoryInfo[] subDirs, IList<FlattenedSubTree> subTrees) {
			var physicalSubtrees = new List<IDirectoryInfo>(subTrees.Count / 2);

			// from the current location, split all the subtrees in to
			// those with physical nodes and those that are virtual
			for (int i = subTrees.Count - 1; i >= 0; i--) {
				var curr = subTrees[i];
				var prefix = curr.Node.Components[0];

				for (int j = 0; j < subDirs.Length; j++) {
					var physicalDir = subDirs[j];

					if (physicalDir == null)
						continue;

					if (physicalDir.Name.Equals(prefix, StringComparison.OrdinalIgnoreCase)) {
						physicalSubtrees.Add(new GitDirectory(physicalDir, curr, VirtualItemsOnly));
						subTrees.RemoveAt(i);
						subDirs[j] = null;
						
						break;
					}
				}
			}

			// the remaining (non-removed) physical subdirs in the list contain
			// only non-virtual items.  if we're printing non-virtual items then
			// include these directories in the result (if they are not hidden)
			if (!VirtualItemsOnly) {
				for (int j = 0; j < subDirs.Length; j++) {
					if (subDirs[j] != null && (subDirs[j].Attributes & FileAttributes.Hidden) != FileAttributes.Hidden)
						physicalSubtrees.Add(new VirtualDirectory(subDirs[j], false));
				}
			}

			return physicalSubtrees;
		}

		public override IDirectoryInfo[] GetDirectories() {
			var vDirs = GetVirtualDirectories();
			var pDirs = mMissingPhysicalPaths;

			var result = new IDirectoryInfo[pDirs.Count + vDirs.Length];

			if (result.Length > 0) {
				mMissingPhysicalPaths.CopyTo(result, 0);
				vDirs.CopyTo(result, mMissingPhysicalPaths.Count);
			}

			return result;
		}

		protected override IFileInfo[] GetVirtualFiles() {
			if (mMissingVirtualPaths != null) {
				List<IFileInfo> result = new List<IFileInfo>();
				foreach (var vPath in mMissingVirtualPaths) {
					if (vPath.Node.Components.Length == 1) {
						if (vPath.Node.IsFile) {
							var fileName = vPath.Node.Components[0];
							var fullName = FullName + fileName;

							// check that the path doesn't exit, because
							// if it does then the same file would end up
							// being printed twice.
							if (VirtualItemsOnly || !File.Exists(fullName))
								result.Add(new VirtualFile(fileName, fullName));
						}
					}	
				}

				return result.ToArray();
			} else
				return base.GetVirtualFiles();
		}
	}
}

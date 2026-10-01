using System.IO;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// Where the converted tree lives. The converter writes into this folder and
    /// every reader opens it from here, so the folder the converter window
    /// writes to is the folder the preview reads: one setting, not two.
    /// </summary>
    public static class ContentRoot
    {
        /// <summary>
        /// The tree the converter writes by default, relative to the repository
        /// root - the same value ConversionSettings carries as its default
        /// output, so a plain command line run and the preview agree.
        /// </summary>
        public const string Default = "artifacts/content";

        /// <summary>
        /// The repository root. The Unity project is unity/TalesOfPirates, so
        /// the Assets folder sits three levels below the root the converter
        /// runs from.
        /// </summary>
        public static string Repository
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..")); }
        }

        /// <summary>
        /// A root as a full path. A relative one resolves against the
        /// repository rather than the editor's working directory, which is what
        /// makes the converter's default output and the preview's default input
        /// the same folder.
        /// </summary>
        public static string Resolve(string root)
        {
            var path = string.IsNullOrWhiteSpace(root) ? Default : root.Trim();

            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(Repository, path));
        }
    }
}

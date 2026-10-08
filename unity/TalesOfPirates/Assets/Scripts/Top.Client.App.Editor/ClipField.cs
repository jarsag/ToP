using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Choosing a clip of a rig, offered the same way wherever a clip is named. <br/>
    /// The names are in no table and in no asset of the project: they live inside the converted rig
    /// file, which is where these read them. Read once per rig and remembered, because a rig is a
    /// megabyte of json and a field is drawn every frame.
    /// </summary>
    public static class ClipField
    {
        /// <summary>What a field shows when it names no clip.</summary>
        public const string None = "(none)";

        private static readonly Dictionary<string, string[]> Remembered = new Dictionary<string, string[]>();

        /// <summary>
        /// Draws a field that names a clip as a list of the rig's own clips. <br/>
        /// The field stays an ordinary one when the rig is unknown or holds no clip of that name, so
        /// that a name can still be typed when there is nothing to choose from - a hero whose rig has
        /// not been converted yet, or one being set up before its model is.
        /// </summary>
        public static void Choose(SerializedProperty property, string rig, string label = null)
        {
            label ??= property.displayName;

            var names = Names(rig);

            if (names.Length == 0)
            {
                EditorGUILayout.PropertyField(property, true);

                return;
            }

            var list = new string[names.Length + 1];
            list[0] = None;
            Array.Copy(names, 0, list, 1, names.Length);

            var current = Array.IndexOf(list, property.stringValue);
            var picked = EditorGUILayout.Popup(label, Mathf.Max(0, current), list);

            if (picked != current)
            {
                property.stringValue = picked == 0 ? string.Empty : list[picked];
            }
        }

        /// <summary>The clips a converted rig carries, by the rig file's name.</summary>
        public static string[] Names(string rig)
        {
            var file = string.IsNullOrEmpty(rig) ? null : Path.GetFileName(rig);

            if (string.IsNullOrEmpty(file))
            {
                return Array.Empty<string>();
            }

            if (Remembered.TryGetValue(file, out var known))
            {
                return known;
            }

            var names = Read(file);
            Remembered[file] = names;

            return names;
        }

        /// <summary>
        /// Reads the animation names out of a converted rig. It is a glTF binary, and the names sit in
        /// its JSON chunk beside the meshes'; every clip of a character begins with the model it belongs
        /// to - 0003_05_run and the like - which is what tells a clip from a bone.
        /// </summary>
        private static string[] Read(string file)
        {
            var found = new List<string>();

            try
            {
                var here = Directory.GetParent(Application.dataPath);

                // The converted content is not inside the Unity project, so the search starts a couple
                // of folders above it.
                var root = here != null && here.Parent != null && here.Parent.Parent != null
                    ? here.Parent.Parent.FullName
                    : here.FullName;

                var path = Directory.GetFiles(root, file, SearchOption.AllDirectories);

                if (path.Length == 0)
                {
                    return found.ToArray();
                }

                var text = Encoding.UTF8.GetString(File.ReadAllBytes(path[0]));

                var prefix = Path.GetFileNameWithoutExtension(file) + "_";
                var seen = new HashSet<string>();

                foreach (Match match in Regex.Matches(text, "\"name\"\\s*:\\s*\"([^\"]+)\""))
                {
                    var name = match.Groups[1].Value;

                    if (name.StartsWith(prefix, StringComparison.Ordinal) && seen.Add(name))
                    {
                        found.Add(name);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"could not read the clips of '{file}': {exception.Message}");
            }

            found.Sort(StringComparer.Ordinal);

            return found.ToArray();
        }
    }
}

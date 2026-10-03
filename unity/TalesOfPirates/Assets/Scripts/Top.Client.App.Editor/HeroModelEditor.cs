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
    /// Draws the hero the way it is meant to be set up: the fields that name a clip of the rig
    /// are offered as the rig's own clips to choose from, so a name is never typed. The names are
    /// in no table and in no asset of the project - they live in the converted rig file, which is
    /// where this reads them.
    /// </summary>
    [CustomEditor(typeof(HeroModel))]
    public class HeroModelEditor : UnityEditor.Editor
    {
        private const string None = "(none)";

        /// <summary>The fields that name a clip, by the name the component gives them.</summary>
        private static readonly string[] Clips = { "_idle", "_move", "_warIdle", "_warMove", "_warRun", "_warWait" };

        private static readonly Dictionary<string, string[]> Remembered = new Dictionary<string, string[]>();

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var at = serializedObject.GetIterator();
            var into = true;

            while (at.NextVisible(into))
            {
                into = false;

                if (Array.IndexOf(Clips, at.propertyPath) >= 0 && at.propertyType == SerializedPropertyType.String)
                {
                    Choose(at);

                    continue;
                }

                EditorGUILayout.PropertyField(at, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>The clip of the rig, chosen from the rig's own clips.</summary>
        private void Choose(SerializedProperty property)
        {
            var names = Names();

            if (names.Length == 0)
            {
                // Nothing was found to choose from, so the field stays an ordinary name.
                EditorGUILayout.PropertyField(property, true);

                return;
            }

            var list = new string[names.Length + 1];
            list[0] = None;
            Array.Copy(names, 0, list, 1, names.Length);

            var current = Array.IndexOf(list, property.stringValue);
            var picked = EditorGUILayout.Popup(property.displayName, Mathf.Max(0, current), list);

            if (picked != current)
            {
                property.stringValue = picked == 0 ? string.Empty : list[picked];
            }
        }

        /// <summary>The clips of the rig named on this hero, remembered per rig.</summary>
        private string[] Names()
        {
            var rig = serializedObject.FindProperty("_rig");
            var file = rig != null ? Path.GetFileName(rig.stringValue) : null;

            if (string.IsNullOrEmpty(file))
            {
                return new string[0];
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
        /// Reads the animation names out of a converted rig. It is a glTF binary, and the names
        /// sit in its JSON chunk beside the meshes'; every clip of a character begins with the
        /// model it belongs to - 0003_05_run and the like - which is what tells a clip from a bone.
        /// </summary>
        private static string[] Read(string file)
        {
            var found = new List<string>();

            try
            {
                var here = Directory.GetParent(Application.dataPath);

                // The converted content is not inside the Unity project, so the search starts a
                // couple of folders above it.
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
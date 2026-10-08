using System;
using UnityEditor;

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
        /// <summary>The fields that name a clip, by the name the component gives them.</summary>
        private static readonly string[] Clips =
        {
            "_idle", "_move", "_warIdle", "_warMove", "_warRun", "_warWait", "_skill",
        };

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
            var rig = serializedObject.FindProperty("_rig");

            ClipField.Choose(property, rig != null ? rig.stringValue : null);
        }
    }
}

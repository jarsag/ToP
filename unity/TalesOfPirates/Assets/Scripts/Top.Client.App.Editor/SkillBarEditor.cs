using UnityEditor;
using UnityEngine;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Draws a skill the way the hero is drawn: the clip a skill makes the hero play is offered as the
    /// rig's own clips to choose from rather than typed. <br/>
    /// A skill brings its own gesture, so the field is the same kind of field as the hero's own - and it
    /// is offered the same way, from the same rig, or the two would disagree about what a clip is called
    /// for no better reason than where it was written down.
    /// </summary>
    [CustomEditor(typeof(SkillBar))]
    public class SkillBarEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var at = serializedObject.GetIterator();
            var into = true;

            while (at.NextVisible(into))
            {
                into = false;

                if (at.propertyPath == "_skills")
                {
                    Skills(at);

                    continue;
                }

                EditorGUILayout.PropertyField(at, true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>The list of skills, with each one's clip chosen rather than typed.</summary>
        private void Skills(SerializedProperty list)
        {
            EditorGUILayout.PropertyField(list, false);

            if (!list.isExpanded)
            {
                return;
            }

            EditorGUI.indentLevel++;

            list.arraySize = Mathf.Max(0, EditorGUILayout.IntField("Size", list.arraySize));

            for (var i = 0; i < list.arraySize; i++)
            {
                var skill = list.GetArrayElementAtIndex(i);
                var shown = skill.isExpanded;

                skill.isExpanded = EditorGUILayout.Foldout(shown, Name(skill, i), true);

                if (!skill.isExpanded)
                {
                    continue;
                }

                EditorGUI.indentLevel++;

                var at = skill.Copy();
                var end = at.GetEndProperty();
                var into = true;

                while (at.NextVisible(into) && !SerializedProperty.EqualContents(at, end))
                {
                    into = false;

                    if (at.propertyPath.EndsWith(".clip"))
                    {
                        ClipField.Choose(at, Rig());
                    }
                    else
                    {
                        EditorGUILayout.PropertyField(at, true);
                    }
                }

                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
        }

        /// <summary>What a skill is called, for the line it folds out on.</summary>
        private static string Name(SerializedProperty skill, int at)
        {
            var name = skill.FindPropertyRelative("name");

            return name != null && !string.IsNullOrEmpty(name.stringValue)
                ? name.stringValue
                : $"Skill {at + 1}";
        }

        /// <summary>The rig whose clips are offered: the hero's, which is the only rig there is.</summary>
        private static string Rig()
        {
            var hero = FindAnyObjectByType<HeroModel>();

            return hero != null ? hero.RigPath : null;
        }
    }
}

using Top.Client.App;
using UnityEditor;
using UnityEngine;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Draws a pair of numbers as a short list: the setting's own name, and under it one narrow slider
    /// per side with a name of its own. <br/>
    /// Side by side the two sliders came out as wide as the whole inspector and read as one sprawling
    /// thing; stacked they read as what they are - two sides of one setting, one under the other, each
    /// no longer than the words beside it need. A slider rather than a field because how a sheet lies
    /// over an item is a thing found by eye.
    /// </summary>
    [CustomPropertyDrawer(typeof(CarriedItem.RangeVectorAttribute))]
    public class RangeVectorDrawer : PropertyDrawer
    {
        /// <summary>How much of the line a slider takes, the rest being left empty to keep it tidy.</summary>
        private const float Share = 0.55f;

        public override void OnGUI(Rect at, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(at, property, label, true);

                return;
            }

            var setting = (CarriedItem.RangeVectorAttribute)attribute;
            var height = EditorGUIUtility.singleLineHeight;
            var step = height + EditorGUIUtility.standardVerticalSpacing;
            var names = setting.Names;

            EditorGUI.LabelField(new Rect(at.x, at.y, at.width, height), label);

            Slider(new Rect(at.x, at.y + step, at.width, height),
                names != null && names.Length > 0 ? names[0] : "X",
                property.FindPropertyRelative("x"), setting.Min, setting.Max);

            Slider(new Rect(at.x, at.y + (step * 2f), at.width, height),
                names != null && names.Length > 1 ? names[1] : "Y",
                property.FindPropertyRelative("y"), setting.Min, setting.Max);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.propertyType != SerializedPropertyType.Vector2
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : (EditorGUIUtility.singleLineHeight * 3f) + (EditorGUIUtility.standardVerticalSpacing * 2f);
        }

        /// <summary>One narrow slider with its own name in front of it.</summary>
        private static void Slider(Rect at, string name, SerializedProperty number, float min, float max)
        {
            // The name takes the width of its own words, indented a little so that the two read as
            // belonging to the setting above them.
            var indent = 12f;
            var width = EditorStyles.label.CalcSize(new GUIContent(name)).x;
            var labelAt = new Rect(at.x + indent, at.y, width, at.height);

            EditorGUI.LabelField(labelAt, name);

            var fieldAt = new Rect(labelAt.xMax + 4f, at.y, (at.width - indent - width - 4f) * Share, at.height);

            number.floatValue = EditorGUI.Slider(fieldAt, number.floatValue, min, max);
        }
    }
}

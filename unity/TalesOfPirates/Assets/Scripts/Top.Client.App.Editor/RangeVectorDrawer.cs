using Top.Client.App;
using UnityEditor;
using UnityEngine;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Draws a pair of numbers as two named fields side by side, where the plain field would be one box
    /// of two numbers whose meaning has to be remembered. <br/>
    /// Numbers rather than sliders: a setting like how many times a sheet repeats is usually wanted at
    /// some figure thought of rather than hunted for with a handle, and a slider makes a figure hard to
    /// hit.
    /// </summary>
    [CustomPropertyDrawer(typeof(CarriedItem.RangeVectorAttribute))]
    public class RangeVectorDrawer : PropertyDrawer
    {
        private const float Between = 4f;

        public override void OnGUI(Rect at, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(at, property, label, true);

                return;
            }

            var names = ((CarriedItem.RangeVectorAttribute)attribute).Names;
            var y = at.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.LabelField(new Rect(at.x, at.y, at.width, EditorGUIUtility.singleLineHeight), label);

            // The name takes the width of its own words, so that a long one does not crowd the field
            // out; the two fields share what is left of the line between them.
            var line = new Rect(at.x, y, at.width, EditorGUIUtility.singleLineHeight);

            Named(line, names != null && names.Length > 0 ? names[0] : "X", property.FindPropertyRelative("x"));

            var second = new Rect(at.x + (at.width * 0.5f) + Between, y, (at.width * 0.5f) - Between,
                EditorGUIUtility.singleLineHeight);

            Named(second, names != null && names.Length > 1 ? names[1] : "Y", property.FindPropertyRelative("y"));
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.propertyType != SerializedPropertyType.Vector2
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
        }

        private static void Named(Rect at, string name, SerializedProperty number)
        {
            var width = EditorStyles.label.CalcSize(new GUIContent(name)).x;
            var labelAt = new Rect(at.x, at.y, width, at.height);
            var fieldAt = new Rect(at.x + width + 4f, at.y, Mathf.Max(at.width - width - 4f, 4f), at.height);

            EditorGUI.LabelField(labelAt, name);
            number.floatValue = EditorGUI.FloatField(fieldAt, number.floatValue);
        }
    }
}

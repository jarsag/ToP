using Top.Client.App;
using UnityEditor;
using UnityEngine;

namespace Top.Client.App.Editor
{
    /// <summary>
    /// Draws a pair of numbers as two sliders with a name each, where the plain field would be one box
    /// of two numbers to be typed. <br/>
    /// A pair like how many times a sheet repeats across an item and along it is a thing to feel for
    /// rather than to work out, and a slider is how it is felt. The two are drawn side by side under one
    /// label, because they are one setting with two sides rather than two settings.
    /// </summary>
    [CustomPropertyDrawer(typeof(CarriedItem.RangeVectorAttribute))]
    public class RangeVectorDrawer : PropertyDrawer
    {
        private const float Between = 2f;

        public override void OnGUI(Rect at, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Vector2)
            {
                EditorGUI.PropertyField(at, property, label, true);

                return;
            }

            var setting = (CarriedItem.RangeVectorAttribute)attribute;
            var names = setting.Names;
            var height = (at.height - EditorGUIUtility.standardVerticalSpacing) * 0.5f;
            var line = new Rect(at.x, at.y, at.width, height);

            EditorGUI.LabelField(line, label);

            var below = new Rect(at.x, at.y + height + EditorGUIUtility.standardVerticalSpacing, at.width, height);
            var gap = (below.width - Between) * 0.5f;

            var left = new Rect(below.x, below.y, gap, below.height);
            var right = new Rect(below.x + gap + Between, below.y, gap, below.height);

            var x = property.FindPropertyRelative("x");
            var y = property.FindPropertyRelative("y");

            x.floatValue = Named(left, names != null && names.Length > 0 ? names[0] : "X", x.floatValue,
                setting.Min, setting.Max);

            y.floatValue = Named(right, names != null && names.Length > 1 ? names[1] : "Y", y.floatValue,
                setting.Min, setting.Max);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return property.propertyType != SerializedPropertyType.Vector2
                ? EditorGUI.GetPropertyHeight(property, label, true)
                : (EditorGUIUtility.singleLineHeight * 2f) + EditorGUIUtility.standardVerticalSpacing;
        }

        private static float Named(Rect at, string name, float value, float min, float max)
        {
            // The label is drawn separately so that it takes the width of its own words rather than a
            // share of the line, which keeps a long name from crowding the slider out.
            var width = EditorStyles.label.CalcSize(new GUIContent(name)).x;
            var labelAt = new Rect(at.x, at.y, width, at.height);
            var sliderAt = new Rect(at.x + width + 4f, at.y, at.width - width - 4f, at.height);

            EditorGUI.LabelField(labelAt, name);

            return EditorGUI.Slider(sliderAt, value, min, max);
        }
    }
}

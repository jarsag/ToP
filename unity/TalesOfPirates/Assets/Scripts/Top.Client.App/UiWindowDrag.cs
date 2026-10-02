using UnityEngine;
using UnityEngine.EventSystems;

namespace Top.Client.App
{
    /// <summary>
    /// The header of a window: picked up with the mouse, it moves the window under
    /// the pointer, which is how the client's own forms are moved about. What it is
    /// drawn on is a clear rectangle over the top of the window, so the parts that
    /// live there - tabs, buttons - are drawn above it and keep taking their own
    /// clicks.
    /// </summary>
    public class UiWindowDrag : MonoBehaviour, IDragHandler
    {
        /// <summary>The rectangle that is moved, which is the window this header belongs to.</summary>
        public RectTransform Window;

        public void OnDrag(PointerEventData pointer)
        {
            if (Window == null)
            {
                return;
            }

            // A pointer moves in screen pixels and the window is placed in the units
            // of its canvas, which a scale of its own sits between.
            var canvas = Window.GetComponentInParent<Canvas>();
            var scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;

            Window.anchoredPosition += pointer.delta / scale;
        }
    }
}

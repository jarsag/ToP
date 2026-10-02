using UnityEngine;

namespace Top.Client.Core
{
    public static class UnityObjects
    {
        public static void Destroy(Object destroyed)
        {
            if (destroyed == null)
            {
                return;
            }

            Forget(destroyed);

            if (Application.isPlaying)
            {
                Object.Destroy(destroyed);
            }
            else
            {
                Object.DestroyImmediate(destroyed);
            }
        }

        /// <summary>
        /// Lets go of an object the editor is showing before it goes away. A
        /// streamed chunk is destroyed the moment it leaves the view, and an
        /// inspector left pointing at the renderer of one throws once per refresh -
        /// SerializedObjectNotCreatableException, out of the URP mesh editor - with
        /// nothing wrong anywhere but the order of the two.
        /// </summary>
        private static void Forget(Object destroyed)
        {
#if UNITY_EDITOR
            if (!(destroyed is GameObject target))
            {
                return;
            }

            var selected = UnityEditor.Selection.activeGameObject;

            if (selected != null && selected.transform.IsChildOf(target.transform))
            {
                UnityEditor.Selection.activeGameObject = null;
            }
#endif
        }
    }
}

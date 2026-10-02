using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Top.Client.App
{
    /// <summary>
    /// A cell of the inventory that can be picked up with the mouse. What it holds
    /// is asked of whoever built it, and what happens when it is put down is theirs
    /// to say, so the same small piece serves a cell of the bag and a slot of the
    /// body.
    /// <br/>
    /// Picking one up draws its icon under the pointer until it is let go, and lets
    /// go onto whatever the pointer is over: a cell that takes drops does something
    /// with it, and the rest of the window does nothing, which leaves the thing
    /// where it was.
    /// </summary>
    public class UiItemDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        /// <summary>What the cell holds, or zero when it holds nothing.</summary>
        public Func<int> Item;

        /// <summary>What to do when something is dropped on this cell, or null when nothing can be.</summary>
        public Action<UiItemDrag> Drop;

        /// <summary>
        /// What the item under the mouse looks like, which is the icon drawn in the cell
        /// rather than anything the cell draws itself: the cell is a clear square that
        /// takes the mouse, and the picture of the thing lives inside it.
        /// </summary>
        public Func<Sprite> Picture;

        /// <summary>
        /// What a right click does, which is the client's way of putting something on
        /// or taking it off without moving the mouse.
        /// </summary>
        public Action<UiItemDrag> RightClick;

        /// <summary>Which cell of the bag this is, or -1 for a slot of the body.</summary>
        public int Cell = -1;

        /// <summary>Which slot of the body this is, or zero for a cell of the bag.</summary>
        public int Slot;

        /// <summary>Told when something is picked up here, so that it can be written down.</summary>
        public Action<UiItemDrag> Picked = null;

        /// <summary>
        /// Told when something was let go of where nothing could take it, which is how the
        /// window hears that a thing was thrown away rather than put down.
        /// </summary>
        public Action<UiItemDrag> Released = null;

        /// <summary>
        /// Where the pointer was when the thing was let go of, in the screen coordinates the
        /// camera takes - the event carries it, and reading the mouse directly would ask the
        /// wrong input system for it.
        /// </summary>
        public Vector2 LetGoAt;

        /// <summary>
        /// Whether something is being carried about. A click let go of outside the window is
        /// a throw rather than a walk, so the rest of the game has to leave the mouse alone.
        /// </summary>
        public static bool Carrying { get; private set; }

        /// <summary>Whether the button has just come up, so that the flag can outlive the frame.</summary>
        private static bool _released;

        private static RectTransform _dragged;

        public void OnPointerDown(PointerEventData pointer)
        {
            if (pointer.button != PointerEventData.InputButton.Left || Item == null || Item() == 0)
            {
                return;
            }

            // The mouse belongs to the game the moment a thing is pressed on, not when the
            // drag starts: the game reads the mouse itself, so a press it acted on would
            // walk the hero away with the thing still in hand.
            Carrying = true;
        }

        public void OnPointerUp(PointerEventData pointer)
        {
            if (pointer.button == PointerEventData.InputButton.Left)
            {
                _released = true;
            }
        }

        private void LateUpdate()
        {
            // A frame late on purpose: the game reads the same release in its own Update and
            // has to still see that something was in hand.
            if (_released)
            {
                _released = false;
                Carrying = false;
            }
        }

        public void OnPointerClick(PointerEventData pointer)
        {
            if (pointer.button == PointerEventData.InputButton.Right)
            {
                RightClick?.Invoke(this);
            }
        }

        public void OnBeginDrag(PointerEventData pointer)
        {
            if (Item == null || Item() == 0)
            {
                return;
            }

            var sprite = Picture != null ? Picture() : null;
            var canvas = GetComponentInParent<Canvas>();

            if (sprite == null || canvas == null)
            {
                return;
            }

            var ghost = new GameObject("Dragging", typeof(RectTransform)).AddComponent<Image>();

            ghost.sprite = sprite;
            ghost.raycastTarget = false;

            _dragged = ghost.rectTransform;
            _dragged.SetParent(canvas.transform, false);
            _dragged.sizeDelta = sprite.rect.size;
            _dragged.position = pointer.position;

            Carrying = true;

            Picked?.Invoke(this);
        }

        public void OnDrag(PointerEventData pointer)
        {
            if (_dragged != null)
            {
                // The canvas is drawn over the screen, so a pointer position is where
                // the icon goes.
                _dragged.position = pointer.position;
            }
        }

        public void OnEndDrag(PointerEventData pointer)
        {
            LetGoAt = pointer.position;
            _released = true;

            Forget();

            var cell = Under(pointer);

            if (cell == null)
            {
                Released?.Invoke(this);

                return;
            }

            cell.Drop?.Invoke(this);
        }

        /// <summary>
        /// What the pointer was let go over, found with a ray of our own. The ray carried
        /// by the end event is the one from when the drag began, so it names the cell the
        /// thing came from rather than the one it was dropped on - and a thing dropped on
        /// itself does not move.
        /// </summary>
        private static UiItemDrag Under(PointerEventData pointer)
        {
            var system = EventSystem.current;

            if (system == null)
            {
                return null;
            }

            var where = new PointerEventData(system) { position = pointer.position };
            var results = new List<RaycastResult>();

            system.RaycastAll(where, results);

            foreach (var result in results)
            {
                if (result.gameObject == null)
                {
                    continue;
                }

                var cell = result.gameObject.GetComponentInParent<UiItemDrag>();

                if (cell != null)
                {
                    return cell;
                }
            }

            return null;
        }

        private void OnDisable()
        {
            
            // The game has to stop thinking the mouse is carrying something if it was taken
            // out of the window mid-drag.
            Carrying = false;
Forget();
        }

        private static void Forget()
        {
            if (_dragged == null)
            {
                return;
            }

            Destroy(_dragged.gameObject);

            _dragged = null;
        }
    }
}

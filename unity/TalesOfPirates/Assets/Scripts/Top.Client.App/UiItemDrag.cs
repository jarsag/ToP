using System;
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
        /// What a right click does, which is the client's way of putting something on
        /// or taking it off without moving the mouse.
        /// </summary>
        public Action<UiItemDrag> RightClick;

        private static RectTransform _dragged;

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

            var source = GetComponent<Image>();

            if (source == null || source.sprite == null || source.canvas == null)
            {
                return;
            }

            var ghost = new GameObject("Dragging", typeof(RectTransform)).AddComponent<Image>();

            ghost.sprite = source.sprite;
            ghost.raycastTarget = false;

            _dragged = ghost.rectTransform;
            _dragged.SetParent(source.canvas.transform, false);
            _dragged.sizeDelta = source.rectTransform.sizeDelta;
            _dragged.position = pointer.position;
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
            Forget();

            var under = pointer.pointerCurrentRaycast.gameObject;

            if (under == null)
            {
                return;
            }

            var cell = under.GetComponentInParent<UiItemDrag>();

            cell?.Drop?.Invoke(this);
        }

        private void OnDisable()
        {
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

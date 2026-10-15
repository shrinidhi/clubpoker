using System.Collections.Generic;
using ClubPoker.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ClubPoker.Audio
{
    /// <summary>
    /// Plays ButtonClick for every interactable Button/Toggle tap in every scene,
    /// so no prefab needs its own sound component. Fires on release over the same
    /// control that was pressed and only if the finger didn't drag (scroll lists).
    ///
    /// Runs before the EventSystem: a click that closes its own popup (an X
    /// button) would otherwise hide the button before the release check, and the
    /// raycast would find nothing under the finger.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class UIClickSounds : MonoBehaviour
    {
        private readonly List<RaycastResult> _hits = new List<RaycastResult>();
        private PointerEventData _pointer;
        private EventSystem _pointerOwner;
        private Selectable _pressed;
        private Vector2 _pressPos;

        private void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _pressed = SelectableUnder(Input.mousePosition);
                _pressPos = Input.mousePosition;
                return;
            }

            if (!Input.GetMouseButtonUp(0) || _pressed == null)
                return;

            Selectable pressed = _pressed;
            _pressed = null;

            EventSystem es = EventSystem.current;
            float dragThreshold = es != null ? es.pixelDragThreshold : 10f;
            if (((Vector2)Input.mousePosition - _pressPos).sqrMagnitude > dragThreshold * dragThreshold)
                return;

            if (SelectableUnder(Input.mousePosition) != pressed)
                return;

            GameAudio.Play(SoundId.ButtonClick);
        }

        private Selectable SelectableUnder(Vector2 screenPos)
        {
            EventSystem es = EventSystem.current;
            if (es == null)
                return null;

            if (_pointerOwner != es)
            {
                _pointer = new PointerEventData(es);
                _pointerOwner = es;
            }

            _pointer.position = screenPos;
            _hits.Clear();
            es.RaycastAll(_pointer, _hits);

            if (_hits.Count == 0)
                return null;

            GameObject top = _hits[0].gameObject;
            Selectable sel = top.GetComponentInParent<Selectable>();

            if (sel == null || !sel.IsInteractable())
                return null;
            if (!(sel is Button) && !(sel is Toggle))
                return null;
            if (sel.GetComponentInParent<NoClickSound>() != null)
                return null;

            return sel;
        }
    }
}

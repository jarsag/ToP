using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Top.Client.Game.Tables;
using Top.Logging;
using UnityEngine;

namespace Top.Client.App
{
    /// <summary>
    /// What the hero owns and what he is wearing. The bag and the equipment panel
    /// are what will drive this; until they exist the lists below are set in the
    /// inspector, so a set of clothes can be put on and looked at before there is
    /// anything to click.
    /// <br/>
    /// Wearing is per slot of the body. Which model an item contributes comes from
    /// the converted table of items and depends on the class the hero is, and which
    /// slot it fills comes from the module the client names.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        [SerializeField] private MapPreview _preview;
        [SerializeField] private HeroModel _hero;

        /// <summary>What the hero owns, by item id - what a bag would show.</summary>
        [SerializeField] private int[] _owned = { 2196, 359, 535, 711 };

        /// <summary>
        /// The character's own set, by item id: what every slot falls back to when
        /// whatever was over it comes off.
        /// <br/>
        /// It matters because a character is not one model. The body a character is
        /// converted with is its head and not much else, and its arms, legs and feet
        /// are the parts of its own set - so an empty slot is not bare skin, it is a
        /// hole where an arm should be. These are the items that name the character's
        /// own suit, and they start worn.
        /// </summary>
        [SerializeField] private int[] _own = { 2246, 464, 640, 816 };

        private readonly Dictionary<int, int> _equipped = new Dictionary<int, int>();

        /// <summary>
        /// The two hands, out of the client's own places for equipment: its left hand is 6 and its right
        /// is 9, and this port keeps the thin end of the body - head, body, gloves, shoes - starting at
        /// one, so the same two places come out as 5 and 6 here. The two numbers stand beside each other
        /// because they are the only two a thing can be moved between.
        /// </summary>
        private const int RightHand = 5;

        private const int LeftHand = 6;

        private ItemCatalog _items;

        /// <summary>What the hero owns, by item id.</summary>
        public IReadOnlyList<int> Owned => _owned;

        /// <summary>Fired whenever what is worn changes, which is what a window of it redraws on.</summary>
        public event Action Changed;

        /// <summary>The item covering a slot of the body, or zero when the body has that slot bare.</summary>
        public int Equipped(int slot)
        {
            return _equipped.TryGetValue(slot, out var id) ? id : 0;
        }

        /// <summary>
        /// The slot a thing is on, or zero when it is on nothing. <br/>
        /// Asked after putting something on rather than worked out beforehand, because which hand a
        /// thing ends up in is not the caller's to know: a carried thing goes in its own hand and in the
        /// other when that one is taken. A window that assumed the slot it asked for is the slot it got
        /// left the thing in the bag as well, and the hero wore it twice.
        /// </summary>
        public int SlotOf(int id)
        {
            if (id == 0)
            {
                return 0;
            }

            foreach (var pair in _equipped)
            {
                if (pair.Value == id)
                {
                    return pair.Key;
                }
            }

            return 0;
        }

        /// <summary>What an item is, for the class the hero is - what a bag draws a name and an icon from.</summary>
        public bool TryGet(int id, out ItemLookup item)
        {
            item = default;

            return _items != null && _hero != null && _items.TryGet(id, _hero.Class, out item);
        }

        private async void Start()
        {
            var preview = _preview != null ? _preview : FindAnyObjectByType<MapPreview>();
            var hero = _hero != null ? _hero : GetComponent<HeroModel>();

            _hero = hero;

            if (preview == null || preview.Content == null || hero == null)
            {
                Log.Error("the inventory needs a map preview and a hero to dress");

                return;
            }

            try
            {
                _items = (await new TableReader(preview.Content).Read()).Items;
            }
            catch (Exception exception)
            {
                Log.Error("could not read the tables", exception);
            }

            if (_items == null)
            {
                Log.Warning("the converted tree names no items, so nothing can be worn by id");

                return;
            }

            // The character's own set goes on - without it there is a head and little
            // else - and what the hero owns stays in the bag until it is put on.
            foreach (var id in _own)
            {
                await Equip(id);
            }

            // A window built before the tables were read drew an empty bag, so it is
            // told to look again now that there is something to draw.
            Changed?.Invoke();
        }

        /// <summary>
        /// Puts an item on the hero, in the slot of the body it covers. An item that
        /// covers no slot is left alone: a sword is held rather than worn, and a face
        /// is part of the body already.
        /// </summary>
        public async Task<bool> Equip(int id)
        {
            if (_items == null || _hero == null)
            {
                return false;
            }

            if (!_items.TryGet(id, _hero.Class, out var item))
            {
                Log.Warning($"no item {id} in the converted table");

                return false;
            }

            if (item.Slot <= 0)
            {
                Log.Info($"item {id} '{item.Name}' covers no slot of the body");

                return false;
            }

            if (string.IsNullOrEmpty(item.Model))
            {
                Log.Warning($"item {id} '{item.Name}' has no model for class {_hero.Class}");

                return false;
            }

            // A thing carried goes in the hand its kind belongs in - and in the other one when that is
            // taken, which is how the client had it: a sword's own data names two places, the right hand
            // and then the left, and both were used. Nothing else has a second place, so nothing else
            // moves over.
            var slot = Hand(item.Slot);

            await _hero.Wear(slot, item.Model, item.Type);

            _equipped[slot] = id;

            Changed?.Invoke();

            return true;
        }

        /// <summary>
        /// Which hand a thing goes in, out of the hand its kind belongs in: that one when it is free, and
        /// the other when it is not. <br/>
        /// Only the two hands, and only for what is carried: a slot of the body has no second place to
        /// move to, and a thing worn there is simply displaced.
        /// </summary>
        private int Hand(int slot)
        {
            if (slot != RightHand && slot != LeftHand)
            {
                return slot;
            }

            if (!_equipped.ContainsKey(slot))
            {
                return slot;
            }

            var other = slot == RightHand ? LeftHand : RightHand;

            return _equipped.ContainsKey(other) ? slot : other;
        }

        /// <summary>
        /// Takes off whatever covers a slot of the body, and what the character wears
        /// of its own goes back on instead.
        /// <br/>
        /// The character's own is never taken off: its parts are not one model, so a
        /// bare slot of the body is a hole in it rather than skin. Anything put over
        /// that slot gives way to it.
        /// </summary>
        public void Unequip(int slot)
        {
            var own = Own(slot);

            if (own == 0)
            {
                _hero?.TakeOff(slot);

                if (_equipped.Remove(slot))
                {
                    Changed?.Invoke();
                }

                return;
            }

            if (Equipped(slot) != own)
            {
                Put(own);
            }
        }

        /// <summary>
        /// Whether an item is one of the character's own parts. Those never leave it:
        /// taking something off a slot only ever hands the slot back to one of these.
        /// </summary>
        public bool IsOwn(int id)
        {
            foreach (var own in _own)
            {
                if (own == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The item the character wears of its own in a slot, or zero when it wears none there.</summary>
        private int Own(int slot)
        {
            foreach (var id in _own)
            {
                if (TryGet(id, out var item) && item.Slot == slot)
                {
                    return id;
                }
            }

            return 0;
        }

        /// <summary>
        /// Puts an item on out of something that cannot wait for a model to load, which
        /// is what a click is: what goes wrong is written to the log.
        /// </summary>
        private async void Put(int id)
        {
            try
            {
                await Equip(id);
            }
            catch (Exception exception)
            {
                Log.Error($"could not put item {id} on the hero", exception);
            }
        }
    }
}

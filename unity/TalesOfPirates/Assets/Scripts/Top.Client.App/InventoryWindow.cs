using System.Collections.Generic;
using System.Threading.Tasks;
using Top.Client.Game.Tables;
using Top.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Top.Client.App
{
    /// <summary>
    /// The inventory, drawn the way the client drew it: one window of four hundred
    /// and forty by four hundred pixels, the bag down the right of it and the slots
    /// of the body down the left, in the places the client's own form script puts
    /// them.
    /// <br/>
    /// The window is built here rather than authored as a prefab, because its whole
    /// layout is a table of numbers taken from that script and a prefab would only
    /// hide them. The art is the client's own, imported as sprites by Tools -&gt;
    /// Import UI art and loaded by name.
    /// </summary>
    public class InventoryWindow : MonoBehaviour
    {
        /// <summary>
        /// The window is as big as the art it is drawn on. The client's script
        /// declares its form as 440 by 400, but the background it loads is 470 by 433
        /// and the grid painted into that background is at the pitch the script lays
        /// its cells out at - so the image is what the numbers have to agree with.
        /// </summary>
        private const float WindowWidth = 470f;
        private const float WindowHeight = 433f;

        private const float Cell = 32f;
        private const float Frame = 36f;

        private const float BagX = 215f;
        private const float BagY = 55f;
        private const float BagStep = 35f;
        private const int BagColumns = 6;
        private const int BagRows = 8;

        private const float TabWidth = 80f;
        private const float TabHeight = 35f;
        private const float TabY = 2f;
        private const float TabEquipX = 8f;
        private const float TabApparelX = 98f;

        /// <summary>
        /// How far a thing can be thrown, in metres. The ground is a height field rather
        /// than geometry, so the spot a throw lands on is taken on the plane the hero
        /// stands on and kept this close to him.
        /// </summary>
        private const float Reach = 2.5f;

        /// <summary>How much of the top of the window is the strip it is picked up by.</summary>
        private const float HeaderHeight = 36f;

        /// <summary>
        /// Where the slots of the body are nudged to. The client's script puts them where
        /// it puts them, but its window art draws the panel they belong in a little
        /// further in - the white of it starts at 13 by 52 - so the group is moved to sit
        /// inside it rather than over its border.
        /// </summary>
        private const float SlotsX = 7f;

        private const float SlotsY = 14f;

        /// <summary>
        /// The slots of the body, where the client's script puts them: a cell of
        /// thirty-two pixels with a frame of thirty-six around it, two up and left.
        /// </summary>
        private static readonly (string Name, int X, int Y)[] Body =
        {
            ("cmdArmet", 8, 40), ("cmdWing", 8, 89), ("cmdNecklace", 8, 138), ("cmdRightHand", 8, 187),
            ("cmdJewelry4", 8, 236), ("cmdCloak", 8, 285),
            ("cmdCirclet1", 56, 187), ("cmdJewelry1", 56, 236), ("cmdRearPet", 56, 285),
            ("cmdCirclet2", 104, 187), ("cmdJewelry2", 104, 236), ("cmdPet", 104, 285),
            ("cmdBody", 152, 40), ("cmdGlove", 152, 89), ("cmdShoes", 152, 138), ("cmdLeftHand", 152, 187),
            ("cmdJewelry3", 152, 236), ("cmdMount", 152, 285),
        };

        /// <summary>The apparel slots: what may be worn over each, in three columns of step fifty-five.</summary>
        private static readonly (string Name, int X, int Y)[] Apparel =
        {
            ("cmdPetApp", 22, 48), ("cmdDaggerApp", 22, 103), ("cmdGunApp", 22, 158), ("cmdSword1App", 22, 213),
            ("cmdGreatSwordApp", 22, 268),
            ("cmdArmetApp", 82, 48), ("cmdFaceApp", 82, 103), ("cmdBodyApp", 82, 158), ("cmdGloveApp", 82, 213),
            ("cmdShoesApp", 82, 268),
            ("cmdGlowApp", 142, 48), ("cmdStaffApp", 142, 103), ("cmdBowApp", 142, 158), ("cmdSword2App", 142, 213),
            ("cmdShieldApp", 142, 268),
        };

        /// <summary>Which frame of the body a slot of an item is drawn in, by the number the client gives it.</summary>
        private static readonly Dictionary<int, string> Worn = new Dictionary<int, string>
        {
            { 1, "cmdArmet" }, { 2, "cmdBody" }, { 3, "cmdGlove" }, { 4, "cmdShoes" },
            { 5, "cmdRightHand" }, { 6, "cmdLeftHand" },
        };

        [SerializeField] private Inventory _inventory;

        /// <summary>The key that opens it, which the client did not have - it used the shortcut below.</summary>
        [SerializeField] private Key _toggle = Key.I;

        /// <summary>Whether the client's own shortcut, alt and E, opens it as well.</summary>
        [SerializeField] private bool _altE = true;

        [SerializeField] private bool _open;

        /// <summary>
        /// Whether what the mouse does to the bag and the body is written to the log and
        /// every cell is numbered where it stands. It is what to read while something
        /// about the interface is not behaving, and what to turn off when it is.
        /// </summary>
        [SerializeField] private bool _debug = true;

        /// <summary>
        /// How big the interface is drawn. The client drew it a pixel for a pixel on
        /// the screen it was made for - eight hundred by six hundred - and so does
        /// this; the number here is what makes it readable on a screen of another
        /// size or density.
        /// </summary>
        [SerializeField] private float _scale = 1f;

        private RectTransform _window;
        private RectTransform _equip;
        private RectTransform _apparel;
        private readonly Dictionary<int, Image> _worn = new Dictionary<int, Image>();
        private readonly List<Image> _cells = new List<Image>();

        /// <summary>
        /// What stands in each cell of the bag, by the cell, zero being an empty one. A
        /// bag is a place rather than a list: what is put in it stays where it is, and
        /// taking one thing out does not shuffle the rest along.
        /// </summary>
        private readonly int[] _slots = new int[BagColumns * BagRows];

        private Font _font;

        private void Awake()
        {
            _inventory = _inventory != null ? _inventory : FindAnyObjectByType<Inventory>();
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            Build();

            Show(_open);
        }

        private void OnEnable()
        {
            if (_inventory != null)
            {
                _inventory.Changed += Redraw;
            }
        }

        private void OnDisable()
        {
            if (_inventory != null)
            {
                _inventory.Changed -= Redraw;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            var alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

            if (keyboard[_toggle].wasPressedThisFrame || (_altE && _open && alt && keyboard.eKey.wasPressedThisFrame))
            {
                Toggle();
            }
            else if (_open && keyboard.escapeKey.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        /// <summary>Opens it, or closes it if it is open.</summary>
        public void Toggle()
        {
            Show(!_open);
        }

        /// <summary>Shows what the hero owns and what he is wearing, in the places the client put them.</summary>
        private void Build()
        {
            var canvas = new GameObject("Inventory", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var scaler = canvas.GetComponent<CanvasScaler>();

            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = _scale;
            scaler.referencePixelsPerUnit = 1f;

            Pointer();

            // In the middle of whatever screen it is on: the client put it a little
            // off centre on a screen of eight hundred by six hundred, and where a
            // window that gets dragged about starts is not worth copying exactly.
            _window = Panel(canvas.transform, "Window", 0f, 0f, WindowWidth, WindowHeight, centred: true);

            // The whole window takes clicks and not only the cells in it: a click
            // through its frame must not order the hero about underneath.
            Clear(_window, "Cover", 0f, 0f, WindowWidth, WindowHeight);

            Picture(_window, "Background", Art("INV/invform"), 0f, 0f, WindowWidth, WindowHeight);

            Title();

            // The strip along the top is what the window is picked up by. The tabs
            // and the close button are drawn over it and keep their own clicks.
            Clear(_window, "Header", 0f, 0f, WindowWidth, HeaderHeight)
                .gameObject.AddComponent<UiWindowDrag>().Window = _window;

            Tabs();

            _equip = Panel(_window, "Equip", 0f, 0f, WindowWidth, WindowHeight);
            _apparel = Panel(_window, "Apparel", 0f, 0f, WindowWidth, WindowHeight);

            Slots(_equip, Body);
            Slots(_apparel, Apparel);

            Bag(_equip);
            Gold(_equip);

            Close();

            // One page at a time: the window opens on the slots of the body and the
            // bag, while the apparel page waits behind the tab that names it.
            Page(true);

            Fill();

            Redraw();
        }

        private void Title()
        {
            var label = Label(_window, "Title", "Inventory", 10f, 2f, 400f, 150f);

            label.color = Color.black;
            label.fontSize = 16;
            label.alignment = TextAnchor.UpperLeft;

            var shadow = Label(_window, "Title shadow", "Inventory", 11f, 3f, 400f, 150f);

            shadow.color = new Color(1f, 1f, 1f, 0.6f);
            shadow.fontSize = 16;
            shadow.alignment = TextAnchor.UpperLeft;

            // The shadow belongs behind the writing, and the writing was made first.
            shadow.transform.SetSiblingIndex(label.transform.GetSiblingIndex());
        }

        private void Tabs()
        {
            var atlas = Art("INV/ivntab");

            // The frames of the client's tab atlas carry their own writing, so none is
            // added here: the window would show every caption twice.
            Tab(atlas, "Equip tab", TabEquipX, true);
            Tab(atlas, "Apparel tab", TabApparelX, false);
        }

        private void Tab(Sprite atlas, string name, float x, bool equip)
        {
            // The two frames the client draws: the one that is showing is the top
            // row of the atlas, the other the row beneath it.
            var sprite = Piece(atlas, 0f, equip ? 0f : TabHeight, TabWidth, TabHeight);
            var image = Picture(_window, name, sprite, x, TabY, TabWidth, TabHeight);

            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();

            button.targetGraphic = image;
            button.onClick.AddListener(() => Page(equip));
        }

        /// <summary>Shows one of the two pages the window has, and with it the bag or what may be worn.</summary>
        private void Page(bool equip)
        {
            _equip.gameObject.SetActive(equip);
            _apparel.gameObject.SetActive(!equip);

            Redraw();
        }

        private void Slots(RectTransform page, (string Name, int X, int Y)[] slots)
        {
            foreach (var (name, x, y) in slots)
            {
                // A frame of the client's around a cell that takes the mouse and draws
                // nothing of its own.
                Picture(page, name, Art($"eqform/{name}"), x - 2f + SlotsX, y - 2f + SlotsY, Frame, Frame);

                var cell = Clear(page, name + " cell", x + SlotsX, y + SlotsY, Cell, Cell);

                // Letting something go anywhere on the body puts it on, and the four
                // slots the body has also show what is worn in them.
                var drag = cell.gameObject.AddComponent<UiItemDrag>();

                // Letting go anywhere on the body puts a thing on: the item itself knows
                // which slot of the body it covers.
                drag.Drop = PutOn;

                var slot = Slot(name);

                if (slot == 0)
                {
                    continue;
                }

                drag.Slot = slot;
                Number(cell, $"S{slot}");

                _worn[slot] = Inside(cell);
                drag.Item = () => _inventory != null ? _inventory.Equipped(slot) : 0;
                drag.Picture = () => _worn[slot].sprite;
                drag.Picked = picked =>
                    Said($"picked up {Called(picked.Item != null ? picked.Item() : 0)} from the body, " +
                         $"slot {picked.Slot}");

                // The right button on the body takes a thing off, which is the other
                // half of putting it on with the right button in the bag.
                drag.RightClick = ignored => PutAway(-1, drag);
            }
        }

        /// <summary>Which slot of the body a frame of the client's is: the numbers the modules carry.</summary>
        private static int Slot(string frame)
        {
            foreach (var entry in Worn)
            {
                if (entry.Value == frame)
                {
                    return entry.Key;
                }
            }

            return 0;
        }

        private void Bag(RectTransform page)
        {
            var bag = Panel(page, "Bag", BagX, BagY, (BagColumns * BagStep) - 3f, (BagRows * BagStep) - 3f);

            // The ground behind the cells takes a drop as well, so letting go over the
            // bag at all is enough to take something out of a slot. It needs something
            // to be pointed at, which is what the clear image is for.
            var behind = bag.gameObject.AddComponent<Image>();

            behind.color = new Color(0f, 0f, 0f, 0f);
            behind.raycastTarget = true;

            bag.gameObject.AddComponent<UiItemDrag>().Drop = drag => PutAway(-1, drag);

            for (var row = 0; row < BagRows; row++)
            {
                for (var column = 0; column < BagColumns; column++)
                {
                    var index = (row * BagColumns) + column;

                    // The cell takes the mouse and draws nothing: what is drawn in it is
                    // the icon inside it, at the size of its own art.
                    var cell = Clear(bag, $"Cell {index}", column * BagStep, row * BagStep, Cell, Cell);

                    Number(cell, index.ToString());

                    _cells.Add(Inside(cell));

                    var drag = cell.gameObject.AddComponent<UiItemDrag>();

                    drag.Cell = index;
                    drag.Item = () => _slots[index];
                    drag.Picture = () => _cells[index].sprite;

                    // The left button picks a thing up and the right button puts it on,
                    // as the client does; letting go over a cell moves it there or changes
                    // places with what is in it.
                    // The handler is given the thing that was picked up, which is not the
                    // cell it is being dropped on.
                    drag.Drop = source => PutAway(index, source);
                    drag.Released = Throw;
                    drag.RightClick = PutOn;
                    drag.Picked = picked =>
                        Said($"picked up {Called(picked.Item != null ? picked.Item() : 0)} from {At(picked.Cell)}");
                }
            }
        }

        private void Gold(RectTransform page)
        {
            Picture(page, "Gold", Art("INV/goldimp"), 200f, 342f, 150f, 53f);

            var gold = Label(page, "Gold value", "0", 224f, 350f, 91f, 8f);

            gold.color = Color.black;
            gold.fontSize = 10;
            gold.alignment = TextAnchor.MiddleRight;

            var imp = Label(page, "Imp value", "0", 224f, 376f, 91f, 8f);

            imp.color = Color.black;
            imp.fontSize = 10;
            imp.alignment = TextAnchor.MiddleRight;
        }

        private void Close()
        {
            // The client keeps its small buttons in one atlas and names a rectangle
            // in it: fourteen by fourteen at a hundred and sixteen across.
            var button = Picture(_window, "Close", Piece(Art("PublicC"), 116f, 175f, 14f, 14f), 415f, 1f, 14f, 14f);

            button.raycastTarget = true;
            button.gameObject.AddComponent<Button>().onClick.AddListener(() => Show(false));
        }

        /// <summary>Draws what the hero owns in the bag and what he wears in the slots of the body.</summary>
        private void Redraw()
        {
            if (_inventory == null)
            {
                return;
            }

            for (var i = 0; i < _cells.Count; i++)
            {
                Show(_cells[i], _slots[i]);
            }

            foreach (var entry in _worn)
            {
                Show(entry.Value, _inventory.Equipped(entry.Key));
            }
        }

        /// <summary>
        /// Draws what an item looks like in a cell, at the size its art was drawn at and
        /// centred. The client draws an icon as it is rather than stretching it to the
        /// cell, so one that is a little wider than its cell overlaps the frame around
        /// it the same way there.
        /// </summary>
        private void Show(Image icon, int id)
        {
            icon.sprite = id != 0 && _inventory != null && _inventory.TryGet(id, out var item)
                ? Icon(item.Icon)
                : null;

            icon.enabled = icon.sprite != null;

            if (icon.enabled)
            {
                icon.SetNativeSize();
            }
        }

        /// <summary>
        /// Puts what the hero owns into the bag, in the order it is listed, and only
        /// once - from then on a cell holds what it holds.
        /// </summary>
        private void Fill()
        {
            if (_inventory == null)
            {
                return;
            }

            for (var i = 0; i < _inventory.Owned.Count && i < _slots.Length; i++)
            {
                _slots[i] = _inventory.Owned[i];
            }

            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != 0)
                {
                    Said($"the bag starts with {Called(_slots[i])} in {At(i)}");
                }
            }

            Snapshot("as the bag is filled");
            Settle();
        }

        /// <summary>How many cells hold something, and what they hold, as one line.</summary>
        private void Snapshot(string when)
        {
            var held = 0;
            var list = string.Empty;

            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == 0)
                {
                    continue;
                }

                held++;
                list += list.Length == 0 ? $"{At(i)}={_slots[i]}" : $", {At(i)}={_slots[i]}";
            }

            var owned = _inventory != null ? _inventory.Owned.Count : 0;

            Said($"{when}: {held} item(s) in the bag, {owned} owned - {list}");
        }

        /// <summary>
        /// Says what the bag holds once the start of the game has had time to finish. The
        /// window fills the bag as soon as it is built, while the character's own set is
        /// being put on at the same time, so a thing that goes missing goes missing then.
        /// </summary>
        private async void Settle()
        {
            await System.Threading.Tasks.Task.Delay(2000);

            Snapshot("two seconds after the start");
        }

        /// <summary>Empties the cell an item stands in, which is what putting it on does.</summary>
        private void Take(int id)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == id)
                {
                    _slots[i] = 0;

                    return;
                }
            }
        }

        /// <summary>Puts an item in the first empty cell, which is where taking it off leaves it.</summary>
        private void Store(int id)
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == 0)
                {
                    _slots[i] = id;

                    return;
                }
            }
        }

        /// <summary>
        /// Putting something down on the body puts it on, wherever on the body it was
        /// let go: the item knows which slot of the body it covers.
        /// </summary>
        /// <summary>
        /// Something was let go over the body: it is put on, and whatever that displaces
        /// goes into the bag.
        /// </summary>
        private async void PutOn(UiItemDrag drag)
        {
            var id = drag.Item != null ? drag.Item() : 0;

            Said($"put on asked for {At(drag.Cell)}, slot {drag.Slot}: id {id}");

            if (id == 0 || _inventory == null || !_inventory.TryGet(id, out var item))
            {
                Said($"  nothing to put on: id {id} is not in the table of items");

                return;
            }

            if (item.Slot <= 0)
            {
                Said($"  {Called(id)} covers no slot of the body");

                return;
            }

            try
            {
                var displaced = _inventory.Equipped(item.Slot);

                if (!await Equip(id))
                {
                    Said($"  {Called(id)} could not be put on");

                    return;
                }

                // The cell it came from empties only now that the thing is really on.
                Take(id);

                if (displaced != 0 && displaced != id && !_inventory.IsOwn(displaced))
                {
                    Store(displaced);

                    Said($"  {Called(id)} is on, and {Called(displaced)} went into the bag");
                }
                else
                {
                    Said($"  {Called(id)} is on");
                }

                Redraw();
            }
            catch (System.Exception exception)
            {
                Log.Error($"could not put item {id} on the hero", exception);
            }
        }

        /// <summary>
        /// Something was let go over the bag - over a cell of it, or over the bag itself
        /// when no cell is under the pointer. What was picked up in the bag changes
        /// places with what is in the cell it was let go over; what came off the body
        /// goes into that cell, or into the first free one.
        /// </summary>
        /// <summary>
        /// Something was let go of outside the window. A thing out of the bag is thrown at
        /// the ground the pointer names, within reach of the hero, and leaves the bag; what
        /// the row says it looks like on the ground is what the mark is drawn from.
        /// </summary>
        private void Throw(UiItemDrag drag)
        {
            var id = drag.Item != null ? drag.Item() : 0;

            if (id == 0 || drag.Cell < 0 || _inventory == null || !_inventory.TryGet(id, out var item))
            {
                return;
            }

            var hero = FindAnyObjectByType<HeroController>();
            var from = hero != null ? hero.transform.position : transform.position;

            var camera = Camera.main;
            var ray = camera != null ? camera.ScreenPointToRay(drag.LetGoAt) : new Ray(from, Vector3.forward);
            var ground = new Plane(Vector3.up, from);

            if (!ground.Raycast(ray, out var distance))
            {
                Said($"nothing dropped: the pointer is not over the world");

                return;
            }

            var away = ray.GetPoint(distance) - from;

            away.y = 0f;

            if (away.magnitude > Reach)
            {
                away = away.normalized * Reach;
            }

            var spot = from + away;

            var bag = FindAnyObjectByType<GroundItems>();

            if (bag == null)
            {
                // The scene does not have to be wired up for this to work: the things the
                // hero lets go of are kept beside him, wherever he is.
                bag = new GameObject("Ground items").AddComponent<GroundItems>();

                Said("the scene has no GroundItems, so one was made to drop it on");
            }

            // Where a thing taken back off the ground goes: into the bag it came out of, in
            // the first cell that is free.
            bag.Picked = picked =>
            {
                Store(picked);
                Redraw();

                Said($"picked item {picked} off the ground");
            };

            bag.Drop(id, item.DropModel, from, spot);

            Take(id);
            Redraw();
        }

        private void PutAway(int cell, UiItemDrag drag)
        {
            var id = drag.Item != null ? drag.Item() : 0;

            Said($"let go on {At(cell)}, carrying id {id} from {At(drag.Cell)}, slot {drag.Slot}");

            if (id == 0)
            {
                return;
            }

            if (drag.Cell >= 0)
            {
                Move(drag.Cell, cell >= 0 ? cell : FirstFree());

                Redraw();

                return;
            }

            // Out of a slot of the body: it is put into the bag before it is taken off,
            // because taking it off is asynchronous - the character's own part has to
            // load first - and by then the slot answers to something else.
            if (_inventory != null && _inventory.Equipped(drag.Slot) == id && !_inventory.IsOwn(id))
            {
                var into = cell >= 0 && _slots[cell] == 0 ? cell : FirstFree();

                if (into >= 0)
                {
                    _slots[into] = id;

                    Said($"  {Called(id)} taken off slot {drag.Slot} into {At(into)}");
                }

                _inventory.Unequip(drag.Slot);
            }
            else
            {
                Said($"  nothing taken off slot {drag.Slot}");
            }

            Redraw();
        }

        /// <summary>
        /// Puts what is in one cell into another, changing places with what is there:
        /// dragging a thing onto an occupied cell swaps the two rather than dropping one
        /// of them.
        /// </summary>
        private void Move(int from, int to)
        {
            if (from == to || from < 0 || to < 0 || from >= _slots.Length || to >= _slots.Length)
            {
                Said($"  nothing moved: {At(from)} to {At(to)}");

                return;
            }

            var held = _slots[to];
            var moved = _slots[from];

            _slots[to] = moved;
            _slots[from] = held;

            Said(held == 0
                ? $"  {Called(moved)} moved from {At(from)} to {At(to)}"
                : $"  {Called(moved)} and {Called(held)} changed places between {At(from)} and {At(to)}");
        }

        private int FirstFree()
        {
            for (var i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == 0)
                {
                    return i;
                }
            }

            return -1;
        }

        private async Task<bool> Equip(int id)
        {
            return _inventory != null && await _inventory.Equip(id);
        }

        private void Show(bool open)
        {
            _open = open;

            if (_window != null)
            {
                _window.gameObject.SetActive(open);
            }

            if (open)
            {
                Redraw();
            }
        }

        /// <summary>
        /// An event system, because a canvas of buttons needs one to click them and a
        /// scene of a walking hero need not have one.
        /// </summary>
        private static void Pointer()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            var system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            system.transform.SetAsFirstSibling();
        }

        /// <summary>
        /// A rectangle of the interface, measured from the top left as the client
        /// measures - or set in the middle of its parent, for a window.
        /// </summary>
        private static RectTransform Panel(Transform parent, string name, float x, float y, float width, float height,
            bool centred = false)
        {
            var panel = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)panel.transform;

            rect.SetParent(parent, false);

            if (centred)
            {
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
            }
            else
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(x, -y);
            }

            rect.sizeDelta = new Vector2(width, height);

            return rect;
        }

        private static Image Picture(RectTransform parent, string name, Sprite sprite, float x, float y, float width,
            float height)
        {
            var image = Panel(parent, name, x, y, width, height).gameObject.AddComponent<Image>();

            image.sprite = sprite;
            image.raycastTarget = false;
            image.enabled = sprite != null;

            return image;
        }

        /// <summary>
        /// An area that takes clicks and draws nothing: both the face of the window and
        /// the strip it is picked up by need one, and a picture with no sprite would
        /// draw a white rectangle instead.
        /// </summary>
        /// <summary>
        /// Where an item's icon is drawn inside a cell: the cell itself is a clear
        /// square that takes the mouse, and the icon is a child of it, centred, so it
        /// can be drawn at the size of its own art without moving the cell.
        /// </summary>
        private static Image Inside(Image cell)
        {
            var icon = Picture((RectTransform)cell.transform, "Icon", null, 0f, 0f, Cell, Cell);
            var rect = (RectTransform)icon.transform;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;

            return icon;
        }

        private static Image Clear(RectTransform parent, string name, float x, float y, float width, float height)
        {
            var image = Picture(parent, name, null, x, y, width, height);

            image.color = new Color(0f, 0f, 0f, 0f);
            image.raycastTarget = true;
            image.enabled = true;

            return image;
        }

        private Text Label(RectTransform parent, string name, string caption, float x, float y, float width,
            float height)
        {
            var text = Panel(parent, name, x, y, width, height).gameObject.AddComponent<Text>();

            text.font = _font;
            text.text = caption;
            text.raycastTarget = false;

            return text;
        }

        /// <summary>
        /// A piece of an atlas, which is how the client keeps its small buttons: the
        /// script names a rectangle in pixels from the top left, and a sprite
        /// rectangle is measured from the bottom left.
        /// </summary>
        private static Sprite Piece(Sprite atlas, float x, float y, float width, float height)
        {
            if (atlas == null)
            {
                return null;
            }

            var texture = atlas.texture;

            return Sprite.Create(texture, new Rect(x, texture.height - y - height, width, height),
                new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>An icon of an item, out of the art the client names in its item table. It is
        /// looked for without complaint: a client can name an icon it never shipped, and
        /// an empty cell is a better answer to that than a warning on every redraw.
        /// </summary>
        private static Sprite Icon(string icon)
        {
            return string.IsNullOrEmpty(icon) ? null : Resources.Load<Sprite>($"Ui/icon/{icon}");
        }

        /// <summary>One line about what the interface has just done, when it is being watched.</summary>
        private void Said(string what)
        {
            if (_debug)
            {
                Log.Info($"inventory: {what}");
            }
        }

        /// <summary>Where a cell of the bag is: the number on it, and the column and row it sits in.</summary>
        private static string At(int cell)
        {
            return cell < 0 ? "the bag" : $"cell {cell} ({cell % BagColumns},{cell / BagColumns})";
        }

        /// <summary>What a thing is called, for a line about it.</summary>
        private string Called(int id)
        {
            return _inventory != null && _inventory.TryGet(id, out var item) && !string.IsNullOrEmpty(item.Name)
                ? $"'{item.Name}' (id {id}, slot {item.Slot})"
                : $"id {id}";
        }

        /// <summary>
        /// Writes the number of a cell on it, so that a line of the log can be pointed at
        /// something on the screen. Slots of the body are numbered by the slot they are.
        /// </summary>
        private void Number(Image cell, string number)
        {
            if (!_debug)
            {
                return;
            }

            var label = Label((RectTransform)cell.transform, "Number", number, 1f, 0f, Cell, 12f);

            label.color = new Color(0f, 0f, 0f, 0.55f);
            label.fontSize = 9;
            label.alignment = TextAnchor.UpperLeft;
        }

        private static Sprite Art(string name)
        {
            var sprite = Resources.Load<Sprite>($"Ui/{name}");

            if (sprite == null)
            {
                Log.Warning($"no interface art for '{name}': run Tools -> Import UI art");
            }

            return sprite;
        }
    }
}

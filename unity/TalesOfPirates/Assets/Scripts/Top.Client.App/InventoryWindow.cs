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

        /// <summary>How much of the top of the window is the strip it is picked up by.</summary>
        private const float HeaderHeight = 36f;

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
        };

        [SerializeField] private Inventory _inventory;

        /// <summary>The key that opens it, which the client did not have - it used the shortcut below.</summary>
        [SerializeField] private Key _toggle = Key.I;

        /// <summary>Whether the client's own shortcut, alt and E, opens it as well.</summary>
        [SerializeField] private bool _altE = true;

        [SerializeField] private bool _open;

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

        /// <summary>What the bag shows, by the position in it: what is owned and not worn.</summary>
        private readonly List<int> _bag = new List<int>();

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
            var cover = Picture(_window, "Cover", null, 0f, 0f, WindowWidth, WindowHeight);

            cover.raycastTarget = true;
            cover.enabled = true;

            Picture(_window, "Background", Art("INV/invform"), 0f, 0f, WindowWidth, WindowHeight);

            Title();

            // The strip along the top is what the window is picked up by. The tabs
            // and the close button are drawn over it and keep their own clicks.
            var header = Picture(_window, "Header", null, 0f, 0f, WindowWidth, HeaderHeight);

            header.raycastTarget = true;
            header.enabled = true;

            header.gameObject.AddComponent<UiWindowDrag>().Window = _window;

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

            Tab(atlas, "Equip tab", TabEquipX, "Equip", true);
            Tab(atlas, "Apparel tab", TabApparelX, "Apparel", false);
        }

        private void Tab(Sprite atlas, string name, float x, string caption, bool equip)
        {
            // The two frames the client draws: the one that is showing is the top
            // row of the atlas, the other the row beneath it.
            var sprite = Piece(atlas, 0f, equip ? 0f : TabHeight, TabWidth, TabHeight);
            var image = Picture(_window, name, sprite, x, TabY, TabWidth, TabHeight);

            image.raycastTarget = true;

            var button = image.gameObject.AddComponent<Button>();

            button.targetGraphic = image;
            button.onClick.AddListener(() => Page(equip));

            var label = Label((RectTransform)image.transform, name + " label", caption, 0f, 4f, TabWidth, 20f);

            label.color = Color.black;
            label.fontSize = 12;
            label.alignment = TextAnchor.UpperCenter;
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
                Picture(page, name, Art($"eqform/{name}"), x - 2f, y - 2f, Frame, Frame);

                var cell = Picture(page, name + " cell", null, x, y, Cell, Cell);

                cell.raycastTarget = true;

                // Letting something go anywhere on the body puts it on, and the four
                // slots the body has also show what is worn in them.
                var drag = cell.gameObject.AddComponent<UiItemDrag>();

                drag.Drop = PutUp;

                var slot = Slot(name);

                if (slot == 0)
                {
                    continue;
                }

                _worn[slot] = cell;
                drag.Item = () => _inventory != null ? _inventory.Equipped(slot) : 0;

                // The right button on the body takes a thing off, which is the other
                // half of putting it on with the right button in the bag.
                drag.RightClick = PutDown;
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

            bag.gameObject.AddComponent<UiItemDrag>().Drop = PutDown;

            for (var row = 0; row < BagRows; row++)
            {
                for (var column = 0; column < BagColumns; column++)
                {
                    var index = (row * BagColumns) + column;
                    var cell = Picture(bag, $"Cell {index}", null, column * BagStep, row * BagStep, Cell, Cell);

                    cell.raycastTarget = true;
                    _cells.Add(cell);

                    var drag = cell.gameObject.AddComponent<UiItemDrag>();

                    drag.Item = () => index < _bag.Count ? _bag[index] : 0;

                    // The left button picks a thing up and the right button puts it on,
                    // as the client does; letting go over the bag takes it off again.
                    drag.Drop = PutDown;
                    drag.RightClick = PutUp;
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

            _bag.Clear();

            foreach (var id in _inventory.Owned)
            {
                // What is worn is drawn in its slot of the body and not in the bag:
                // one thing is in one place at a time, which is also what drag and
                // drop moves it between.
                if (_inventory.TryGet(id, out var owned) && owned.Slot > 0 && _inventory.Equipped(owned.Slot) == id)
                {
                    continue;
                }

                _bag.Add(id);
            }

            for (var i = 0; i < _cells.Count; i++)
            {
                _cells[i].sprite = i < _bag.Count && _inventory.TryGet(_bag[i], out var item)
                    ? Icon(item.Icon)
                    : null;

                _cells[i].enabled = _cells[i].sprite != null;
            }

            foreach (var entry in _worn)
            {
                var worn = _inventory.Equipped(entry.Key);

                entry.Value.sprite = worn != 0 && _inventory.TryGet(worn, out var item) ? Icon(item.Icon) : null;
                entry.Value.enabled = entry.Value.sprite != null;
            }
        }

        /// <summary>
        /// Putting something down on the body puts it on, wherever on the body it was
        /// let go: the item knows which slot of the body it covers.
        /// </summary>
        private void PutUp(UiItemDrag drag)
        {
            var id = drag.Item != null ? drag.Item() : 0;

            if (id != 0)
            {
                Wear(id);
            }
        }

        /// <summary>
        /// Puts an item on out of a click, which cannot wait for a model to load: what
        /// goes wrong is written to the log rather than thrown at the pointer.
        /// </summary>
        private async void Wear(int id)
        {
            try
            {
                await Equip(id);
            }
            catch (System.Exception exception)
            {
                Log.Error($"could not put item {id} on the hero", exception);
            }
        }

        /// <summary>Putting something down on the bag takes it off the body, if that is where it was.</summary>
        private void PutDown(UiItemDrag drag)
        {
            var id = drag.Item != null ? drag.Item() : 0;

            if (id == 0 || _inventory == null || !_inventory.TryGet(id, out var item) || item.Slot <= 0)
            {
                return;
            }

            if (_inventory.Equipped(item.Slot) == id)
            {
                _inventory.Unequip(item.Slot);
            }
        }

        private async Task Equip(int id)
        {
            try
            {
                await _inventory.Equip(id);
            }
            catch (System.Exception exception)
            {
                Log.Error($"could not put item {id} on the hero", exception);
            }
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

        /// <summary>An icon of an item, out of the art the client names in its item table.</summary>
        private static Sprite Icon(string icon)
        {
            return string.IsNullOrEmpty(icon) ? null : Art($"icon/{icon}");
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

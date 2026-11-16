using System;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ObenseuerOS
{
    internal sealed class MinesweeperWindow : MonoBehaviour
    {
        private const string Preference = "nomad.obenseuer.os.ugui.minesweeper.level";
        private static readonly int[] Rows = { 9, 16, 16 };
        private static readonly int[] Columns = { 9, 16, 30 };
        private static readonly int[] Mines = { 10, 40, 99 };
        private static readonly string[] Levels = { "Beginner", "Intermediate", "Expert" };
        private static readonly Color[] NumberColors = { Color.black, Color.blue, new Color32(0, 128, 0, 255), Color.red, new Color32(0, 0, 128, 255), new Color32(128, 0, 0, 255), new Color32(0, 128, 128, 255), Color.black, Color.gray };
        private RectTransform board;
        private UnityEngine.UI.ScrollRect scroll;
        private Text counter;
        private Text timer;
        private Text message;
        private Text face;
        private Font font;
        private float scale;
        private float width;
        private float height;
        private float started;
        private int seconds;
        private int level;
        private bool timerStarted;
        private bool suppress;
        private MinesweeperGame game;
        private Image[] cellImages;
        private Text[] cellLabels;

        internal static MinesweeperWindow Create(RectTransform desktop, Font font, float scale, int columns)
        {
            float width = Mathf.Min(720f, Mathf.Max(1f, desktop.rect.width / scale - 64f));
            float height = Mathf.Min(560f, Mathf.Max(1f, desktop.rect.height / scale - 64f));
            float x = Mathf.Min(16f + columns * 88f + 56f, Mathf.Max(0f, desktop.rect.width / scale - width));
            RectTransform frame = DesktopUi.Panel(desktop, "Minesweeper window", new Color32(190, 205, 222, 217));
            DesktopUi.TopLeft(frame, x, 32f, width, height, scale);
            MinesweeperWindow window = frame.gameObject.AddComponent<MinesweeperWindow>();
            window.font = font;
            window.scale = scale;
            window.width = width;
            window.height = height;
            window.Build(frame);
            frame.gameObject.SetActive(false);
            return window;
        }

        internal void Open()
        {
            bool alreadyOpen = gameObject.activeSelf;
            gameObject.SetActive(true);
            transform.SetSiblingIndex(transform.parent.childCount - 2);
            if (alreadyOpen) return;
            level = Mathf.Clamp(PlayerPrefs.GetInt(Preference, 0), 0, 2);
            NewGame();
        }

        private void Build(RectTransform frame)
        {
            RectTransform bar = DesktopUi.Panel(frame, "Title bar", Color.clear);
            DesktopUi.TopLeft(bar, 6f, 6f, Mathf.Max(0f, width - 12f), 30f, scale);
            DesktopUi.MakeDraggable(bar, frame);
            Text title = DesktopUi.Label(bar, font, "Minesweeper", Mathf.RoundToInt(14f * scale), Color.black, TextAnchor.MiddleLeft);
            DesktopUi.TopLeft((RectTransform)title.transform, 8f, 0f, Mathf.Max(0f, width - 80f), 30f, scale);
            RectTransform close = DesktopUi.Panel(bar, "Close", new Color32(199, 55, 47, 255));
            DesktopUi.TopLeft(close, Mathf.Max(0f, width - 58f), 5f, 46f, 20f, scale);
            Image closeIcon = OsUguiPrototype.Icon(close, "x", Color.white);
            DesktopUi.TopLeft((RectTransform)closeIcon.transform, 16f, 3f, 14f, 14f, scale);
            Button(close, delegate { gameObject.SetActive(false); });
            RectTransform content = DesktopUi.Panel(frame, "Minesweeper", new Color32(192, 192, 192, 255));
            DesktopUi.TopLeft(content, 6f, 36f, width - 12f, height - 42f, scale);
            for (int i = 0; i < Levels.Length; i++)
            {
                int choice = i;
                RectTransform button = DesktopUi.Panel(content, Levels[i], new Color32(210, 210, 210, 255));
                DesktopUi.TopLeft(button, 10f + i * 100f, 8f, 96f, 24f, scale);
                Label(button, Levels[i], 12, Color.black);
                Button(button, delegate { SetLevel(choice); });
            }
            RectTransform count = DesktopUi.Panel(content, "Mines remaining", Color.black);
            DesktopUi.TopLeft(count, 12f, 40f, 56f, 30f, scale);
            counter = Label(count, "000", 18, Color.red);
            RectTransform clock = DesktopUi.Panel(content, "Timer", Color.black);
            DesktopUi.TopLeft(clock, width - 80f, 40f, 56f, 30f, scale);
            timer = Label(clock, "000", 18, Color.red);
            RectTransform reset = DesktopUi.Panel(content, "New game", Color.yellow);
            DesktopUi.TopLeft(reset, (width - 40f) * 0.5f, 40f, 28f, 28f, scale);
            face = Label(reset, ":)", 17, Color.black);
            Button(reset, NewGame);
            RectTransform viewport = DesktopUi.Panel(content, "Board viewport", Color.clear);
            DesktopUi.TopLeft(viewport, 12f, 84f, Mathf.Max(1f, width - 36f), Mathf.Max(1f, height - 150f), scale);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll = viewport.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            scroll.viewport = viewport;
            scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f * scale;
            board = DesktopUi.Panel(viewport, "Board", Color.clear);
            scroll.content = board;
            board.GetComponent<Image>().raycastTarget = false;
            message = DesktopUi.Label(content, font, "", Mathf.RoundToInt(12f * scale), Color.black, TextAnchor.MiddleCenter);
            DesktopUi.TopLeft((RectTransform)message.transform, 8f, height - 72f, width - 28f, 20f, scale);
        }

        private Text Label(Transform parent, string value, int size, Color color)
        {
            Text label = DesktopUi.Label(parent, font, value, Mathf.RoundToInt(size * scale), color, TextAnchor.MiddleCenter);
            DesktopUi.Stretch((RectTransform)label.transform);
            return label;
        }

        private static void Button(RectTransform rect, Action action)
        {
            UnityEngine.UI.Button button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(delegate { action(); });
        }

        private void SetLevel(int value)
        {
            level = value;
            PlayerPrefs.SetInt(Preference, level);
            NewGame();
        }

        private void NewGame()
        {
            for (int i = board.childCount - 1; i >= 0; i--)
            {
                board.GetChild(i).gameObject.SetActive(false);
                Destroy(board.GetChild(i).gameObject);
            }
            game = new MinesweeperGame(Rows[level], Columns[level], Mines[level], new System.Random());
            seconds = 0;
            timerStarted = false;
            suppress = false;
            cellImages = new Image[game.Cells.Length];
            cellLabels = new Text[game.Cells.Length];
            float areaWidth = Mathf.Max(1f, width - 36f);
            float areaHeight = Mathf.Max(1f, height - 150f);
            float size = Mathf.Max(16f, Mathf.Floor(Mathf.Min(areaWidth / game.Columns, areaHeight / game.Rows)));
            float boardWidth = Mathf.Max(areaWidth, size * game.Columns);
            float boardHeight = Mathf.Max(areaHeight, size * game.Rows);
            DesktopUi.TopLeft(board, 0f, 0f, boardWidth, boardHeight, scale);
            scroll.horizontalNormalizedPosition = 0f;
            scroll.verticalNormalizedPosition = 1f;
            float left = (boardWidth - size * game.Columns) * 0.5f;
            for (int i = 0; i < game.Cells.Length; i++)
            {
                RectTransform cell = DesktopUi.Panel(board, "Cell " + i, new Color32(192, 192, 192, 255));
                DesktopUi.TopLeft(cell, left + i % game.Columns * size, i / game.Columns * size, size, size, scale);
                cellImages[i] = cell.GetComponent<Image>();
                cellLabels[i] = Label(cell, "", Mathf.Max(10, Mathf.RoundToInt(size * 0.65f)), Color.black);
                MinesweeperCell input = cell.gameObject.AddComponent<MinesweeperCell>();
                input.Window = this;
                input.Index = i;
            }
            Paint();
        }

        internal void Press(int index, PointerEventData.InputButton button)
        {
            if (game == null || game.Won || game.Lost) return;
            if (button == PointerEventData.InputButton.Middle || (Input.GetMouseButton(0) && Input.GetMouseButton(1)))
            {
                suppress = true;
                if (game.Chord(index)) Changed();
            }
            else if (button == PointerEventData.InputButton.Right)
            {
                if (game.Toggle(index)) Changed();
            }
            else if (button == PointerEventData.InputButton.Left) suppress = false;
        }

        internal void Release(int index, PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || suppress) return;
            if (eventData.pointerCurrentRaycast.gameObject != eventData.pointerPress) return;
            if (game.Reveal(index)) Changed();
        }

        private void Changed()
        {
            if (game.Placed && !timerStarted && !game.Won && !game.Lost)
            {
                timerStarted = true;
                started = Time.unscaledTime;
            }
            Paint();
        }

        private void Update()
        {
            if (game == null || !timerStarted || game.Won || game.Lost) return;
            int current = Mathf.Min(999, Mathf.FloorToInt(Time.unscaledTime - started));
            if (current == seconds) return;
            seconds = current;
            Paint();
        }

        private void Paint()
        {
            int flags = 0;
            for (int i = 0; i < game.Cells.Length; i++)
            {
                MinesweeperGame.Cell cell = game.Cells[i];
                if (cell.Mark == 1) flags++;
                bool showMine = game.Lost && cell.Mine && cell.Mark != 1;
                cellImages[i].color = i == game.Exploded ? (Color32)Color.red : game.Lost && cell.Mark == 1 && !cell.Mine ? new Color32(255, 144, 144, 255) : cell.Open || showMine ? new Color32(215, 215, 215, 255) : new Color32(192, 192, 192, 255);
                cellLabels[i].text = cell.Open && cell.Adjacent > 0 ? cell.Adjacent.ToString() : showMine ? "●" : cell.Mark == 1 ? "⚑" : cell.Mark == 2 ? "?" : "";
                cellLabels[i].color = cell.Open && cell.Adjacent > 0 ? NumberColors[cell.Adjacent] : cell.Mark == 1 ? Color.red : Color.black;
            }
            int remaining = Mathf.Clamp(game.Mines - flags, -99, 999);
            counter.text = remaining < 0 ? "-" + (-remaining).ToString("00") : remaining.ToString("000");
            timer.text = seconds.ToString("000");
            face.text = game.Won ? "B)" : game.Lost ? ":(" : ":)";
            message.text = game.Won ? "You cleared the field in " + seconds + " seconds." : game.Lost ? "You hit a mine." : "";
        }
    }

    internal sealed class MinesweeperCell : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        internal MinesweeperWindow Window;
        internal int Index;

        public void OnPointerDown(PointerEventData eventData) { Window.Press(Index, eventData.button); }

        public void OnPointerUp(PointerEventData eventData) { Window.Release(Index, eventData); }
    }
}

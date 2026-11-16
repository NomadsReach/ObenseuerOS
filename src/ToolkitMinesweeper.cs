using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitMinesweeper
    {
        private const string Preference = "nomad.obenseuer.os.toolkit.minesweeper.level";
        private static readonly int[] Rows = { 9, 16, 16 };
        private static readonly int[] Columns = { 9, 16, 30 };
        private static readonly int[] Mines = { 10, 40, 99 };
        private static readonly string[] Levels = { "Beginner", "Intermediate", "Expert" };
        private static readonly Color[] NumberColors = { Color.black, Color.blue, new Color32(0, 128, 0, 255), Color.red, new Color32(0, 0, 128, 255), new Color32(128, 0, 0, 255), new Color32(0, 128, 128, 255), Color.black, Color.gray };
        private readonly VisualElement board;
        private readonly Label counter;
        private readonly Label timer;
        private readonly Label message;
        private readonly UnityEngine.UIElements.Button face;
        private readonly UnityEngine.UIElements.Button[] levelButtons = new UnityEngine.UIElements.Button[3];
        private readonly IVisualElementScheduledItem clock;
        private MinesweeperGame game;
        private MinesweeperInput input;
        private VisualElement[] cells;
        private Label[] labels;
        private int level;
        private int seconds;
        private bool timerStarted;
        private float started;

        internal readonly VisualElement Window;

        internal ToolkitMinesweeper()
        {
            Window = new VisualElement();
            Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 320;
            Window.style.top = 32;
            Window.style.width = 720;
            Window.style.height = 560;
            Window.style.paddingLeft = 6;
            Window.style.paddingRight = 6;
            Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217);
            Window.style.display = DisplayStyle.None;
            VisualElement bar = new VisualElement();
            bar.style.height = 36;
            bar.style.flexDirection = FlexDirection.Row;
            Label title = new Label("Minesweeper");
            title.style.flexGrow = 1;
            title.style.color = Color.black;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            bar.Add(title);
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(Close);
            close.text = "×";
            close.style.width = 46;
            close.style.height = 20;
            close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255);
            close.style.color = Color.white;
            bar.Add(close);
            Window.Add(bar);
            VisualElement content = new VisualElement();
            content.style.flexGrow = 1;
            content.style.backgroundColor = (Color)new Color32(192, 192, 192, 255);
            content.style.paddingLeft = 8;
            content.style.paddingRight = 8;
            content.style.paddingTop = 8;
            Window.Add(content);
            VisualElement levels = new VisualElement();
            levels.style.flexDirection = FlexDirection.Row;
            for (int i = 0; i < Levels.Length; i++)
            {
                int choice = i;
                UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(delegate { SetLevel(choice); });
                button.text = Levels[i];
                button.style.width = 100;
                levelButtons[i] = button;
                levels.Add(button);
            }
            content.Add(levels);
            VisualElement controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.style.justifyContent = Justify.SpaceBetween;
            controls.style.alignItems = Align.Center;
            controls.style.height = 42;
            counter = Lcd();
            controls.Add(counter);
            face = new UnityEngine.UIElements.Button(NewGame);
            face.style.width = 30;
            face.style.height = 30;
            face.style.backgroundColor = Color.yellow;
            controls.Add(face);
            timer = Lcd();
            controls.Add(timer);
            content.Add(controls);
            ScrollView viewport = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            viewport.style.flexGrow = 1;
            viewport.style.minHeight = 0;
            content.Add(viewport);
            board = new VisualElement();
            board.style.alignSelf = Align.Center;
            viewport.Add(board);
            message = new Label();
            message.style.height = 24;
            message.style.color = Color.black;
            message.style.unityTextAlign = TextAnchor.MiddleCenter;
            content.Add(message);
            clock = Window.schedule.Execute(UpdateClock).Every(250);
            clock.Pause();
        }

        private static Label Lcd()
        {
            Label label = new Label("000");
            label.style.width = 56;
            label.style.height = 30;
            label.style.fontSize = 18;
            label.style.backgroundColor = Color.black;
            label.style.color = Color.red;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            return label;
        }

        internal void Open()
        {
            if (Window.style.display == DisplayStyle.None)
            {
                level = Mathf.Clamp(PlayerPrefs.GetInt(Preference, 0), 0, 2);
                NewGame();
                Window.style.display = DisplayStyle.Flex;
            }
            Window.BringToFront();
        }

        private void Close()
        {
            clock.Pause();
            Window.style.display = DisplayStyle.None;
        }

        private void SetLevel(int value)
        {
            level = value;
            PlayerPrefs.SetInt(Preference, level);
            NewGame();
        }

        private void NewGame()
        {
            clock.Pause();
            game = new MinesweeperGame(Rows[level], Columns[level], Mines[level], new System.Random());
            input = new MinesweeperInput(game);
            seconds = 0;
            timerStarted = false;
            board.Clear();
            cells = new VisualElement[game.Cells.Length];
            labels = new Label[game.Cells.Length];
            float size = Mathf.Max(16f, Mathf.Floor(Mathf.Min(660f / game.Columns, 390f / game.Rows)));
            for (int row = 0; row < game.Rows; row++)
            {
                VisualElement line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                board.Add(line);
                for (int col = 0; col < game.Columns; col++)
                {
                    int index = row * game.Columns + col;
                    VisualElement cell = new VisualElement();
                    cell.style.width = size;
                    cell.style.height = size;
                    Label label = new Label();
                    label.pickingMode = PickingMode.Ignore;
                    label.style.flexGrow = 1;
                    label.style.fontSize = Mathf.Max(10, Mathf.RoundToInt(size * 0.65f));
                    label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
                    label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    cell.Add(label);
                    cell.RegisterCallback<MouseDownEvent>(delegate(MouseDownEvent e) { Press(index, e.button, e.pressedButtons); });
                    cell.RegisterCallback<MouseUpEvent>(delegate(MouseUpEvent e) { Release(index, e.button, e.pressedButtons); });
                    cells[index] = cell;
                    labels[index] = label;
                    line.Add(cell);
                }
            }
            Paint();
        }

        private void Press(int index, int button, int buttons)
        {
            if (game != null && input.Press(index, button, buttons)) Changed();
        }

        private void Release(int index, int button, int buttons)
        {
            if (game != null && input.Release(index, button, buttons != 0)) Changed();
        }

        private void Changed()
        {
            if (game.Placed && !timerStarted && !game.Won && !game.Lost)
            {
                timerStarted = true;
                started = Time.unscaledTime;
                clock.Resume();
            }
            if (game.Won || game.Lost) clock.Pause();
            Paint();
        }

        private void UpdateClock()
        {
            if (!timerStarted || game.Won || game.Lost) return;
            int current = Mathf.Min(999, Mathf.FloorToInt(Time.unscaledTime - started));
            if (current == seconds) return;
            seconds = current;
            Paint();
        }

        private void Paint()
        {
            for (int i = 0; i < levelButtons.Length; i++)
            {
                UnityEngine.UIElements.Button button = levelButtons[i];
                bool active = i == level;
                button.style.backgroundColor = (Color)new Color32(192, 192, 192, 255);
                button.style.color = Color.black;
                button.style.borderTopWidth = 2;
                button.style.borderLeftWidth = 2;
                button.style.borderBottomWidth = 2;
                button.style.borderRightWidth = 2;
                button.style.borderTopColor = active ? Color.gray : Color.white;
                button.style.borderLeftColor = active ? Color.gray : Color.white;
                button.style.borderBottomColor = active ? Color.white : Color.gray;
                button.style.borderRightColor = active ? Color.white : Color.gray;
            }
            int flags = 0;
            for (int i = 0; i < game.Cells.Length; i++)
            {
                MinesweeperGame.Cell cell = game.Cells[i];
                if (cell.Mark == 1) flags++;
                bool showMine = game.Lost && cell.Mine && cell.Mark != 1;
                bool open = cell.Open || showMine;
                cells[i].style.borderTopWidth = open ? 1 : 2;
                cells[i].style.borderBottomWidth = open ? 1 : 2;
                cells[i].style.borderLeftWidth = open ? 1 : 2;
                cells[i].style.borderRightWidth = open ? 1 : 2;
                cells[i].style.borderTopColor = open ? Color.gray : Color.white;
                cells[i].style.borderLeftColor = open ? Color.gray : Color.white;
                cells[i].style.borderBottomColor = Color.gray;
                cells[i].style.borderRightColor = Color.gray;
                cells[i].style.backgroundColor = i == game.Exploded ? Color.red : game.Lost && cell.Mark == 1 && !cell.Mine ? (Color)new Color32(255, 144, 144, 255) : cell.Open || showMine ? (Color)new Color32(215, 215, 215, 255) : (Color)new Color32(192, 192, 192, 255);
                labels[i].text = cell.Open && cell.Adjacent > 0 ? cell.Adjacent.ToString() : showMine ? "●" : cell.Mark == 1 ? "⚑" : cell.Mark == 2 ? "?" : "";
                labels[i].style.color = cell.Open && cell.Adjacent > 0 ? NumberColors[cell.Adjacent] : cell.Mark == 1 ? Color.red : Color.black;
            }
            int remaining = Mathf.Clamp(game.Mines - flags, -99, 999);
            counter.text = remaining < 0 ? "-" + (-remaining).ToString("00") : remaining.ToString("000");
            timer.text = seconds.ToString("000");
            face.text = game.Won ? "B)" : game.Lost ? ":(" : ":)";
            message.text = game.Won ? "You cleared the field in " + seconds + " seconds." : game.Lost ? "You hit a mine." : "";
        }
    }
}

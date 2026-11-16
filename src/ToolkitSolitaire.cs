using System;
using System.Collections.Generic;
using ObenseuerCore.Ui;
using UnityEngine;
using UnityEngine.UIElements;

namespace ObenseuerOS
{
    internal sealed class ToolkitSolitaire
    {
        private const string Preference = "nomad.obenseuer.os.toolkit.solitaire.draw";
        private static readonly string[] Ranks = { "", "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K" };
        private static readonly string[] Suits = { "♠", "♥", "♦", "♣" };
        private const float CardWidth = 66f;
        private const float CardHeight = 92f;
        private readonly VisualElement table;
        private readonly Label score;
        private readonly Label timer;
        private readonly Label message;
        private readonly UnityEngine.UIElements.Button undo;
        private readonly UnityEngine.UIElements.Button[] modes = new UnityEngine.UIElements.Button[2];
        private readonly Stack<SolitaireGame> history = new Stack<SolitaireGame>();
        private readonly IVisualElementScheduledItem clock;
        private readonly IVisualElementScheduledItem auto;
        private readonly IVisualElementScheduledItem celebrationClock;
        private readonly List<Particle> particles = new List<Particle>();
        private readonly System.Random random = new System.Random();
        private SolitaireGame game;
        private int draw = 1;
        private int seconds;
        private float started;
        private bool timerStarted;
        private bool done;
        private bool autoRunning;
        private Source press;
        private Vector2 pressAt;
        private int pointer = -1;
        private VisualElement ghost;
        private VisualElement celebration;
        private float celebrationStarted;
        private string lastTap = "";
        private float lastTapAt;

        internal readonly VisualElement Window;

        internal ToolkitSolitaire()
        {
            Window = new VisualElement();
            Window.style.position = UnityEngine.UIElements.Position.Absolute;
            Window.style.left = 320;
            Window.style.top = 32;
            Window.style.width = 720;
            Window.style.height = 700;
            Window.style.paddingLeft = 6;
            Window.style.paddingRight = 6;
            Window.style.paddingBottom = 6;
            Window.style.backgroundColor = (Color)new Color32(190, 205, 222, 217);
            Window.style.display = DisplayStyle.None;
            VisualElement titlebar = Row();
            titlebar.style.height = 36;
            Label title = new Label("Solitaire");
            title.style.flexGrow = 1;
            title.style.color = Color.black;
            title.style.unityTextAlign = TextAnchor.MiddleLeft;
            titlebar.Add(title);
            UnityEngine.UIElements.Button close = new UnityEngine.UIElements.Button(Close);
            close.text = "×";
            close.style.width = 46;
            close.style.height = 20;
            close.style.marginTop = 8;
            close.style.backgroundColor = (Color)new Color32(199, 55, 47, 255);
            close.style.color = Color.white;
            titlebar.Add(close);
            Window.Add(titlebar);
            ToolkitUi.MakeDraggable(Window, titlebar);
            VisualElement body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.backgroundColor = (Color)new Color32(192, 192, 192, 255);
            body.style.paddingLeft = 6;
            body.style.paddingRight = 6;
            body.style.paddingTop = 6;
            Window.Add(body);
            VisualElement controls = Row();
            controls.Add(Button("New game", NewGame));
            undo = Button("Undo", Undo);
            controls.Add(undo);
            modes[0] = Button("Draw 1", delegate { SetDraw(1); });
            modes[1] = Button("Draw 3", delegate { SetDraw(3); });
            controls.Add(modes[0]);
            controls.Add(modes[1]);
            score = new Label();
            timer = new Label();
            score.style.color = timer.style.color = Color.black;
            controls.Add(score);
            controls.Add(timer);
            body.Add(controls);
            ScrollView scroll = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scroll.style.flexGrow = 1;
            scroll.style.minHeight = 0;
            body.Add(scroll);
            table = new VisualElement();
            table.style.position = UnityEngine.UIElements.Position.Relative;
            table.style.width = 670;
            table.style.minHeight = 580;
            table.style.backgroundColor = (Color)new Color32(0, 128, 0, 255);
            scroll.Add(table);
            message = new Label();
            message.style.height = 24;
            message.style.color = Color.black;
            body.Add(message);
            Window.RegisterCallback<PointerMoveEvent>(MovePointer);
            Window.RegisterCallback<PointerUpEvent>(EndPointer);
            Window.RegisterCallback<PointerCancelEvent>(CancelPointer);
            clock = Window.schedule.Execute(UpdateClock).Every(250);
            clock.Pause();
            auto = Window.schedule.Execute(AutoStep).Every(150);
            auto.Pause();
            celebrationClock = Window.schedule.Execute(AnimateCelebration).Every(16);
            celebrationClock.Pause();
        }

        internal void Open()
        {
            if (Window.style.display == DisplayStyle.None)
            {
                draw = PlayerPrefs.GetInt(Preference, 1) == 3 ? 3 : 1;
                NewGame();
                Window.style.display = DisplayStyle.Flex;
            }
            Window.BringToFront();
        }

        private void Close()
        {
            clock.Pause();
            auto.Pause();
            StopCelebration();
            CancelDrag();
            Window.style.display = DisplayStyle.None;
        }

        private static VisualElement Row()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            return row;
        }

        private static UnityEngine.UIElements.Button Button(string label, Action action)
        {
            UnityEngine.UIElements.Button button = new UnityEngine.UIElements.Button(action);
            button.text = label;
            return button;
        }

        private void SetDraw(int value)
        {
            draw = value;
            PlayerPrefs.SetInt(Preference, draw);
            NewGame();
        }

        private void NewGame()
        {
            clock.Pause();
            auto.Pause();
            StopCelebration();
            CancelDrag();
            history.Clear();
            seconds = 0;
            timerStarted = false;
            autoRunning = false;
            done = false;
            lastTap = "";
            game = SolitaireGame.Deal(draw, new System.Random());
            message.text = "";
            Paint();
        }

        private void Undo()
        {
            if (done || history.Count == 0) return;
            auto.Pause();
            autoRunning = false;
            game = history.Pop();
            Paint();
        }

        private void Act(Func<bool> action)
        {
            if (done) return;
            SolitaireGame previous = game.Copy();
            if (!action()) return;
            history.Push(previous);
            if (!timerStarted)
            {
                timerStarted = true;
                started = Time.unscaledTime;
                clock.Resume();
            }
            After();
        }

        private void After()
        {
            if (game.Won)
            {
                done = true;
                auto.Pause();
                autoRunning = false;
                clock.Pause();
                message.text = "You won in " + FormatTime(seconds) + " with a score of " + game.Score + ".";
                Celebrate();
            }
            else if (game.CanFinish && !autoRunning)
            {
                autoRunning = true;
                auto.Resume();
            }
            Paint();
        }

        private void AutoStep()
        {
            SolitaireGame previous = game.Copy();
            if (!game.Step()) { auto.Pause(); autoRunning = false; return; }
            history.Push(previous);
            After();
        }

        private void UpdateClock()
        {
            if (!timerStarted || done) return;
            int now = Mathf.FloorToInt(Time.unscaledTime - started);
            if (now == seconds) return;
            seconds = now;
            timer.text = "Time " + FormatTime(seconds);
        }

        private static string FormatTime(int value)
        {
            return (value / 60) + ":" + (value % 60).ToString("00");
        }

        private void Paint()
        {
            table.Clear();
            score.text = "Score " + game.Score;
            timer.text = "Time " + FormatTime(seconds);
            undo.SetEnabled(history.Count > 0 && !done);
            modes[0].style.backgroundColor = draw == 1 ? Color.gray : Color.white;
            modes[1].style.backgroundColor = draw == 3 ? Color.gray : Color.white;
            VisualElement stock = Slot(0, 0, null);
            if (game.Stock.Count > 0) stock.Add(Card(new SolitaireCard(0, 1, false)));
            else if (game.Waste.Count > 0) stock.Add(new Label("↻"));
            stock.RegisterCallback<ClickEvent>(delegate { Act(game.Draw); });
            VisualElement waste = Slot(1, 0, null);
            int shown = Math.Min(game.Waste.Count, draw == 3 ? 3 : 1);
            for (int i = 0; i < shown; i++)
            {
                SolitaireCard card = game.Waste[game.Waste.Count - shown + i];
                VisualElement view = Card(card);
                view.style.left = i * 16;
                if (i == shown - 1) Bind(view, new Source(SolitairePile.Waste, 0, 0));
                waste.Add(view);
            }
            for (int i = 0; i < game.Foundations.Length; i++)
            {
                int pile = i;
                VisualElement slot = Slot(i + 3, 0, new Target(SolitairePile.Foundation, i));
                if (game.Foundations[i].Count == 0) slot.Add(new Label(Suits[i]));
                else
                {
                    int at = game.Foundations[i].Count - 1;
                    VisualElement view = Card(game.Foundations[i][at]);
                    Bind(view, new Source(SolitairePile.Foundation, pile, at));
                    slot.Add(view);
                }
            }
            float lowest = 0;
            for (int i = 0; i < game.Tableau.Length; i++)
            {
                int pile = i;
                VisualElement col = Slot(i, 120, new Target(SolitairePile.Tableau, i));
                col.style.height = 440;
                float y = 0;
                for (int j = 0; j < game.Tableau[i].Count; j++)
                {
                    SolitaireCard card = game.Tableau[i][j];
                    VisualElement view = Card(card);
                    view.style.top = y;
                    if (card.Up) Bind(view, new Source(SolitairePile.Tableau, pile, j));
                    col.Add(view);
                    y += card.Up ? 22 : 9;
                }
                col.style.height = Mathf.Max(440, y + CardHeight);
                lowest = Mathf.Max(lowest, y + CardHeight + 130);
            }
            table.style.height = Mathf.Max(580, lowest);
        }

        private VisualElement Slot(int column, float top, Target target)
        {
            VisualElement slot = new VisualElement();
            slot.userData = target;
            slot.style.position = UnityEngine.UIElements.Position.Absolute;
            slot.style.left = 16 + column * 91;
            slot.style.top = top + 12;
            slot.style.width = CardWidth;
            slot.style.height = CardHeight;
            slot.style.borderTopWidth = slot.style.borderBottomWidth = 1;
            slot.style.borderLeftWidth = slot.style.borderRightWidth = 1;
            slot.style.borderTopColor = slot.style.borderBottomColor = Color.gray;
            slot.style.borderLeftColor = slot.style.borderRightColor = Color.gray;
            table.Add(slot);
            return slot;
        }

        private static VisualElement Card(SolitaireCard card)
        {
            VisualElement view = new VisualElement();
            view.style.position = UnityEngine.UIElements.Position.Absolute;
            view.style.width = CardWidth;
            view.style.height = CardHeight;
            view.style.backgroundColor = card.Up ? Color.white : (Color)new Color32(16, 45, 136, 255);
            view.style.borderTopWidth = view.style.borderBottomWidth = 1;
            view.style.borderLeftWidth = view.style.borderRightWidth = 1;
            view.style.borderTopColor = view.style.borderBottomColor = Color.black;
            view.style.borderLeftColor = view.style.borderRightColor = Color.black;
            if (card.Up)
            {
                Label label = new Label(Ranks[card.Rank] + Suits[card.Suit] + "\n\n" + Suits[card.Suit]);
                label.pickingMode = PickingMode.Ignore;
                label.style.color = card.Red ? Color.red : Color.black;
                label.style.fontSize = 15;
                label.style.whiteSpace = WhiteSpace.Normal;
                view.Add(label);
            }
            return view;
        }

        private void Bind(VisualElement view, Source source)
        {
            view.RegisterCallback<PointerDownEvent>(delegate(PointerDownEvent e)
            {
                if (e.button != 0 || done) return;
                if (autoRunning) { auto.Pause(); autoRunning = false; }
                press = source;
                pressAt = new Vector2(e.position.x, e.position.y);
                pointer = e.pointerId;
                Window.CapturePointer(pointer);
                e.StopPropagation();
            });
        }

        private void MovePointer(PointerMoveEvent e)
        {
            if (press == null || e.pointerId != pointer) return;
            Vector2 current = new Vector2(e.position.x, e.position.y);
            if (ghost == null && (current - pressAt).sqrMagnitude > 16)
            {
                ghost = new VisualElement();
                ghost.pickingMode = PickingMode.Ignore;
                ghost.style.position = UnityEngine.UIElements.Position.Absolute;
                List<SolitaireCard> cards = press.Kind == SolitairePile.Waste ? game.Waste : press.Kind == SolitairePile.Foundation ? game.Foundations[press.Pile] : game.Tableau[press.Pile];
                int start = press.Kind == SolitairePile.Tableau ? press.At : cards.Count - 1;
                ghost.style.width = CardWidth;
                ghost.style.height = CardHeight + (cards.Count - start - 1) * 22;
                for (int i = start; i < cards.Count; i++)
                {
                    VisualElement card = Card(cards[i]);
                    card.pickingMode = PickingMode.Ignore;
                    card.style.top = (i - start) * 22;
                    ghost.Add(card);
                }
                Window.Add(ghost);
            }
            if (ghost == null) return;
            Vector2 local = Window.WorldToLocal(current);
            ghost.style.left = local.x - CardWidth / 2;
            ghost.style.top = local.y - CardHeight / 2;
        }

        private void EndPointer(PointerUpEvent e)
        {
            if (press == null || e.pointerId != pointer) return;
            Source source = press;
            bool dragging = ghost != null;
            CancelDrag();
            if (dragging)
            {
                VisualElement hit = Window.panel == null ? null : Window.panel.Pick(new Vector2(e.position.x, e.position.y));
                while (hit != null && !(hit.userData is Target)) hit = hit.parent;
                Target target = hit == null ? null : (Target)hit.userData;
                if (target != null) Act(delegate { return game.Move(source.Kind, source.Pile, source.At, target.Kind, target.Pile); });
                ResumeAuto();
                return;
            }
            string key = source.Kind + ":" + source.Pile + ":" + source.At;
            float now = Time.unscaledTime;
            if (key == lastTap && now - lastTapAt < 0.4f)
            {
                lastTap = "";
                Act(delegate { return game.ToFoundation(source.Kind, source.Pile, source.At); });
            }
            else { lastTap = key; lastTapAt = now; }
            ResumeAuto();
        }

        private void CancelPointer(PointerCancelEvent e)
        {
            if (e.pointerId == pointer) { CancelDrag(); ResumeAuto(); }
        }

        private void ResumeAuto()
        {
            if (done || autoRunning || !game.CanFinish) return;
            autoRunning = true;
            auto.Resume();
        }

        private void Celebrate()
        {
            StopCelebration();
            celebration = new VisualElement();
            celebration.pickingMode = PickingMode.Ignore;
            celebration.style.position = UnityEngine.UIElements.Position.Absolute;
            celebration.style.left = 0;
            celebration.style.top = 0;
            celebration.style.right = 0;
            celebration.style.bottom = 0;
            Window.Add(celebration);
            for (int pile = 0; pile < game.Foundations.Length; pile++)
            {
                foreach (SolitaireCard card in game.Foundations[pile])
                {
                    VisualElement view = Card(card);
                    view.pickingMode = PickingMode.Ignore;
                    celebration.Add(view);
                    particles.Add(new Particle(view, 22 + (pile + 3) * 91, 90, (float)(random.NextDouble() * 6 - 3), (float)(-2 - random.NextDouble() * 10)));
                }
            }
            celebrationStarted = Time.unscaledTime;
            celebrationClock.Resume();
        }

        private void AnimateCelebration()
        {
            if (celebration == null || Time.unscaledTime - celebrationStarted > 9f) { StopCelebration(); return; }
            float step = Mathf.Min(3f, Time.unscaledDeltaTime * 60f);
            float width = Mathf.Max(CardWidth, Window.resolvedStyle.width);
            float height = Mathf.Max(CardHeight, Window.resolvedStyle.height);
            foreach (Particle item in particles)
            {
                item.Vy += 0.6f * step;
                item.X += item.Vx * step;
                item.Y += item.Vy * step;
                if (item.X < 0) { item.X = 0; item.Vx = -item.Vx; }
                else if (item.X > width - CardWidth) { item.X = width - CardWidth; item.Vx = -item.Vx; }
                if (item.Y > height - CardHeight)
                {
                    item.Y = height - CardHeight;
                    item.Vy = -item.Vy * 0.7f;
                    if (Mathf.Abs(item.Vy) < 4) item.Vy = (float)(-8 - random.NextDouble() * 8);
                }
                item.View.style.left = item.X;
                item.View.style.top = item.Y;
            }
        }

        private void StopCelebration()
        {
            celebrationClock.Pause();
            if (celebration != null) celebration.RemoveFromHierarchy();
            celebration = null;
            particles.Clear();
        }

        private void CancelDrag()
        {
            if (pointer >= 0 && Window.HasPointerCapture(pointer)) Window.ReleasePointer(pointer);
            pointer = -1;
            press = null;
            if (ghost != null) ghost.RemoveFromHierarchy();
            ghost = null;
        }

        private sealed class Source
        {
            internal readonly SolitairePile Kind;
            internal readonly int Pile;
            internal readonly int At;
            internal Source(SolitairePile kind, int pile, int at) { Kind = kind; Pile = pile; At = at; }
        }

        private sealed class Target
        {
            internal readonly SolitairePile Kind;
            internal readonly int Pile;
            internal Target(SolitairePile kind, int pile) { Kind = kind; Pile = pile; }
        }

        private sealed class Particle
        {
            internal readonly VisualElement View;
            internal float X;
            internal float Y;
            internal float Vx;
            internal float Vy;
            internal Particle(VisualElement view, float x, float y, float vx, float vy)
            {
                View = view;
                X = x;
                Y = y;
                Vx = vx;
                Vy = vy;
            }
        }
    }
}

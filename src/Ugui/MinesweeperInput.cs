namespace ObenseuerOS
{
    internal sealed class MinesweeperInput
    {
        private readonly MinesweeperGame game;
        private int pressed = -1;
        private bool suppress;

        internal MinesweeperInput(MinesweeperGame game)
        {
            this.game = game;
        }

        internal bool Press(int index, int button, int buttons)
        {
            if (game.Won || game.Lost) return false;
            if (button == 2 || (buttons & 3) == 3)
            {
                suppress = true;
                return game.Chord(index);
            }
            if (button == 1) return game.Toggle(index);
            if (button == 0 && buttons == 1)
            {
                suppress = false;
                pressed = index;
            }
            return false;
        }

        internal bool Release(int index, int button, bool buttonsDown)
        {
            if (buttonsDown) return false;
            bool changed = button == 0 && !suppress && pressed == index && game.Reveal(index);
            pressed = -1;
            suppress = false;
            return changed;
        }
    }
}

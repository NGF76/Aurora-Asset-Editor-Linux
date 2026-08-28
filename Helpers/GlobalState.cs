using System;
using AuroraAssetEditorLinux.Models;

namespace AuroraAssetEditorLinux.Helpers
{
    public static class GlobalState
    {
        private static Game _currentGame = new Game()
        {
            IsGameSelected = false
        };

        public static event Action? GameChanged;

        public static Game CurrentGame
        {
            get => _currentGame;
            set
            {
                if (_currentGame != value)
                {
                    _currentGame = value;
                    GameChanged?.Invoke();
                }
            }
        }
    }
}

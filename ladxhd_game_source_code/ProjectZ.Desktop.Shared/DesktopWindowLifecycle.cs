using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Base;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Things;

namespace ProjectZ
{
    public interface IDesktopWindowAdapter
    {
        void InitializeNativeWindow(Game game);
        Rectangle GetDesktopBounds();
        bool TryCenterWindow(Game game);
    }

    public sealed class DefaultDesktopWindowAdapter : IDesktopWindowAdapter
    {
        public void InitializeNativeWindow(Game game) { }
        public Rectangle GetDesktopBounds()
        {
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Rectangle(0, 0, mode.Width, mode.Height);
        }
        public bool TryCenterWindow(Game game) => false;
    }

    public sealed class DesktopWindowLifecycle : IPlatformWindowLifecycle
    {
        private readonly IDesktopWindowAdapter _adapter;
        private bool _initialized;
        private bool _initialFullscreenApplied;
        private bool _synchronizingBackBuffer;
        private int _lastWindowWidth;
        private int _lastWindowHeight;
        private bool _windowSizeRestored;
        private bool _windowPositionRestored;
        private bool _hasWindowedPosition;
        private bool _restoreWindowPosition;
        private Point _windowedPosition;

        public DesktopWindowLifecycle(IDesktopWindowAdapter adapter)
        {
            _adapter = adapter;
        }

        public void Initialize(Game1 game)
        {
            if (_initialized)
                return;

            _initialized = true;
            _lastWindowWidth = Game1.Graphics.PreferredBackBufferWidth;
            _lastWindowHeight = Game1.Graphics.PreferredBackBufferHeight;
            _adapter.InitializeNativeWindow(game);
            game.Window.AllowUserResizing = true;
            game.Window.AllowAltF4 = true;
            game.Window.ClientSizeChanged += (_, _) => OnClientSizeChanged(game);
            game.Window.KeyDown += (_, e) => HandleKeyDown(game, e.Key);
            game.Window.TextInput += (_, e) => InputHandler.ReceiveTextInput(e.Character);
        }

        public void ApplySavedSettings(Game1 game)
        {
            _windowSizeRestored = true;
            var width = GameSettings.WindowWidth;
            var height = GameSettings.WindowHeight;
            if (width > 0 && height > 0)
            {
                var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
                width = MathHelper.Clamp(width, Values.MinWidth, displayMode.Width);
                height = MathHelper.Clamp(height, Values.MinHeight, displayMode.Height);
                _lastWindowWidth = width;
                _lastWindowHeight = height;
                GameSettings.WindowWidth = width;
                GameSettings.WindowHeight = height;

                if (GameSettings.ScreenMode == 0)
                {
                    Game1.Graphics.PreferredBackBufferWidth = width;
                    Game1.Graphics.PreferredBackBufferHeight = height;
                    Game1.Graphics.ApplyChanges();
                    game.RefreshPresentationSize();
                }
            }

            _windowPositionRestored = true;
            if (GameSettings.ScreenMode != 0)
                return;

            width = Game1.Graphics.PreferredBackBufferWidth;
            height = Game1.Graphics.PreferredBackBufferHeight;
            if (width <= 0 || height <= 0)
                return;

            if (GameSettings.WindowPosX != Values.UnsetWindowPosition &&
                GameSettings.WindowPosY != Values.UnsetWindowPosition)
            {
                var position = new Point(GameSettings.WindowPosX, GameSettings.WindowPosY);
                if (IsPlacementReachable(position, width, height))
                {
                    game.Window.Position = position;
                    return;
                }
            }

            CenterWindow(game, width, height);
        }

        public void Update(Game1 game)
        {
            RestoreWindowPosition(game);
            SynchronizeBackBufferToClientBounds(game);
            TrackWindowPosition(game);
        }

        public void ApplyInitialFullscreen(Game1 game)
        {
            if (_initialFullscreenApplied)
                return;

            _initialFullscreenApplied = true;
            if (GameSettings.ScreenMode > 0)
                ToggleFullscreen(game);
        }

        public void PreparePresentationReset(Game1 game)
        {
            if ((Game1.Graphics.IsFullScreen && Game1.Graphics.HardwareModeSwitch) ||
                Game1.GraphicsCapabilities.NeedsPreferredBackBufferSynchronization ||
                Game1.Graphics.GraphicsDevice == null)
                return;

            var presentation = Game1.Graphics.GraphicsDevice.PresentationParameters;
            if (presentation.BackBufferWidth <= 0 || presentation.BackBufferHeight <= 0)
                return;

            Game1.Graphics.PreferredBackBufferWidth = presentation.BackBufferWidth;
            Game1.Graphics.PreferredBackBufferHeight = presentation.BackBufferHeight;
        }

        public void ToggleFullscreen(Game1 game)
        {
            RememberWindowPositionForFullscreenTransition(game);

            if (GameSettings.ScreenMode > 0)
            {
                bool wasWindowed = !Game1.FullScreen;
                Game1.FullScreen = true;
                if (wasWindowed)
                {
                    var client = game.Window.ClientBounds;
                    _lastWindowWidth = client.Width > 0 ? client.Width : Game1.Graphics.PreferredBackBufferWidth;
                    _lastWindowHeight = client.Height > 0 ? client.Height : Game1.Graphics.PreferredBackBufferHeight;
                }

                if (GameSettings.ScreenMode == 1)
                {
                    Game1.Graphics.HardwareModeSwitch = false;
                    Game1.Graphics.IsFullScreen = true;
                    Game1.Graphics.ApplyChanges();
                }
                else if (GameSettings.ScreenMode == 2)
                {
                    var mode = Game1.Graphics.GraphicsDevice.Adapter.CurrentDisplayMode;
                    Game1.Graphics.PreferredBackBufferWidth = mode.Width;
                    Game1.Graphics.PreferredBackBufferHeight = mode.Height;
                    Game1.Graphics.HardwareModeSwitch = true;
                    Game1.Graphics.IsFullScreen = true;
                    Game1.Graphics.ApplyChanges();
                }
            }
            else
            {
                Game1.FullScreen = false;
                Game1.Graphics.PreferredBackBufferWidth = _lastWindowWidth > 0 ? _lastWindowWidth : Values.MinWidth * 3;
                Game1.Graphics.PreferredBackBufferHeight = _lastWindowHeight > 0 ? _lastWindowHeight : Values.MinHeight * 3;
                Game1.Graphics.IsFullScreen = false;
                Game1.Graphics.ApplyChanges();
            }

            Game1.GameManager?.UpdateRenderTargets();
        }

        private void HandleKeyDown(Game1 game, Keys key)
        {
            if (key == Keys.F11)
            {
                HandleFullscreenHotkey(game);
                return;
            }

            var state = Keyboard.GetState();
            bool altDown = state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
            if (!altDown)
                return;

            if (key == Keys.F4)
            {
                InputHandler.ResetInputState();
                game.Exit();
            }
            else if (key == Keys.Enter)
            {
                HandleFullscreenHotkey(game);
            }
        }

        private void HandleFullscreenHotkey(Game1 game)
        {
            Game1.ToggleFullscreenFromHotkey();
        }

        private void OnClientSizeChanged(Game1 game)
        {
            if (GameSettings.ScreenMode == 0)
            {
                var bounds = game.Window.ClientBounds;
                if (bounds.Width < Values.MinWidth || bounds.Height < Values.MinHeight)
                {
                    Game1.Graphics.PreferredBackBufferWidth = Math.Max(bounds.Width, Values.MinWidth);
                    Game1.Graphics.PreferredBackBufferHeight = Math.Max(bounds.Height, Values.MinHeight);
                    Game1.Graphics.ApplyChanges();
                }
            }

            game.RefreshPresentationSize();
            if (_windowSizeRestored && GameSettings.ScreenMode == 0 && !Game1.FullScreen)
                RememberWindowedSize(Game1.WindowWidth, Game1.WindowHeight);
        }

        private void RememberWindowPositionForFullscreenTransition(Game1 game)
        {
            if (GameSettings.ScreenMode > 0 && !Game1.FullScreen)
            {
                _windowedPosition = game.Window.Position;
                _hasWindowedPosition = true;
            }
            else if (GameSettings.ScreenMode == 0 && Game1.FullScreen && _hasWindowedPosition)
            {
                _restoreWindowPosition = true;
            }
        }

        private void RestoreWindowPosition(Game1 game)
        {
            if (!_restoreWindowPosition)
                return;

            _restoreWindowPosition = false;
            if (GameSettings.ScreenMode == 0)
                game.Window.Position = _windowedPosition;
        }

        private void SynchronizeBackBufferToClientBounds(Game1 game)
        {
            if (_synchronizingBackBuffer ||
                !Game1.GraphicsCapabilities.NeedsPreferredBackBufferSynchronization ||
                (Game1.Graphics.IsFullScreen && Game1.Graphics.HardwareModeSwitch))
                return;

            var bounds = game.Window.ClientBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return;

            var width = Math.Max(bounds.Width, Values.MinWidth);
            var height = Math.Max(bounds.Height, Values.MinHeight);
            if (Game1.Graphics.PreferredBackBufferWidth == width && Game1.Graphics.PreferredBackBufferHeight == height)
                return;

            _synchronizingBackBuffer = true;
            try
            {
                Game1.Graphics.PreferredBackBufferWidth = width;
                Game1.Graphics.PreferredBackBufferHeight = height;
                Game1.Graphics.ApplyChanges();
            }
            finally
            {
                _synchronizingBackBuffer = false;
            }
        }

        private void TrackWindowPosition(Game1 game)
        {
            if (!_windowPositionRestored || GameSettings.ScreenMode != 0 || Game1.FullScreen)
                return;

            var position = game.Window.Position;
            if (position.X <= -30000 || position.Y <= -30000)
                return;

            GameSettings.WindowPosX = position.X;
            GameSettings.WindowPosY = position.Y;
        }

        private void RememberWindowedSize(int width, int height)
        {
            if (width < Values.MinWidth || height < Values.MinHeight)
                return;

            _lastWindowWidth = width;
            _lastWindowHeight = height;
            GameSettings.WindowWidth = width;
            GameSettings.WindowHeight = height;
        }

        private bool IsPlacementReachable(Point position, int width, int height)
        {
            var visible = Rectangle.Intersect(_adapter.GetDesktopBounds(), new Rectangle(position.X, position.Y, width, height));
            return visible.Width >= 120 && visible.Height >= 40;
        }

        private void CenterWindow(Game1 game, int width, int height)
        {
            if (_adapter.TryCenterWindow(game))
                return;

            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            game.Window.Position = new Point(Math.Max(0, (mode.Width - width) / 2), Math.Max(0, (mode.Height - height) / 2));
        }
    }

    public sealed class DesktopPlatformInput : IPlatformInput
    {
        public bool HasTouchInput => false;
        public IReadOnlyList<PlatformTouch> Touches => [];
        public Vector2 RightStick => Vector2.Zero;
        public bool IsButtonDown(CButtons button) => false;
        public bool WasButtonPressed(CButtons button) => false;
        public bool WasButtonReleased(CButtons button) => false;
        public bool ConsumeSelectPressed() => false;
        public void BeginFrame() { }
        public KeyboardState FilterKeyboardState(KeyboardState state) => DesktopKeyboardInput.FilterFullscreenChord(state);
        public bool TryGetRawMouseClientOffset(Game game, out Point offset)
        {
            offset = Point.Zero;
            return false;
        }
    }

    public static class DesktopKeyboardInput
    {
        public static KeyboardState FilterFullscreenChord(KeyboardState state)
        {
            bool altDown = state.IsKeyDown(Keys.LeftAlt) || state.IsKeyDown(Keys.RightAlt);
            if (!altDown || state.IsKeyUp(Keys.Enter))
                return state;

            var down = state.GetPressedKeys();
            var filtered = new List<Keys>(down.Length);
            for (int i = 0; i < down.Length; i++)
                if (down[i] != Keys.Enter)
                    filtered.Add(down[i]);
            return new KeyboardState(filtered.ToArray(), state.CapsLock, state.NumLock);
        }
    }
}

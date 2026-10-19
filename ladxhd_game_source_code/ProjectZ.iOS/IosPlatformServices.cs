using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using ProjectZ.InGame.Controls;

namespace ProjectZ.iOS
{
    internal sealed class IosUserDataPaths : IUserDataPaths
    {
        public IosUserDataPaths(string root) { UserDataRoot = root; }
        public bool DefaultMapThreadLoad => false;
        public bool ShouldCreateModsDirs => true;
        public string UserDataRoot { get; }
        public string ModsRoot => Path.Combine(UserDataRoot, "Mods");
        public string InternalModsRoot => null;
        public string SaveDirectory => Path.Combine(UserDataRoot, "SaveFiles");
        public string SettingsFilePath => Path.Combine(UserDataRoot, "settings");
        public string AdvancedFilePath => Path.Combine(UserDataRoot, "advanced");
        public string AchievementsFilePath => Path.Combine(UserDataRoot, "achievements");
    }

    internal sealed class IosPlatformInput : IPlatformInput
    {
        private readonly List<PlatformTouch> _touches = [];

        public bool HasTouchInput => true;
        public IReadOnlyList<PlatformTouch> Touches => _touches;
        public Vector2 RightStick => Vector2.Zero;
        public bool IsButtonDown(CButtons b) => false;
        public bool WasButtonPressed(CButtons b) => false;
        public bool WasButtonReleased(CButtons b) => false;
        public bool ConsumeSelectPressed() => false;

        public void BeginFrame()
        {
            _touches.Clear();
            foreach (var t in TouchPanel.GetState())
            {
                var s = t.State switch
                {
                    TouchLocationState.Pressed => PlatformTouchState.Pressed,
                    TouchLocationState.Moved => PlatformTouchState.Moved,
                    TouchLocationState.Released => PlatformTouchState.Released,
                    _ => PlatformTouchState.Invalid
                };
                _touches.Add(new PlatformTouch(t.Id, t.Position, s));
            }
        }

        public bool TryGetRawMouseClientOffset(Game game, out Point offset)
        {
            offset = Point.Zero;
            return false;
        }
    }

    internal sealed class IosPlatformWindow : IPlatformWindow
    {
        public bool SupportsFullscreenConfiguration => false;
        public bool SupportsInactiveWindowInput => false;
        public bool ForceFullscreen => true;
        public bool VerticalFlipBlur => true;
        public void Exit(Game game) { }
    }
}

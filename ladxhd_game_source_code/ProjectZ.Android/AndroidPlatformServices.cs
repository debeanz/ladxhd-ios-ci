using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.Provider;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;
using ProjectZ.InGame.Controls;
using ProjectZ.InGame.Things;
using AndroidEnv = Android.OS.Environment;
using AndroidNet = Android.Net;

namespace ProjectZ.Android
{
    internal sealed class AndroidPlatformFileSystem : IPlatformFileSystem
    {
        private readonly AssetManager _assets;

        public AndroidPlatformFileSystem(AssetManager assets)
        {
            _assets = assets;
        }

        public bool PackagedAssetExists(string path)
        {
            try
            {
                using var stream = OpenPackagedAsset(path);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool PackagedAssetDirectoryExists(string path)
        {
            try
            {
                using var stream = _assets.Open(path.Trim('/', '\\'));
                return false;
            }
            catch
            {
                // AssetManager returns an empty array for missing directories, so only a
                // directory containing packaged entries should select the internal mod root.
                return _assets.List(path.Trim('/', '\\')) is { Length: > 0 };
            }
        }

        public Stream OpenPackagedAsset(string path) => TitleContainer.OpenStream(path.TrimStart('/', '\\'));

        public string[] ListPackagedAssets(string directory)
        {
            try
            {
                return _assets.List(directory.Trim('/', '\\')) ?? [];
            }
            catch
            {
                return [];
            }
        }
    }

    internal sealed class AndroidUserDataPaths : IUserDataPaths
    {
        public bool DefaultMapThreadLoad => false;
        public bool ShouldCreateModsDirs => Values.ResolvedMods == Values.PathMods;
        public AndroidUserDataPaths(string userDataRoot)
        {
            UserDataRoot = userDataRoot;
        }
        public string UserDataRoot { get; }
        public string ModsRoot => Path.Combine(UserDataRoot, "Mods");
        public string InternalModsRoot => "Mods";
        public string SaveDirectory => Path.Combine(UserDataRoot, "SaveFiles");
        public string SettingsFilePath => Path.Combine(UserDataRoot, "settings");
        public string AdvancedFilePath => Path.Combine(UserDataRoot, "advanced");
        public string AchievementsFilePath => Path.Combine(UserDataRoot, "achievements");
    }

    internal sealed class AndroidSharedSaveService : ISharedSaveService
    {
        private const string ExternalRootName = "LADXHD";
        private const string ExternalSavesSubfolder = "SaveFiles";

        public bool IsSupported => true;

        public bool HasAccess => OperatingSystem.IsAndroidVersionAtLeast(30)
            ? AndroidEnv.IsExternalStorageManager
            : Application.Context.CheckSelfPermission(global::Android.Manifest.Permission.WriteExternalStorage) == Permission.Granted;

        public string SharedRootDirectory => Path.Combine(AndroidEnv.ExternalStorageDirectory.AbsolutePath, ExternalRootName);

        public string SharedSaveDirectory => Path.Combine(AndroidEnv.ExternalStorageDirectory.AbsolutePath, ExternalRootName, ExternalSavesSubfolder);

        public void RequestAccess()
        {
            try
            {
                Intent intent;
                if (OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    // Deep-link to this app's "All files access" toggle.
                    intent = new Intent(Settings.ActionManageAppAllFilesAccessPermission);
                    intent.SetData(AndroidNet.Uri.Parse("package:" + Application.Context.PackageName));
                }
                else
                {
                    intent = new Intent(Settings.ActionApplicationDetailsSettings);
                    intent.SetData(AndroidNet.Uri.Parse("package:" + Application.Context.PackageName));
                }

                intent.AddFlags(ActivityFlags.NewTask);
                Application.Context.StartActivity(intent);
            }
            catch { }
        }

        public void CopyFile(string sourcePath, string destinationPath)
        {
            // Keep the previous shared save intact until the Java I/O copy completes.
            // Java streams avoid the Samsung/SELinux issue with .NET file APIs here.
            var temporaryPath = destinationPath + ".tmp";
            JavaStreamCopy(sourcePath, temporaryPath);

            var destination = new Java.IO.File(destinationPath);
            if (destination.Exists() && !destination.Delete())
                throw new IOException($"Could not replace shared save '{destinationPath}'.");

            var temporary = new Java.IO.File(temporaryPath);
            if (!temporary.RenameTo(destination))
                throw new IOException($"Could not finalize shared save '{destinationPath}'.");
        }

        private static void JavaStreamCopy(string sourcePath, string destinationPath)
        {
            using var input = new Java.IO.FileInputStream(sourcePath);
            using var output = new Java.IO.FileOutputStream(destinationPath);
            var buffer = new byte[8192];
            int read;
            while ((read = input.Read(buffer)) > 0)
                output.Write(buffer, 0, read);
        }

        public bool FileExists(string path) => File.Exists(path);
        public void DeleteFile(string path) => File.Delete(path);
        public void EnsureDirectory(string path) => Directory.CreateDirectory(path);
        public DateTime GetLastWriteTimeUtc(string path) => File.GetLastWriteTimeUtc(path);
    }

    internal sealed class AndroidPlatformInput : IPlatformInput
    {
        // Button state used to derive held, pressed, and released edges each frame.
        private volatile int _down;
        private volatile int _last;

        // Legacy one-shot Select latch, kept separate from held button state.
        private volatile int _selectPressed;

        // Right-stick state supplied by Android motion events.
        private volatile float _rightStickX;
        private volatile float _rightStickY;

        // Touch state captured from MonoGame at the start of each frame.
        private readonly List<PlatformTouch> _touches = [];

        public bool HasTouchInput => true;
        public IReadOnlyList<PlatformTouch> Touches => _touches;
        public Vector2 RightStick => new Vector2(_rightStickX, _rightStickY);
        public bool IsButtonDown(CButtons button) => (_down & (int)button) != 0;
        public bool WasButtonPressed(CButtons button) => (_down & (int)button) != 0 && (_last & (int)button) == 0;
        public bool WasButtonReleased(CButtons button) => (_down & (int)button) == 0 && (_last & (int)button) != 0;
        public bool ConsumeSelectPressed() => Interlocked.Exchange(ref _selectPressed, 0) != 0;
        public void BeginFrame()
        {
            _last = _down;
            _touches.Clear();
            foreach (var touch in TouchPanel.GetState())
            {
                var state = touch.State switch
                {
                    TouchLocationState.Pressed => PlatformTouchState.Pressed,
                    TouchLocationState.Moved => PlatformTouchState.Moved,
                    TouchLocationState.Released => PlatformTouchState.Released,
                    _ => PlatformTouchState.Invalid
                };
                _touches.Add(new PlatformTouch(touch.Id, touch.Position, state));
            }
        }

        public bool TryGetRawMouseClientOffset(Game game, out Point offset)
        {
            offset = Point.Zero;
            return false;
        }

        public void SetButton(CButtons button, bool down)
        {
            if (down)
                Interlocked.Or(ref _down, (int)button);
            else
                Interlocked.And(ref _down, ~(int)button);
        }

        public void SetRightStick(float x, float y)
        {
            _rightStickX = x;
            _rightStickY = y;
        }

        public void SetSelectPressed() => Interlocked.Exchange(ref _selectPressed, 1);
    }

    internal sealed class AndroidPlatformWindow : IPlatformWindow
    {
        public bool SupportsFullscreenConfiguration => false;
        public bool SupportsInactiveWindowInput => false;
        public bool ForceFullscreen => true;
        public bool VerticalFlipBlur => true;
        public bool TryCenterWindow(Game game) => false;
        public Rectangle GetDesktopBounds()
        {
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Rectangle(0, 0, mode.Width, mode.Height);
        }
        // MainActivity is reused, so Game.Exit would leave a disposed game
        // behind for the next launcher intent. The explicit in-game exit must end it.
        public void Exit(Game game) => global::Android.OS.Process.KillProcess(global::Android.OS.Process.MyPid());
    }
}

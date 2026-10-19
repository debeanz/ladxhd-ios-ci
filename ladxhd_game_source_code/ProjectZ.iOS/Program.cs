using System;
using System.IO;
using Foundation;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Content.ContentReaders;
using UIKit;

namespace ProjectZ.iOS
{
    [Register("AppDelegate")]
    internal class Program : UIApplicationDelegate
    {
        private static Game1 _game;

        internal static void RunGame()
        {
            BitmapFontContentReader.Register();

            var root = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            Directory.CreateDirectory(Path.Combine(root, "SaveFiles"));
            foreach (var dir in new[] { "Animations", "Dungeon", "Graphics", "Music", "Languages", "LAHDMods", "Maps", "SoundEffects" })
                Directory.CreateDirectory(Path.Combine(root, "Mods", dir));

            var bounds = UIScreen.MainScreen.NativeBounds;
            var w = (int)Math.Max(bounds.Width, bounds.Height);
            var h = (int)Math.Min(bounds.Width, bounds.Height);

            _game = new Game1(editorMode: false, loadSave: false, loadSlot: 0);

            _game.Services.AddService(typeof(IPlatformDisplayConfiguration), new PlatformDisplayConfiguration(w, h));
            _game.Services.AddService(typeof(IPlatformFileSystem), new LocalPlatformFileSystem(NSBundle.MainBundle.BundlePath));
            _game.Services.AddService(typeof(IUserDataPaths), new IosUserDataPaths(root));
            _game.Services.AddService(typeof(ISharedSaveService), new UnavailableSharedSaveService());
            _game.Services.AddService(typeof(IPlatformInput), new IosPlatformInput());
            _game.Services.AddService(typeof(ITextInputService), new NullTextInputService());
            _game.Services.AddService(typeof(IPlatformWindow), new IosPlatformWindow());
            _game.Services.AddService(typeof(IGraphicsCapabilities), new GraphicsCapabilities(
                usePresentationParametersForSize: true,
                canCreateGraphicsResourcesOnWorkerThread: false,
                supportsBlendFunctionMax: false,
                useAnisotropicFiltering: false));
            _game.Services.AddService(typeof(IPlatformPresentation), new PlatformPresentation(240, true, true, 1));
            _game.Services.AddService(typeof(IFileDialogService), new UnavailableFileDialogService());
            _game.Services.AddService(typeof(IPhotoPrintService), new UnavailablePhotoPrintService());

            _game.Run();
        }

        public override void FinishedLaunching(UIApplication app) => RunGame();

        private static void Main(string[] args) => UIApplication.Main(args, null, typeof(Program));
    }
}

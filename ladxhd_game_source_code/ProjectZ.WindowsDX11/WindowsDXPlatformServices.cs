using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Xna.Framework;
using Point = System.Drawing.Point;
using Rectangle = System.Drawing.Rectangle;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;

namespace ProjectZ
{
    internal sealed class WindowsDxPlatformWindow : IPlatformWindow, IDesktopWindowAdapter
    {
        private Form _form;

        public bool SupportsFullscreenConfiguration => true;
        public bool SupportsInactiveWindowInput => true;
        public bool ForceFullscreen => false;
        public bool VerticalFlipBlur => false;
        public void InitializeNativeWindow(Game game)
        {
            if (_form == null)
                _form = Form.FromHandle(game.Window.Handle) as Form;

            // DirectX requires this WinForms workaround to set the window icon.
            var iconPath = Path.Combine("Data", "Icon", "Icon.ico");
            if (File.Exists(iconPath))
                _form.Icon = new Icon(iconPath);
            _form.ShowIcon = true;
        }

        public bool TryCenterWindow(Game game)
        {
            if (_form == null)
                return false;

            var work = Screen.GetWorkingArea(_form);
            _form.Location = new Point(
                work.X + System.Math.Max(0, (work.Width  - _form.Width)  / 2),
                work.Y + System.Math.Max(0, (work.Height - _form.Height) / 2));
            return true;
        }

        public XnaRectangle GetDesktopBounds()
        {
            var bounds = Screen.PrimaryScreen.Bounds;
            foreach (var screen in Screen.AllScreens)
                bounds = Rectangle.Union(bounds, screen.Bounds);
            return new XnaRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }
    }
}

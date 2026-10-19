using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ProjectZ
{
    internal sealed class WindowsDx12PlatformWindow : IPlatformWindow, IDesktopWindowAdapter
    {
        public bool SupportsFullscreenConfiguration => true;
        public bool SupportsInactiveWindowInput => true;
        public bool ForceFullscreen => false;
        // Not OpenGL: render targets are not vertically flipped, so the blur is sampled as-is.
        public bool VerticalFlipBlur => false;

        public void InitializeNativeWindow(Game game) { }

        public bool TryCenterWindow(Game game) => false;

        public Rectangle GetDesktopBounds()
        {
            // Primary display only; multi-monitor restore falls back to centering.
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Rectangle(0, 0, mode.Width, mode.Height);
        }

    }
}

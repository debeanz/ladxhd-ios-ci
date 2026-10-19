using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ProjectZ.Base;
using ProjectZ.InGame.Controls;

namespace ProjectZ
{
    internal static class SdlNative
    {
        static SdlNative()
        {
            // Resolve SDL explicitly so these calls use MonoGame's native library.
            NativeLibrary.SetDllImportResolver(typeof(SdlNative).Assembly, ResolveSdl2);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SdlRect { public int X, Y, W, H; }

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_StartTextInput();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_StopTextInput();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetNumVideoDisplays();

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetDisplayBounds(int displayIndex, out SdlRect rect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetDisplayUsableBounds(int displayIndex, out SdlRect rect);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetWindowDisplayIndex(IntPtr window);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_GetWindowSize(IntPtr window, out int w, out int h);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_GetWindowPosition(IntPtr window, out int x, out int y);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SDL_SetWindowPosition(IntPtr window, int x, int y);

        [DllImport("SDL2", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SDL_GetWindowBordersSize(IntPtr window, out int top, out int left, out int bottom, out int right);

        private static IntPtr ResolveSdl2(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != "SDL2")
                return IntPtr.Zero;

            // Pick the SDL library name used by the current operating system.
            string[] candidates =
                OperatingSystem.IsWindows() ? ["SDL2.dll"] :
                OperatingSystem.IsLinux() ? ["libSDL2-2.0.so.0", "libSDL2.so"] :
                OperatingSystem.IsMacOS() ? ["libSDL2-2.0.0.dylib", "libSDL2.dylib"] :
                [];

            foreach (var candidate in candidates) {
                if (NativeLibrary.TryLoad(candidate, out var handle))
                    return handle;
            }

            return IntPtr.Zero;
        }
    }

    internal sealed class DesktopGLTextInputService : ITextInputService
    {
        public void SetEnabled(bool enabled)
        {
            if (enabled)
                SdlNative.SDL_StartTextInput();
            else
                SdlNative.SDL_StopTextInput();
        }
        public void OnGameActivated() => SetEnabled(InputHandler.WantsTextInput);
    }

    internal sealed class DesktopGlPlatformWindow : IPlatformWindow, IDesktopWindowAdapter
    {
        public bool SupportsFullscreenConfiguration => true;
        public bool SupportsInactiveWindowInput => true;
        public bool ForceFullscreen => false;
        public bool VerticalFlipBlur => true;
        public void InitializeNativeWindow(Game game) { }

        public bool TryCenterWindow(Game game)
        {
            var handle = game.Window.Handle;
            if (handle == IntPtr.Zero)
                return false;

            try
            {
                // Center on whichever display the window currently sits on.
                var display = SdlNative.SDL_GetWindowDisplayIndex(handle);
                if (display < 0)
                    display = 0;

                // UsableBounds excludes the taskbar / dock / panel.
                if (SdlNative.SDL_GetDisplayUsableBounds(display, out var work) != 0)
                    return false;

                SdlNative.SDL_GetWindowSize(handle, out var width, out var height);
                if (width <= 0 || height <= 0)
                    return false;

                // Borders are not reported on every window manager; zero is a safe default.
                if (SdlNative.SDL_GetWindowBordersSize(handle, out var top, out var left, out var bottom, out var right) != 0)
                    top = left = bottom = right = 0;

                var frameWidth = width + left + right;
                var frameHeight = height + top + bottom;

                SdlNative.SDL_SetWindowPosition(handle,
                    work.X + Math.Max(0, (work.W - frameWidth) / 2) + left,
                    work.Y + Math.Max(0, (work.H - frameHeight) / 2) + top);
                return true;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
            catch (TypeInitializationException) { return false; }
        }

        public Rectangle GetDesktopBounds()
        {
            try
            {
                Rectangle? union = null;

                for (var i = 0; i < SdlNative.SDL_GetNumVideoDisplays(); i++)
                {
                    if (SdlNative.SDL_GetDisplayBounds(i, out var r) != 0)
                        continue;

                    var bounds = new Rectangle(r.X, r.Y, r.W, r.H);
                    union = union.HasValue ? Rectangle.Union(union.Value, bounds) : bounds;
                }

                if (union.HasValue)
                    return union.Value;
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            catch (TypeInitializationException) { }

            // Primary display only.
            var mode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            return new Rectangle(0, 0, mode.Width, mode.Height);
        }

    }

    // SDL-based input service. macOS can move or shrink a borderless fullscreen window for
    // its notch safe area while SDL's cached window geometry lags behind. MonoGame then
    // reports mouse coordinates against the stale origin. This service measures how far the
    // reported window trails its display and supplies that distance as an offset to remove.
    internal sealed class DesktopGlPlatformInput : IPlatformInput
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

            var handle = game?.Window?.Handle ?? IntPtr.Zero;
            if (!Game1.FullScreen || handle == IntPtr.Zero)
                return false;

            try
            {
                var display = SdlNative.SDL_GetWindowDisplayIndex(handle);
                if (display < 0 || SdlNative.SDL_GetDisplayBounds(display, out var bounds) != 0)
                    return false;

                // SDL caches a stale origin when macOS shifts a borderless window below the
                // notch, derive the true top/left inset from how far the window trails its display.
                SdlNative.SDL_GetWindowPosition(handle, out var posX, out var posY);
                SdlNative.SDL_GetWindowSize(handle, out var width, out var height);

                offset = new Point(
                    Math.Max(0, bounds.X + bounds.W - posX - width),
                    Math.Max(0, bounds.Y + bounds.H - posY - height));
                return offset.X != 0 || offset.Y != 0;
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or TypeInitializationException)
            {
                return false;
            }
        }
    }
}

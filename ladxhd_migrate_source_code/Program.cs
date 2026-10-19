using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Avalonia;
using Mono.Options;
using ProjectZ.Shared;

namespace LADXHD_Migrater
{
    internal class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            bool showHelp = false;
            bool migrateAssets = false;
            bool createPatches = false;
            bool cleanBuild = false;
            bool buildRequested = false;
            string? buildPlatform = null;
            string? graphicsStr = null;
            bool dumpTargets = false;
            bool dumpRaw = false;

            var opts = new OptionSet {
                { "migrate-assets|m",  "Run asset migration in headless mode.",     _ => migrateAssets = true },
                { "create-patches|p",  "Create vcdiff patches from modified files.", _ => createPatches = true },
                { "clean-build|c",     "Remove all bin/obj/Publish folders.",        _ => cleanBuild = true },
                { "build|b:",          "Build for platform [windows|android|linux-x86|linux-arm64|macos-x86|macos-arm64]. Default: current platform.", v => { buildRequested = true; buildPlatform = v; } },
                { "graphics=",         "Target graphics API: directx, opengl. Default: directx (windows), opengl (others).", v => graphicsStr = v },
                { "dump-targets",      "Write the asset target table to the patcher's Resources folder.", _ => dumpTargets = true },
                { "dump-raw",          "With --dump-targets, emit the raw (uncompiled) table instead of the compiled one.", _ => dumpRaw = true },
                { "headless",          "Deprecated. Use --migrate-assets instead.", _ => showHelp = true },
                { "h|?|help",          "Show this help message.",                   _ => showHelp = true },
            };

            opts.Parse(args);

            if (showHelp)
                return ShowHelp(opts);

            if (dumpTargets)
                return RunDumpTargets(dumpRaw);

            if (buildRequested && buildPlatform == null)
                SetNativePlatform();

            if (migrateAssets || createPatches || cleanBuild || buildRequested)
                return RunHeadless(migrateAssets, createPatches, cleanBuild, buildRequested, buildPlatform, graphicsStr);

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }

        private static int RunDumpTargets(bool dumpRaw)
        {
            TryReconnectConsole();
            Config.Initialize();

            if (!Config.Patcher_Source.TestPath())
            {
                Console.Error.WriteLine("ERROR: ladxhd_patcher_source_code is missing. Run this from the repo root.");
                return 1;
            }

            string fileName = dumpRaw ? "file_targets_raw.txt" : "file_targets_xnb.txt";
            string path = Path.Combine(Config.Patcher_Resources, fileName);

            try
            {
                Directory.CreateDirectory(Config.Patcher_Resources);
                File.WriteAllText(path, AssetTargets.DumpTable(!dumpRaw), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }

            Console.WriteLine($"Wrote {(dumpRaw ? "raw" : "compiled")} target table to {path}");
            return 0;
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();

        private static int ShowHelp(Mono.Options.OptionSet opts)
        {
            TryReconnectConsole();
            Console.WriteLine();
            Console.WriteLine("LADXHD Migrater");
            Console.WriteLine("Usage: Migrater [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            opts.WriteOptionDescriptions(Console.Out);
            Console.WriteLine();
            Console.WriteLine("Exit codes: 0=Success, 1=Source assets missing, 2=Operation failed, 3=Invalid arguments");
            Console.WriteLine();
            return 0;
        }

        private static void SetNativePlatform()
        {
            Config.SelectedPlatform = Config.GetNativePlatform();
            Config.SelectedGraphics = (Config.SelectedPlatform == Config.Platform.Windows)
                ? Config.GraphicsAPI.DirectX11
                : Config.GraphicsAPI.OpenGL;
        }

        private static int RunHeadless(bool migrateAssets, bool createPatches, bool cleanBuild, bool buildRequested, string? buildPlatform, string? graphicsStr)
        {
            TryReconnectConsole();
            Functions.HeadlessMode = true;
            Config.Initialize();
            BuildAvaloniaApp().SetupWithoutStarting();

            if (buildRequested && buildPlatform != null)
            {
                try
                {
                    ParsePlatform(buildPlatform, graphicsStr);
                }
                catch (ArgumentException ex)
                {
                    Console.Error.WriteLine("ERROR: " + ex.Message);
                    return 3;
                }
            }

            if (migrateAssets)     return RunMigrate();
            if (createPatches)     return RunCreatePatches();
            if (cleanBuild)        return RunCleanBuild();
            if (buildRequested)    return RunBuild();

            return 0;
        }

        private static void ParsePlatform(string buildPlatform, string? graphicsStr)
        {
            Config.Platform platform = buildPlatform.ToLowerInvariant() switch
            {
                "windows"     => Config.Platform.Windows,
                "android"     => Config.Platform.Android,
                "linux-x86"   => Config.Platform.Linux_x64,
                "linux-arm64" => Config.Platform.Linux_Arm64,
                "macos-x86"   => Config.Platform.MacOS_x64,
                "macos-arm64" => Config.Platform.MacOS_Arm64,
                _ => throw new ArgumentException($"Invalid --build value '{buildPlatform}'. Valid values: windows, android, linux-x86, linux-arm64, macos-x86, macos-arm64")
            };

            Config.GraphicsAPI graphics;
            if (graphicsStr != null)
            {
                graphics = graphicsStr.ToLowerInvariant() switch
                {
                    "dx11"    => Config.GraphicsAPI.DirectX11,
                    "dx12"    => Config.GraphicsAPI.DirectX12,
                    "opengl"  => Config.GraphicsAPI.OpenGL,
                    "vulkan"  => Config.GraphicsAPI.Vulkan,
                    _ => throw new ArgumentException($"Invalid --graphics value '{graphicsStr}'. Valid values: dx11, dx12, opengl, vulkan")
                };
                if ((graphics == Config.GraphicsAPI.DirectX11 || graphics == Config.GraphicsAPI.DirectX12) && platform != Config.Platform.Windows)
                    throw new ArgumentException("--graphics dx11 and dx12 is only supported on Windows. Use --graphics opengl for other platforms.");
            }
            else
            {
                graphics = (platform == Config.Platform.Windows) ? Config.GraphicsAPI.DirectX11 : Config.GraphicsAPI.OpenGL;
            }

            Config.SelectedPlatform = platform;
            Config.SelectedGraphics = graphics;
        }

        private static int RunMigrate()
        {
            if (!Config.Orig_Content.TestPath() || !Config.Orig_Data.TestPath())
            {
                Console.Error.WriteLine("ERROR: assets_original/Content or assets_original/Data is missing.");
                return 1;
            }

            try
            {
                Functions.MigrateFiles();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }

            Console.WriteLine("Migration complete.");
            return 0;
        }

        private static int RunCreatePatches()
        {
            if (!Config.Orig_Content.TestPath() || !Config.Orig_Data.TestPath())
            {
                Console.Error.WriteLine("ERROR: assets_original/Content or assets_original/Data is missing.");
                return 1;
            }
            if (!Config.Update_Content.TestPath() || !Config.Update_Data.TestPath())
            {
                Console.Error.WriteLine("ERROR: ladxhd_game_source_code Content or Data is missing.");
                return 1;
            }

            try
            {
                Functions.CreatePatches();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }

            Console.WriteLine("Patches created.");
            return 0;
        }

        private static int RunCleanBuild()
        {
            try
            {
                Functions.CleanBuildFiles();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }

            Console.WriteLine("Build files cleaned.");
            return 0;
        }

        private static int RunBuild()
        {
            try
            {
                bool success = Functions.CreateBuild().GetAwaiter().GetResult();
                if (!success)
                {
                    Console.Error.WriteLine("ERROR: Build failed.");
                    return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("ERROR: " + ex.Message);
                return 2;
            }

            Console.WriteLine("Build complete.");
            return 0;
        }

        // ---- Windows console re-attachment (headless mode) ----

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int dwProcessId);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [SupportedOSPlatform("windows")]
        [DllImport("kernel32.dll")]
        private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        [SupportedOSPlatform("windows")]
        private static void TryReconnectConsole()
        {
            const int  ATTACH_PARENT_PROCESS = -1;
            const int  STD_OUTPUT_HANDLE     = -11;
            const int  STD_ERROR_HANDLE      = -12;
            const uint GENERIC_READ          = 0x80000000;
            const uint GENERIC_WRITE         = 0x40000000;
            const uint OPEN_EXISTING         = 3;
            const uint FILE_SHARE_WRITE      = 2;

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
            if (!AttachConsole(ATTACH_PARENT_PROCESS)) return;

            IntPtr hOut = CreateFile("CONOUT$", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (hOut != new IntPtr(-1))
            {
                SetStdHandle(STD_OUTPUT_HANDLE, hOut);
                SetStdHandle(STD_ERROR_HANDLE, hOut);
            }

            var stream = new FileStream(new Microsoft.Win32.SafeHandles.SafeFileHandle(hOut, false), FileAccess.Write);
            var writer = new StreamWriter(stream) { AutoFlush = true };
            Console.SetOut(writer);
            Console.SetError(writer);
        }
    }
}
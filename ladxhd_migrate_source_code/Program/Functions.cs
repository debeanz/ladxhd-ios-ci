using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using ProjectZ.Shared;
using static LADXHD_Migrater.Config;
using static LADXHD_Migrater.VCDiff;

namespace LADXHD_Migrater
{
    internal class Functions
    {
        public static bool HeadlessMode;

        public static async Task Notify(string title, string message, int timeoutSeconds = 0, bool altSound = false)
        {
            if (HeadlessMode)
                Console.WriteLine($"[{title}] {message}");
            else
                await OkayWindow.ShowAsync(title, message, timeoutSeconds, altSound);
        }

/*-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        FILE MAPPING CODE : NOT ALL FILES AND PATCHES ARE 1:1 FROM ORIGINAL GAME VERSION. NEW FILES NEED A "BASE" TO BE CREATED FROM USING A PATCH
       
-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------*/

        // THE "KEY" IS THE MASTER FILE THAT CREATES OTHER FILES FROM IT. THE "VALUE" IS THE STRING ARRAY THAT HOLDS THOSE FILES.
        // Table lives in AssetTargets.cs. This tool works on raw source assets before the content
        // pipeline runs, so it wants the uncompiled side.
        private const bool UseXnb = false;
        private static readonly IReadOnlyDictionary<string, string[]> fileTargets = AssetTargets.Get(UseXnb);
        private static readonly IReadOnlyDictionary<string, string> reverseFileTargets = AssetTargets.GetReverse(UseXnb);

/*-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        MIGRATION CODE : COPY NEW FILES THAT CAN NOT BE CREATED FROM PATCHES. THIS INCLUDES FONTS AND A BITMAP ICON FOR OPENGL PORTS.
       
-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------*/

        private static void CopyNewFiles()
        {
            // Set up the path to the two ".fnt" files for the Chinese fonts.
            string smallFont_chn_fileA = Path.Combine(Config.Update_Content, "Fonts", "smallFont_chn.fnt");
            string smallFont_chn_fileB = Path.Combine(Config.Update_Content, "Fonts", "smallFont_chn_redux.fnt");

            // Write the files to the "Content\Fonts" folder.
            File.WriteAllBytes(smallFont_chn_fileA, Resources.GetBytes("smallFont_chn.fnt"));
            File.WriteAllBytes(smallFont_chn_fileB, Resources.GetBytes("smallFont_chn_redux.fnt"));

            // Set up the path to the replacement fonts used for multi-platform support.
            string editorFontA = Path.Combine(Config.Update_Content, "Fonts", "Courier-Prime.ttf");
            string editorFontB = Path.Combine(Config.Update_Content, "Fonts", "NotoSans-Regular.ttf");

            // Write the files to the "Content\Fonts" folder.
            File.WriteAllBytes(editorFontA, Resources.GetBytes("Courier-Prime.ttf"));
            File.WriteAllBytes(editorFontB, Resources.GetBytes("NotoSans-Regular.ttf"));

            // Set up the path to the Icon.
            string iconPath = Path.Combine(Config.Update_Data, "Icon").CreatePath();

            // Write the icon to the "Data\Icon" folder.
            string iconFile = Path.Combine(iconPath, "Icon.ico");
            File.WriteAllBytes(iconFile, Resources.GetBytes("Icon.ico"));

            // Write the bitmap icon to the "Data\Icon" folder.
            string iconBmpFile = Path.Combine(iconPath, "Icon.bmp");
            File.WriteAllBytes(iconBmpFile, Resources.GetBytes("Icon.bmp"));

            // Write the png icon to the the "Data\Icon" folder.
            string iconPngFile = Path.Combine(iconPath, "Icon.png");
            File.WriteAllBytes(iconPngFile, Resources.GetBytes("Icon.png"));

            // Write the svg icon to the the "Data\Icon" folder.
            string iconSvgFile = Path.Combine(iconPath, "Icon.svg");
            File.WriteAllBytes(iconSvgFile,Resources.GetBytes("Icon.svg"));

            // Extract the Android buttons to the data path.
            string buttonsPath = Path.Combine(Config.Update_Data, "Buttons").CreatePath();

            // Check to see if buttons already exist in the path.
            if (!buttonsPath.IsPathEmpty())
                buttonsPath.ClearPath();

            // Create the zip file and extract the buttons.
            string zipButtonsPath = Path.Combine(buttonsPath, "android_buttons.zip");
            File.WriteAllBytes(zipButtonsPath, Resources.GetBytes("android_buttons.zip"));
            ZipFile.ExtractToDirectory(zipButtonsPath, buttonsPath);
            zipButtonsPath.RemovePath();

            // Extract the achievement images to the "Data\Achievements" folder.
            string achievementPath = Path.Combine(Config.Update_Data, "Achievements").CreatePath();

            // Check to see if achievement images already exist in the path.
            if (!achievementPath.IsPathEmpty())
                achievementPath.ClearPath();

            // Create the zip file and extract the achievement images.
            string zipAchievementsPath = Path.Combine(achievementPath, "achievements.zip");
            File.WriteAllBytes(zipAchievementsPath, Resources.GetBytes("achievements.zip"));
            ZipFile.ExtractToDirectory(zipAchievementsPath, achievementPath);
            zipAchievementsPath.RemovePath();
        }

/*-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        MIGRATION CODE : COPY OR PATCH V1.0.0 ASSETS IN "assets_original" USING PATCHES IN "assets_patches" TO CONTENT/DATA OF "ladxhd_game_source_code"
       
-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------*/

        private static void HandleMultiFilePatches(FileItem fileItem, string origPath, string updatePath)
        {
            // Check if the original file has derivatives.
            if (!fileTargets.TryGetValue(fileItem.Name, out var target))
                return;

            // If original file has derivative modified files based off of it.
            foreach (string newFile in target)
            {
                // Create a path to the patch file.
                string vcDiffFile = Path.Combine(Config.Patches, newFile + ".vcdiff");

                // Some files may not yet exist if they haven't been created. The dictionary at the top of
                // this class are "potential" file, especially files that are created for alternate languages.
                if (vcDiffFile.TestPath())
                {
                    // Create all derivative files based on the original file.
                    string patchedFile = Path.Combine(updatePath + fileItem.DirectoryName.Replace(origPath, ""), newFile);
                    VCDiff.Execute(Operation.Apply, fileItem.FullName, vcDiffFile, patchedFile);
                }
            }
        }

        public static void MigrateCopyLoop(string origPath, string updatePath)
        {
            // Remove the target path before copying files to it.
            updatePath.RemovePath();

            // Loop through the original files.
            foreach (string file in origPath.GetFiles("*", true))
            {
                // Get the current "original" file as a file item.
                FileItem fileItem = new FileItem(file);

                // Make sure it's not in "bin" or "obj" folders.
                if (fileItem.IsInFolder("bin") || fileItem.IsInFolder("obj"))
                    continue;

                // Set up path to output vcdiff files and create the output folder while also getting path to updated file.
                string vcDiffFile = Path.Combine(Config.Patches, fileItem.Name + ".vcdiff");
                string patchedFile = Path.Combine((updatePath + fileItem.DirectoryName.Replace(origPath, "")).CreatePath(), fileItem.Name);

                // If a patch exists for the current file, patch it. If it doesn't then copy it.
                if (vcDiffFile.TestPath())
                    VCDiff.Execute(Operation.Apply, fileItem.FullName, vcDiffFile, patchedFile);
                else
                    File.Copy(fileItem.FullName, patchedFile, true);

                // Handle modified files that are derivatives of original files.
                HandleMultiFilePatches(fileItem, origPath, updatePath);
            }
            // Copy the files to the destination.
            CopyNewFiles();

            // After migration, some map files are not needed.
            CleanUp.RemoveJunkMapFiles();
        }

        public static void MigrateFiles()
        {
            MigrateCopyLoop(Config.Orig_Content, Config.Update_Content);
            MigrateCopyLoop(Config.Orig_Data, Config.Update_Data);
        }

/*-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        PATCHES CODE : CREATE PATCHES FROM CONTENT/DATA IN "ladxhd_game_source_code" VS. FILES IN "assets_original" TO FOLDER "assets_patches"

-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------*/

        private static void CreatePatchLoop(string origPath, string updatePath)
        {
            // Create the "assets_patches" folder if it doesn't exist.
            Config.Patches.CreatePath(true);

            // Loop through the new Content and Data folders.
            foreach (string file in updatePath.GetFiles("*", true))
            {
                // Get the current "new" file as a file item.
                FileItem fileItem = new FileItem(file);
                string oldFile = "";

                // Make sure it's not in "bin" or "obj" folders.
                if (fileItem.IsInFolder("bin") || fileItem.IsInFolder("obj")) continue;

                // Get the path to the original file using the relative folder.
                oldFile = Path.Combine(origPath + fileItem.DirectoryName.Replace(updatePath, ""), fileItem.Name);
                
                // If the file doesn't exist it might be a derivative ("eng.lng" -> "deu.lng" for example).
                if (!oldFile.TestPath() && reverseFileTargets.TryGetValue(fileItem.Name, out string shortName))
                    oldFile = Path.Combine(origPath + fileItem.DirectoryName.Replace(updatePath, ""), shortName);

                // If the original file doesn't exist or it's not a derivative skip it.
                if (oldFile == "" || !oldFile.TestPath()) continue;

                // Now that we have both files, calculate hashes from them.
                string oldHash = oldFile.CalculateHash("MD5");
                string newHash = fileItem.FullName.CalculateHash("MD5");

                // If the hashes differ, the file has been updated.
                if (oldHash != newHash)
                {
                    // Create a patch from the old file vs. the new file.
                    string patchName = Path.Combine(Config.Patches, fileItem.Name + ".vcdiff");
                    VCDiff.Execute(Operation.Create, oldFile, fileItem.FullName, patchName);
                }
            }
        }

        public static void CreatePatches()
        {
            Config.Patches.ClearPath();
            CreatePatchLoop(Config.Orig_Content, Config.Update_Content);
            CreatePatchLoop(Config.Orig_Data, Config.Update_Data);
        }

/*-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        CLEAN BUILD FILES CODE : REMOVE ALL "bin" / "obj" FOLDERS AND REMOVE PREVIOUS BUILD FOLDERS "publish" / "zelda_ladxhd_build".

-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------*/

        public static void CleanBuildFiles()
        {
            Path.Combine(Config.Game_Source, "_Publish").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.Android", "bin").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.Android", "obj").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.Content", "bin").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.Content", "obj").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.Core", "bin").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.Core", "obj").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.DesktopGL", "bin").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.DesktopGL", "obj").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.DesktopVK", "bin").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.DesktopVK", "obj").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.Editor", "bin").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.Editor", "obj").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.WindowsDX11", "bin").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.WindowsDX11", "obj").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.WindowsDX12", "bin").RemovePath();
            Path.Combine(Config.Game_Source, "ProjectZ.WindowsDX12", "obj").RemovePath();
            Path.Combine(Config.Launcher_Source, "_Publish").RemovePath();
            Path.Combine(Config.Launcher_Source, "bin").RemovePath();
            Path.Combine(Config.Launcher_Source, "obj").RemovePath();
            Path.Combine(Config.Migrate_Source, "_Publish").RemovePath();
            Path.Combine(Config.Migrate_Source, "bin").RemovePath();
            Path.Combine(Config.Migrate_Source, "obj").RemovePath();
            Path.Combine(Config.Patcher_Source, "_Publish").RemovePath();
            Path.Combine(Config.Patcher_Source, "bin").RemovePath();
            Path.Combine(Config.Patcher_Source, "Patches").RemovePath();
            Path.Combine(Config.Patcher_Source, "obj").RemovePath();
            Path.Combine(Config.PatcherLite_Source, "_Publish").RemovePath();
            Path.Combine(Config.PatcherLite_Source, "bin").RemovePath();
            Path.Combine(Config.PatcherLite_Source, "obj").RemovePath();
            Path.Combine(Config.VCDiff_Source, "_Publish").RemovePath();
            Path.Combine(Config.VCDiff_Source, "bin").RemovePath();
            Path.Combine(Config.VCDiff_Source, "obj").RemovePath();
            Path.Combine(Config.BaseFolder, "_Publish").RemovePath();
        }

/*-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        CREATE NEW BUILD CODE: BUILD A NEW VERSION USING THE CURRENT ASSETS AND MOVE TO THE FOLDER "zelda_ladxhd_build"

-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------*/

        public async static Task<bool> CreateBuild()
        {
            if (!await DotNet.BuildGame())
                return false;

            string finalGamePath = "";
            string publishFolder = Path.Combine(Config.BaseFolder, "_Publish");

            // The path to where the build will be output.
            finalGamePath = Path.Combine(publishFolder, (Config.SelectedPlatform, Config.SelectedGraphics) switch
            {
                (Platform.Windows, GraphicsAPI.DirectX11)  => "zelda_ladxhd_build_windows_dx11",
                (Platform.Windows, GraphicsAPI.DirectX12)  => "zelda_ladxhd_build_windows_dx12",
                (Platform.Windows, GraphicsAPI.OpenGL)     => "zelda_ladxhd_build_windows_gl",
                (Platform.Windows, GraphicsAPI.Vulkan)     => "zelda_ladxhd_build_windows_vk",
                (Platform.Android, GraphicsAPI.OpenGL)     => "zelda_ladxhd_build_android",
                (Platform.Linux_Arm64, GraphicsAPI.OpenGL) => "zelda_ladxhd_build_linux_arm64_gl",
                (Platform.Linux_Arm64, GraphicsAPI.Vulkan) => "zelda_ladxhd_build_linux_arm64_vk",
                (Platform.Linux_x64, GraphicsAPI.OpenGL)   => "zelda_ladxhd_build_linux_x86_64_gl",
                (Platform.Linux_x64, GraphicsAPI.Vulkan)   => "zelda_ladxhd_build_linux_x86_64_vk",
                (Platform.MacOS_Arm64, GraphicsAPI.OpenGL) => "zelda_ladxhd_build_macos_arm64_gl",
                (Platform.MacOS_Arm64, GraphicsAPI.Vulkan) => "zelda_ladxhd_build_macos_arm64_vk",
                (Platform.MacOS_x64, GraphicsAPI.OpenGL)   => "zelda_ladxhd_build_macos_x86_64_gl",
                (Platform.MacOS_x64, GraphicsAPI.Vulkan)   => "zelda_ladxhd_build_macos_x86_64_vk",
                _ => ""
            }).CreatePath();

            // Move the publish folder to the root directory.
            Config.Build_Path.MovePath(finalGamePath, true);

            // Remove the old publish folder if it's empty.
            if (Config.Publish_Path.IsPathEmpty())
                Config.Publish_Path.RemovePath();

            // Build the matching launcher and copy it into the game output folder.
            if (await DotNet.BuildLauncher())
            {
                string launcherPublish = Path.Combine(Config.Launcher_Source, "_Publish");
                if (launcherPublish.TestPath())
                {
                    // Copy launcher files into the already-moved game folder.
                    foreach (string file in launcherPublish.GetFiles("*", true))
                    {
                        FileItem item = new FileItem(file);
                        string dest = Path.Combine(finalGamePath, item.Name);
                        File.Copy(file, dest, true);
                    }
                    launcherPublish.RemovePath();
                }
            }
            // Remove any leftover files that are unnecessary.
            HashSet<string> junkFiles = new HashSet<string> { "nfd.lib", "nfd.pdb", 
                "_Microsoft.Android.Resource.Designer.dll", "Launcher.dll.config",
                "libAvaloniaNative.dylib", "libHarfBuzzSharp.dylib", "libSkiaSharp.dylib" };
            foreach (string file in finalGamePath.GetFiles("*"))
            {
                FileItem item = new FileItem(file);
                if (junkFiles.Contains(item.Name))
                {
                    item.FullName.RemovePath();
                }
            }
            
            return true;
        }
    }
}

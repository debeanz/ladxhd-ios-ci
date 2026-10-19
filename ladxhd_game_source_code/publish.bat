::========================================================================================================================================
:: LINK'S AWAKENING DX HD: Game Publish Script
:: By: Bighead
:: Publishes all game builds into a "_Publish" folder within "ladxhd_game_source_code". 
::========================================================================================================================================

@echo off
cd /d "%~dp0"
Title The Legend of Zelda: Link's Awakening DX HD - Game Publish Script

echo ------------------------------------------------------------------------------------
echo.
echo The Legend of Zelda: Link's Awakening DX HD - Game Publish Script 

::========================================================================================================================================
:: Configuration
::========================================================================================================================================

set RunCreatePatches=true

::========================================================================================================================================
:: Clean Previous Builds
::========================================================================================================================================

if exist "%~dp0_Publish" (
    echo Cleaning previous builds...
    rd /s /q "%~dp0_Publish"
)

::========================================================================================================================================
:: Restore Projects
::========================================================================================================================================

call:ShowTaskInfo "Restoring projects"

:: Restore all projects.
echo Restoring game projects...
dotnet restore ProjectZ.Content\ProjectZ.Content.csproj
if %errorlevel% neq 0 ( echo Restore failed ^(Content Builder^)! & pause & exit /b 1 )
dotnet restore ProjectZ.WindowsDX11\ProjectZ.WindowsDX11.csproj
if %errorlevel% neq 0 ( echo Restore failed ^(ProjectZ.WindowsDX11^)! & pause & exit /b 1 )
dotnet restore ProjectZ.DesktopGL\ProjectZ.DesktopGL.csproj
if %errorlevel% neq 0 ( echo Restore failed ^(ProjectZ.DesktopGL^)! & pause & exit /b 1 )
dotnet restore ProjectZ.Android\ProjectZ.Android.csproj
if %errorlevel% neq 0 ( echo Restore failed ^(ProjectZ.Android^)! & pause & exit /b 1 )
dotnet restore ProjectZ.WindowsDX12\ProjectZ.WindowsDX12.csproj
if %errorlevel% neq 0 ( echo Restore failed ^(ProjectZ.WindowsDX12^)! & pause & exit /b 1 )
dotnet restore ProjectZ.DesktopVK\ProjectZ.DesktopVK.csproj
if %errorlevel% neq 0 ( echo Restore failed ^(ProjectZ.DesktopVK^)! & pause & exit /b 1 )

::========================================================================================================================================
:: Content Builder
::========================================================================================================================================

call:ShowTaskInfo "Building MonoGame Content Builder"

:: Build the Content Builder.
echo Build Content Builder App...
dotnet build --nologo ProjectZ.Content\ProjectZ.Content.csproj -c Release --no-restore
if %errorlevel% neq 0 ( echo Content builder build failed! & pause & exit /b 1 )

::========================================================================================================================================
:: Windows Builds
::========================================================================================================================================

call:ShowTaskInfo "Building Windows projects"

:: Build Windows DX11
echo Building Windows ^(DirectX 11^)...
dotnet build --nologo ProjectZ.WindowsDX11\ProjectZ.WindowsDX11.csproj -c Release --no-restore -p:PublishProfile=FolderProfile_Windows-DX11
if %errorlevel% neq 0 ( echo Windows Direct-X 11 prebuild failed! & pause & exit /b 1 )

echo Publishing Windows ^(DirectX 11^)...
dotnet publish ProjectZ.WindowsDX11\ProjectZ.WindowsDX11.csproj -c Release --no-restore -p:PublishProfile=FolderProfile_Windows-DX11
if %errorlevel% neq 0 ( echo Windows Direct-X 11 publish failed! & pause & exit /b 1 )

:: Build Windows DX12
echo Building Windows ^(DirectX 12^)...
dotnet build --nologo ProjectZ.WindowsDX12\ProjectZ.WindowsDX12.csproj -c Release --no-restore -p:PublishProfile=FolderProfile_Windows-DX12
if %errorlevel% neq 0 ( echo Windows Direct-X 12 prebuild failed! & pause & exit /b 1 )

echo Publishing Windows ^(DirectX 12^)...
dotnet publish ProjectZ.WindowsDX12\ProjectZ.WindowsDX12.csproj -c Release --no-restore -p:PublishProfile=FolderProfile_Windows-DX12
if %errorlevel% neq 0 ( echo Windows Direct-X 12 publish failed! & pause & exit /b 1 )

:: Build Windows OpenGL
echo Building Windows ^(OpenGL^)...
dotnet build --nologo ProjectZ.DesktopGL\ProjectZ.DesktopGL.csproj -c Release -r win-x64 --no-restore -p:PublishProfile=FolderProfile_Windows-GL
if %errorlevel% neq 0 ( echo Windows OpenGL prebuild failed! & pause & exit /b 1 )

echo Publishing Windows ^(OpenGL^)...
dotnet publish ProjectZ.DesktopGL\ProjectZ.DesktopGL.csproj -c Release -r win-x64 --no-restore -p:PublishProfile=FolderProfile_Windows-GL
if %errorlevel% neq 0 ( echo Windows OpenGL publish failed! & pause & exit /b 1 )

:: Build Windows Vulkan
echo Building Windows ^(Vulkan^)...
dotnet build --nologo ProjectZ.DesktopVK\ProjectZ.DesktopVK.csproj -c Release --no-restore -p:PublishProfile=FolderProfile_Windows-VK
if %errorlevel% neq 0 ( echo Windows Vulkan prebuild failed! & pause & exit /b 1 )

echo Publishing Windows ^(Vulkan^)...
dotnet publish ProjectZ.DesktopVK\ProjectZ.DesktopVK.csproj -c Release --no-restore -p:PublishProfile=FolderProfile_Windows-VK
if %errorlevel% neq 0 ( echo Windows Vulkan publish failed! & pause & exit /b 1 )

::========================================================================================================================================
:: Android Builds
::========================================================================================================================================

call:ShowTaskInfo "Building Android projects"

:: Build Android OpenGL
echo Publishing Android APK ^(OpenGL^)...
dotnet publish ProjectZ.Android\ProjectZ.Android.csproj -c Release --no-restore -p:PublishProfile=FolderProfile_Android
if %errorlevel% neq 0 ( echo Android OpenGL build failed! & pause & exit /b 1 )

::========================================================================================================================================
:: Linux Builds
::========================================================================================================================================

call:ShowTaskInfo "Building Linux projects"

:: Build Linux x86-64 OpenGL
echo Publishing Linux x64 ^(OpenGL^)...
dotnet publish ProjectZ.DesktopGL\ProjectZ.DesktopGL.csproj -c Release -r linux-x64 --no-restore -p:PublishProfile=FolderProfile_Linux-x86_64
if %errorlevel% neq 0 ( echo Linux x86_64 OpenGL build failed! & pause & exit /b 1 )

:: Build Linux x86-64 Vulkan
echo Publishing Linux x64 ^(Vulkan^)...
dotnet publish ProjectZ.DesktopVK\ProjectZ.DesktopVK.csproj -c Release -r linux-x64 --no-restore -p:PublishProfile=FolderProfile_Linux-x86_64
if %errorlevel% neq 0 ( echo Linux x86_64 Vulkan build failed! & pause & exit /b 1 )

:: Build Linux ARM64 OpenGL
echo Publishing Linux Arm64 ^(OpenGL^)...
dotnet publish ProjectZ.DesktopGL\ProjectZ.DesktopGL.csproj -c Release -r linux-arm64 --no-restore -p:PublishProfile=FolderProfile_Linux-Arm64
if %errorlevel% neq 0 ( echo Linux Arm64 OpenGL build failed! & pause & exit /b 1 )

:: Build Linux ARM64 Vulkan
echo Publishing Linux Arm64 ^(Vulkan^)...
dotnet publish ProjectZ.DesktopVK\ProjectZ.DesktopVK.csproj -c Release -r linux-arm64 --no-restore -p:PublishProfile=FolderProfile_Linux-Arm64
if %errorlevel% neq 0 ( echo Linux Arm64 Vulkan build failed! & pause & exit /b 1 )

::========================================================================================================================================
:: MacOS Builds
::========================================================================================================================================

call:ShowTaskInfo "Building MacOS projects"

:: Build MacOS x86-64 OpenGL
echo Publishing MacOS x64 ^(OpenGL^)...
dotnet publish ProjectZ.DesktopGL\ProjectZ.DesktopGL.csproj -c Release -r osx-x64 --no-restore -p:PublishProfile=FolderProfile_MacOS-x86_64
if %errorlevel% neq 0 ( echo MacOS x86_64 OpenGL build failed! & pause & exit /b 1 )

:: Build MacOS x86-64 Vulkan
echo Publishing MacOS x64 ^(Vulkan^)...
dotnet publish ProjectZ.DesktopVK\ProjectZ.DesktopVK.csproj -c Release -r osx-x64 --no-restore -p:PublishProfile=FolderProfile_MacOS-x86_64
if %errorlevel% neq 0 ( echo MacOS x86_64 Vulkan build failed! & pause & exit /b 1 )

:: Build MacOS ARM64 OpenGL
echo Publishing MacOS arm64 ^(OpenGL^)...
dotnet publish ProjectZ.DesktopGL\ProjectZ.DesktopGL.csproj -c Release -r osx-arm64 --no-restore -p:PublishProfile=FolderProfile_MacOS-Arm64
if %errorlevel% neq 0 ( echo MacOS Arm64 OpenGL build failed! & pause & exit /b 1 )

:: Build MacOS ARM64 Vulkan
echo Publishing MacOS arm64 ^(Vulkan^)...
dotnet publish ProjectZ.DesktopVK\ProjectZ.DesktopVK.csproj -c Release -r osx-arm64 --no-restore -p:PublishProfile=FolderProfile_MacOS-Arm64
if %errorlevel% neq 0 ( echo MacOS Arm64 Vulkan build failed! & pause & exit /b 1 )

::========================================================================================================================================
:: Clean up unnecessary files
::========================================================================================================================================

call:ShowTaskInfo "Cleaning up junk files"

echo.
echo Cleaning up junk files...
for /r "%~dp0_Publish" %%f in (nfd.lib nfd.pdb sosdocsunix.txt com.zelda.ladxhd.apk _Microsoft.Android.Resource.Designer.dll) do (
  if exist "%%f" (
    echo Deleting: %%f
    del "%%f"
  )
)

::========================================================================================================================================
:: Create Patches
::========================================================================================================================================

if [%RunCreatePatches%]==[true] (
    echo Running CreatePatches.ps1...
    echo.
    powershell -ExecutionPolicy Bypass -File "%~dp0..\ladxhd_patcher_source_code\CreatePatches.ps1"
    if %errorlevel% neq 0 ( echo CreatePatches failed! & pause & exit /b 1 )
)
goto:EndScript

::========================================================================================================================================
:: Console Task Info Message
::========================================================================================================================================

:ShowTaskInfo
echo.
echo ------------------------------------------------------------------------------------
echo.
echo Task: %~1 ...
echo.
echo ------------------------------------------------------------------------------------
echo.
goto:eof

::========================================================================================================================================
:: Finish
::========================================================================================================================================

:EndScript
echo.
if [%RunCreatePatches%]==[true] (
    echo Done! Game built, patches created, and launcher published.
) else (
    echo Done! Builds are in the Publish folder.
    pause >nul
)

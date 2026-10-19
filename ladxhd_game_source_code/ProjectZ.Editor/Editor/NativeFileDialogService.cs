using System.Collections.Generic;
using NativeFileDialogSharp;

namespace ProjectZ.Editor
{
    public sealed class NativeFileDialogService : IFileDialogService
    {
        public bool TryOpen(string extension, string defaultPath, out string path)
        {
            var result = Dialog.FileOpen(extension, defaultPath);
            path = result.IsOk ? result.Path : null;
            return result.IsOk;
        }

        public bool TrySave(string extension, string defaultPath, out string path)
        {
            var result = Dialog.FileSave(extension, defaultPath);
            path = result.IsOk ? result.Path : null;
            return result.IsOk;
        }

        public IReadOnlyList<string> OpenMultiple(string extension)
        {
            var result = Dialog.FileOpenMultiple(extension);
            return result.IsOk ? result.Paths : [];
        }
    }
}

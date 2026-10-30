using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XamlNavigator.Localization;
using XamlNavigator.Platform;

namespace XamlNavigator.DynamicPages
{
    /// <summary>
    /// Папка с фото для поля типа Photo (<see cref="FolderName"/>): в базе хранится имя файла, сам файл — здесь. Файл ищется по имени,
    /// а если не нашёлся — по имени без расширения.
    /// </summary>
    public static class PhotoStore
    {
        private static readonly object Sync = new object();
        private static Dictionary<string, string> _index;
        private static DateTime _indexedAt;
        private static string _folderPath;
        private static string _folderName = "Photos";

        /// <summary>
        /// Имя папки с фото рядом с программой (или в данных приложения). По умолчанию «Photos»; приложение,
        /// у которого фото уже лежат в другой папке (например, «Фото»), задаёт её имя при запуске.
        /// </summary>
        public static string FolderName
        {
            get { return _folderName; }
            set
            {
                _folderName = string.IsNullOrWhiteSpace(value) ? "Photos" : value;
                InvalidateIndex();
            }
        }

        /// <summary>
        /// Полный путь к папке. По умолчанию — от платформы (рядом с программой в WPF, в данных приложения в MAUI);
        /// можно задать свой.
        /// </summary>
        public static string FolderPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_folderPath)) return _folderPath;
                var root = NavigatorPlatform.PhotoService?.DefaultFolder;
                return !string.IsNullOrEmpty(root) ? root : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FolderName);
            }
            set
            {
                _folderPath = value;
                InvalidateIndex();
            }
        }

        public static void EnsureFolder()
        {
            try { Directory.CreateDirectory(FolderPath); }
            catch (Exception) { /* ignored */ }
        }

        public static string Resolve(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return null;

            var name = SafeFileName(fileName);
            if (string.IsNullOrEmpty(name)) return null;

            var direct = Path.Combine(FolderPath, name);
            if (File.Exists(direct)) return direct;

            var index = Index();
            string found;
            return index.TryGetValue(Path.GetFileNameWithoutExtension(name), out found) ? found : null;
        }

        public static bool Exists(string fileName) => Resolve(fileName) != null;

        /// <summary>Скопировать файл в папку и вернуть имя, под которым он там лежит; null — не вышло (см. error).</summary>
        public static string Save(string sourcePath, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                error = Tr.T("File not found.");
                return null;
            }

            try
            {
                EnsureFolder();

                var source = new FileInfo(sourcePath);
                if (IsInsideFolder(source.FullName))
                {
                    InvalidateIndex();
                    return source.Name;
                }

                var target = FreeName(source);
                if (!File.Exists(target)) File.Copy(source.FullName, target, false);
                InvalidateIndex();
                return Path.GetFileName(target);
            }
            catch (Exception ex)
            {
                error = Tr.F("Couldn't copy the file to the \"{0}\" folder: {1}", FolderName, ex.Message);
                return null;
            }
        }

        public static bool CanOpenFolder => NavigatorPlatform.PhotoService != null && NavigatorPlatform.PhotoService.CanOpenFolder;

        public static void OpenFolder()
        {
            EnsureFolder();
            try { NavigatorPlatform.PhotoService?.OpenFolder(FolderPath); }
            catch (Exception) { /* ignored */ }
        }

        /// <summary>Сбросить закэшированный список файлов папки — он перечитается при следующем поиске фото.</summary>
        public static void InvalidateIndex()
        {
            lock (Sync) { _index = null; }
        }

        /// <summary>Прежнее имя <see cref="InvalidateIndex"/>.</summary>
        [Obsolete("Use InvalidateIndex.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public static void Forget() => InvalidateIndex();

        private static bool IsInsideFolder(string fullPath)
        {
            var folder = Path.GetFullPath(FolderPath).TrimEnd(Path.DirectorySeparatorChar);
            var directory = Path.GetDirectoryName(Path.GetFullPath(fullPath)) ?? "";
            return string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar), folder, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Имя в папке для копии: то же, если свободно или там уже лежит этот же файл, иначе с номером.
        /// </summary>
        private static string FreeName(FileInfo source)
        {
            var bare = Path.GetFileNameWithoutExtension(source.Name);
            var extension = source.Extension;

            var candidate = Path.Combine(FolderPath, source.Name);
            for (var i = 2; File.Exists(candidate); i++)
            {
                if (SameContent(new FileInfo(candidate), source)) return candidate;
                candidate = Path.Combine(FolderPath, bare + "_" + i + extension);
            }
            return candidate;
        }

        private static bool SameContent(FileInfo a, FileInfo b)
        {
            if (a.Length != b.Length) return false;
            const int chunk = 64 * 1024;
            var bufferA = new byte[chunk];
            var bufferB = new byte[chunk];
            using (var streamA = a.OpenRead())
            using (var streamB = b.OpenRead())
            {
                while (true)
                {
                    var readA = ReadFully(streamA, bufferA);
                    var readB = ReadFully(streamB, bufferB);
                    if (readA != readB) return false;
                    if (readA == 0) return true;
                    for (var i = 0; i < readA; i++)
                        if (bufferA[i] != bufferB[i]) return false;
                }
            }
        }

        private static int ReadFully(Stream stream, byte[] buffer)
        {
            var total = 0;
            int read;
            while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0) total += read;
            return total;
        }

        private static string SafeFileName(string value)
        {
            try { return Path.GetFileName(value.Trim()); }
            catch (ArgumentException) { return null; }
        }

        private static Dictionary<string, string> Index()
        {
            lock (Sync)
            {
                DateTime stamp;
                try { stamp = Directory.Exists(FolderPath) ? Directory.GetLastWriteTimeUtc(FolderPath) : DateTime.MinValue; }
                catch (Exception) { stamp = DateTime.MinValue; }

                if (_index != null && stamp == _indexedAt) return _index;

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (Directory.Exists(FolderPath))
                        foreach (var file in Directory.GetFiles(FolderPath))
                        {
                            var bare = Path.GetFileNameWithoutExtension(file);
                            if (!map.ContainsKey(bare)) map[bare] = file;
                        }
                }
                catch (Exception) { /* ignored */ }

                _index = map;
                _indexedAt = stamp;
                return _index;
            }
        }

        public static int Count()
        {
            try { return Index().Count; }
            catch (Exception) { return 0; }
        }

        public static IEnumerable<string> Names() => Index().Values.Select(Path.GetFileName);
    }
}

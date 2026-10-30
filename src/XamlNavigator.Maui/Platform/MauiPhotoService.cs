using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using XamlNavigator.DynamicPages;
using XamlNavigator.Localization;

namespace XamlNavigator.Platform
{
    /// <summary>Поле «Фото» в MAUI: папка в данных приложения, выбор файла через FilePicker.</summary>
    public class MauiPhotoService : IPhotoService
    {
        public string DefaultFolder
        {
            get
            {
                try { return Path.Combine(FileSystem.AppDataDirectory, PhotoStore.FolderName); }
                catch (Exception) { return null; } // без платформы (тесты) — папка рядом с программой
            }
        }

        public object LoadImage(string path, int height)
        {
            // ImageSource.FromFile ошибок не сообщает: не картинку отсекаем по сигнатуре файла, чтобы поле «Фото»
            // честно написало «не удалось открыть», а не показало пустое место.
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || !LooksLikeImage(path)) return null;
            return ImageSource.FromFile(path);
        }

        private static bool LooksLikeImage(string path)
        {
            var head = new byte[12];
            int read;
            try
            {
                using (var stream = File.OpenRead(path)) read = stream.Read(head, 0, head.Length);
            }
            catch (Exception) { return false; }
            if (read < 4) return false;

            bool Starts(params byte[] signature)
            {
                if (read < signature.Length) return false;
                for (var i = 0; i < signature.Length; i++) if (head[i] != signature[i]) return false;
                return true;
            }

            return Starts(0x89, 0x50, 0x4E, 0x47)                                         // PNG
                   || Starts(0xFF, 0xD8, 0xFF)                                             // JPEG
                   || Starts(0x47, 0x49, 0x46)                                             // GIF
                   || Starts(0x42, 0x4D)                                                   // BMP
                   || Starts(0x49, 0x49, 0x2A, 0x00) || Starts(0x4D, 0x4D, 0x00, 0x2A)     // TIFF
                   || (Starts(0x52, 0x49, 0x46, 0x46) && read >= 12 && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50) // WEBP
                   || (read >= 12 && head[4] == 0x66 && head[5] == 0x74 && head[6] == 0x79 && head[7] == 0x70 && IsImageBrand(head)); // HEIC/AVIF
        }

        /// <summary>Контейнер ISO BMFF (ftyp) бывает и видео (MP4, MOV): картинкой считаем только бренды HEIF/AVIF.</summary>
        private static bool IsImageBrand(byte[] head)
        {
            var brand = System.Text.Encoding.ASCII.GetString(head, 8, 4);
            return brand == "heic" || brand == "heix" || brand == "hevc" || brand == "heim" || brand == "heis"
                   || brand == "mif1" || brand == "msf1" || brand == "avif" || brand == "avis";
        }

        public void ClearImageCache() { }

        public async Task<string> PickImageAsync(string initialFolder)
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = Tr.T("Choose a photo"),
                FileTypes = FilePickerFileType.Images
            });
            if (file == null) return null;
            if (!string.IsNullOrEmpty(file.FullPath) && File.Exists(file.FullPath)) return file.FullPath;

            // Android отдаёт content://-поток без пути: копия во временную папку, оттуда PhotoStore возьмёт файл.
            var copy = Path.Combine(FileSystem.CacheDirectory, file.FileName);
            using (var source = await file.OpenReadAsync())
            using (var target = File.Create(copy))
                await source.CopyToAsync(target);
            return copy;
        }

        public bool CanOpenFolder
        {
            get
            {
#if WINDOWS
                return true;
#else
                return false;
#endif
            }
        }

        public void OpenFolder(string path)
        {
#if WINDOWS
            System.Diagnostics.Process.Start("explorer.exe", "\"" + path + "\"");
#endif
        }
    }
}

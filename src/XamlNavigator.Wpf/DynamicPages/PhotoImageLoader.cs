using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace XamlNavigator.DynamicPages
{
    public static class PhotoImageLoader
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, ImageSource> Cache = new Dictionary<string, ImageSource>();

        /// <summary>Картинка по имени файла в папке «Фото».</summary>
        public static ImageSource Load(string fileName, int height)
        {
            var path = PhotoStore.Resolve(fileName);
            return path == null ? null : LoadFile(path, height);
        }

        /// <summary>Картинка по полному пути, уменьшенная до высоты <paramref name="height"/>; кэшируется.</summary>
        public static ImageSource LoadFile(string path, int height)
        {
            if (string.IsNullOrEmpty(path)) return null;

            string key;
            try
            {
                var info = new FileInfo(path);
                key = path + "|" + height + "|" + info.Length + "|" + info.LastWriteTimeUtc.Ticks;
            }
            catch (Exception) { return null; }

            lock (Sync)
            {
                ImageSource cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
            }

            var image = Decode(path, height);

            lock (Sync)
            {
                if (Cache.Count > 400) Cache.Clear();
                Cache[key] = image;
            }
            return image;
        }

        private static ImageSource Decode(string path, int height)
        {
            try
            {
                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                image.DecodePixelHeight = height;
                image.UriSource = new Uri(path, UriKind.Absolute);
                image.EndInit();
                image.Freeze();
                return image;
            }
            catch (Exception) { return null; }
        }

        public static void ClearCache()
        {
            lock (Sync) { Cache.Clear(); }
        }
    }

    public class PhotoImageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int height;
            if (!int.TryParse(System.Convert.ToString(parameter, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out height) || height <= 0) height = 48;
            return PhotoImageLoader.Load(value as string, height);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
    }
}
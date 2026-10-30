using System.Threading.Tasks;

namespace XamlNavigator.Platform
{
    /// <summary>Поле «Фото» динамической страницы: показ картинки, выбор файла, папка с фото.</summary>
    public interface IPhotoService
    {
        /// <summary>Папка «Фото» по умолчанию: рядом с программой в WPF, в данных приложения в MAUI.</summary>
        string DefaultFolder { get; }

        /// <summary>Картинка для показа (ImageSource платформы) или null, если файл не открылся.</summary>
        object LoadImage(string path, int height);

        /// <summary>Забыть закэшированные картинки.</summary>
        void ClearImageCache();

        /// <summary>Спросить у пользователя файл картинки; null — передумал.</summary>
        Task<string> PickImageAsync(string initialFolder);

        bool CanOpenFolder { get; }

        void OpenFolder(string path);
    }
}

using System;
using System.ComponentModel;

namespace XamlNavigator.Models
{
    /// <summary>Чем является динамическая страница: формой одной записи или списком записей.</summary>
    public enum PageDisplayMode
    {
        /// <summary>Форма записи (новой или открытой на правку).</summary>
        Edit = 0,

        /// <summary>Список записей с поиском; правка — на парной странице-форме (PairedPageId).</summary>
        List = 1,

        /// <summary>Не поддерживается: показывается как <see cref="Edit"/>. Список с формой — это две страницы, связанные PairedPageId.</summary>
        [Obsolete("Not supported: renders as Edit. A list with a form is two pages linked by PairedPageId.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        Both = 2
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;

namespace XamlNavigator.Localization
{
    /// <summary>
    /// Язык текстов навигатора (кнопки, подсказки, сообщения об ошибках). Строки в коде — английские и служат
    /// ключами. Порядок поиска перевода: <see cref="Translator"/> приложения → встроенная таблица языка
    /// (русский в комплекте, свои — через <see cref="AddTranslations"/>) → английский текст.
    /// </summary>
    public static class NavigatorLocalization
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, IDictionary<string, string>> Tables =
            new Dictionary<string, IDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["ru"] = RussianStrings.Table
            };

        /// <summary>
        /// Свой переводчик: получает английский ключ, возвращает перевод или null — тогда берётся встроенная
        /// таблица. Стоит первым, поэтому может и переопределить отдельные строки встроенного перевода.
        /// </summary>
        public static Func<string, string> Translator { get; set; }

        /// <summary>
        /// Двухбуквенный код языка («ru», «en»…), на котором показывать строки. null — язык CurrentUICulture
        /// потока, т. е. язык системы или выбранный приложением.
        /// </summary>
        public static string Language { get; set; }

        /// <summary>Язык, который действует сейчас.</summary>
        public static string CurrentLanguage => Language ?? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

        /// <summary>Добавить или дополнить таблицу перевода языка: английский ключ → перевод.</summary>
        public static void AddTranslations(string language, IDictionary<string, string> translations)
        {
            if (string.IsNullOrEmpty(language)) throw new ArgumentNullException(nameof(language));
            if (translations == null) throw new ArgumentNullException(nameof(translations));
            lock (Sync)
            {
                IDictionary<string, string> table;
                if (!Tables.TryGetValue(language, out table) || ReferenceEquals(table, RussianStrings.Table))
                {
                    table = table == null ? new Dictionary<string, string>() : new Dictionary<string, string>(table);
                    Tables[language] = table;
                }
                foreach (var pair in translations) table[pair.Key] = pair.Value;
            }
        }

        /// <summary>Перевод строки на текущий язык.</summary>
        public static string Translate(string english)
        {
            var custom = Translator?.Invoke(english);
            if (custom != null) return custom;

            lock (Sync)
            {
                IDictionary<string, string> table;
                string translated;
                if (Tables.TryGetValue(CurrentLanguage, out table) && table.TryGetValue(english, out translated)) return translated;
            }
            return english;
        }

        /// <summary>Перевод строки формата и подстановка аргументов в культуре CurrentCulture.</summary>
        public static string Format(string englishFormat, params object[] args) =>
            string.Format(CultureInfo.CurrentCulture, Translate(englishFormat), args);
    }

    /// <summary>Короткая запись для строк самой библиотеки: Tr.T("Save"), Tr.F("Records: {0}", n).</summary>
    internal static class Tr
    {
        public static string T(string english) => NavigatorLocalization.Translate(english);

        public static string F(string englishFormat, params object[] args) => NavigatorLocalization.Format(englishFormat, args);
    }
}

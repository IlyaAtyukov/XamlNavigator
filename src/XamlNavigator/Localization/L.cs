using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace XamlNavigator.Localization
{
    /// <summary>Прежнее имя <see cref="NavigatorLocalization"/> (WpfNavigator в Stronghold).</summary>
    [Obsolete("Use NavigatorLocalization.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class L
    {
        public static Func<string, string> Translator
        {
            get { return NavigatorLocalization.Translator; }
            set { NavigatorLocalization.Translator = value; }
        }

        public static string Language
        {
            get { return NavigatorLocalization.Language; }
            set { NavigatorLocalization.Language = value; }
        }

        public static string CurrentLanguage => NavigatorLocalization.CurrentLanguage;

        public static void AddTranslations(string language, IDictionary<string, string> translations) =>
            NavigatorLocalization.AddTranslations(language, translations);

        public static string T(string english) => NavigatorLocalization.Translate(english);

        public static string F(string englishFormat, params object[] args) => NavigatorLocalization.Format(englishFormat, args);
    }
}

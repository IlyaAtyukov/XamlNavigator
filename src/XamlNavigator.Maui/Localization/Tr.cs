namespace XamlNavigator.Localization
{
    /// <summary>Короткая запись для строк библиотеки: Tr.T("Save"), Tr.F("Records: {0}", n).</summary>
    internal static class Tr
    {
        public static string T(string english) => NavigatorLocalization.Translate(english);

        public static string F(string englishFormat, params object[] args) => NavigatorLocalization.Format(englishFormat, args);
    }
}
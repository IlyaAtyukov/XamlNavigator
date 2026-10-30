using System.Globalization;
using System.Threading;

namespace XamlNavigator.Tests
{
    /// <summary>
    /// Тесты проверяют русские тексты и русский формат чисел, поэтому ставят ru-RU явно — иначе на машине или CI
    /// с английской Windows результат зависел бы от системы.
    /// </summary>
    internal static class TestCulture
    {
        public static void Russian() => Use("ru-RU");

        public static void Use(string name)
        {
            // Только текущему потоку: культура течёт дальше вместе с async-продолжениями, а общие значения по
            // умолчанию задели бы тесты других классов, идущие параллельно.
            var culture = new CultureInfo(name);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }
    }
}

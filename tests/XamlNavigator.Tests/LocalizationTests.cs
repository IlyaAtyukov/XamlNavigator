using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using Xunit;

namespace XamlNavigator.Tests
{
    public class LocalizationTests
    {
        private static readonly Regex KeyCall = new Regex(@"\b(?:Tr\.T|Tr\.F|Text)\(\s*""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XamlNavigator.sln"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir.FullName;
        }

        /// <summary>Все ключи перевода в исходниках библиотеки: литералы в Tr.T(…), Tr.F(…) и Text(…) (шаблоны WPF).</summary>
        private static Dictionary<string, string> KeysInSources()
        {
            var keys = new Dictionary<string, string>();
            foreach (var file in Directory.GetFiles(Path.Combine(RepoRoot(), "src"), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar)) continue;
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                    foreach (Match m in KeyCall.Matches(lines[i]))
                    {
                        var key = Regex.Unescape(m.Groups[1].Value);
                        if (!keys.ContainsKey(key)) keys[key] = Path.GetFileName(file) + ":" + (i + 1);
                    }
            }
            return keys;
        }

        [Fact]
        public void Every_library_string_has_a_russian_translation_and_no_translation_is_orphaned()
        {
            var keys = KeysInSources();
            Assert.True(keys.Count > 100, "ключей подозрительно мало: " + keys.Count);

            var missing = keys.Where(k => !RussianStrings.Table.ContainsKey(k.Key)).Select(k => k.Value + "  " + k.Key).ToList();
            Assert.True(missing.Count == 0, "Нет русского перевода:\n" + string.Join("\n", missing));

            var orphaned = RussianStrings.Table.Keys.Where(k => !keys.ContainsKey(k)).ToList();
            Assert.True(orphaned.Count == 0, "Перевод без строки в коде:\n" + string.Join("\n", orphaned));
        }

        [Fact]
        public void Keys_are_english_and_placeholders_survive_translation()
        {
            var cyrillic = new Regex("[Ѐ-ӿ]");
            var placeholder = new Regex(@"\{\d+(?:[:,][^}]*)?\}");
            foreach (var pair in RussianStrings.Table)
            {
                Assert.False(cyrillic.IsMatch(pair.Key), "Ключ не на английском: " + pair.Key);
                var expected = placeholder.Matches(pair.Key).Cast<Match>().Select(m => m.Value).OrderBy(v => v);
                var actual = placeholder.Matches(pair.Value).Cast<Match>().Select(m => m.Value).OrderBy(v => v);
                Assert.True(expected.SequenceEqual(actual), "Плейсхолдеры не совпадают: " + pair.Key + " → " + pair.Value);
            }
        }

        [Fact]
        public void English_is_the_default_and_russian_follows_the_ui_culture()
        {
            var key = RussianStrings.Table.Keys.First();
            TestCulture.Use("en-US");
            try { Assert.Equal(key, NavigatorLocalization.Translate(key)); }
            finally { TestCulture.Russian(); }
            Assert.Equal(RussianStrings.Table[key], NavigatorLocalization.Translate(key));
        }

        [Fact]
        public void Validation_messages_read_in_english_under_english_culture()
        {
            TestCulture.Use("en-US");
            try
            {
                string error;
                Assert.False(new FieldValidationModel { Min = 2 }.IsSatisfiedBy("A", FieldType.Text, out error));
                Assert.DoesNotMatch("[Ѐ-ӿ]", error);
                Assert.Contains("2", error);
            }
            finally { TestCulture.Russian(); }
        }

        [Fact]
        public void Custom_translator_comes_first_and_null_falls_back_to_built_in()
        {
            var key = RussianStrings.Table.Keys.First();
            var previous = NavigatorLocalization.Translator;
            try
            {
                NavigatorLocalization.Translator = k => k == key ? "custom" : null;
                Assert.Equal("custom", NavigatorLocalization.Translate(key));
                var other = RussianStrings.Table.Keys.Skip(1).First();
                Assert.Equal(RussianStrings.Table[other], NavigatorLocalization.Translate(other));
            }
            finally { NavigatorLocalization.Translator = previous; }
        }
    }
}

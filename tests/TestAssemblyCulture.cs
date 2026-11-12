using System.Globalization;
using System.Runtime.CompilerServices;

namespace XamlNavigator.Tests
{
    /// <summary>
    /// Базовая культура тестовых сборок — en-US, как на сервере сборки, а не язык этой машины. Тест, которому нужен
    /// русский, ставит его сам (TestCulture.Russian), поэтому скрытая зависимость от русской Windows падает сразу
    /// и здесь, а не только в CI.
    /// </summary>
    internal static class TestAssemblyCulture
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            var culture = new CultureInfo("en-US");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }
    }
}

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = false)]
    internal sealed class ModuleInitializerAttribute : System.Attribute { }
}
#endif

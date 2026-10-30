using System;
using System.Linq;
using System.Reflection;
using XamlNavigator.Localization;

namespace XamlNavigator.Platform
{
    /// <summary>
    /// Помечает сборку платформы (XamlNavigator.Wpf, XamlNavigator.Maui) и тип с методом
    /// <c>public static void Install()</c>, который заполняет <see cref="NavigatorPlatform"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class NavigatorPlatformAttribute : Attribute
    {
        public NavigatorPlatformAttribute(Type installerType) => InstallerType = installerType;

        public Type InstallerType { get; }
    }

    /// <summary>
    /// Точки, в которых ядро обращается к UI. Заполняются один раз при запуске — вызовом
    /// <c>WpfPlatform.Install()</c> / <c>MauiPlatform.Install()</c> или автоматически при
    /// <c>NavigatorBuilder.Build()</c>. В одном процессе работает одна платформа.
    /// </summary>
    public static class NavigatorPlatform
    {
        private static readonly string[] KnownPlatforms = { "XamlNavigator.Wpf", "XamlNavigator.Maui" };
        private static readonly object Sync = new object();

        private static IUiDispatcher _dispatcher = InlineDispatcher.Instance;
        private static ICommandRequery _commandRequery = new ExplicitCommandRequery();
        private static IRecordsViewFactory _recordsViews = FilteredRecordsViewFactory.Instance;

        public static string Name { get; private set; }

        public static IUiDispatcher Dispatcher
        {
            get { return _dispatcher; }
            set { _dispatcher = value ?? InlineDispatcher.Instance; }
        }

        public static ICommandRequery CommandRequery
        {
            get { return _commandRequery; }
            set
            {
                var previous = _commandRequery;
                _commandRequery = value ?? new ExplicitCommandRequery();
                // Кнопки, привязанные до подключения платформы, не должны потерять перепроверку команд.
                var explicitPrevious = previous as ExplicitCommandRequery;
                if (explicitPrevious != null && !ReferenceEquals(previous, _commandRequery)) explicitPrevious.TransferTo(_commandRequery);
            }
        }

        /// <summary>Фабрика отфильтрованного списка записей динамической страницы.</summary>
        public static IRecordsViewFactory RecordsViewFactory
        {
            get { return _recordsViews; }
            set { _recordsViews = value ?? FilteredRecordsViewFactory.Instance; }
        }

        /// <summary>Операции с представлениями платформы: создание, привязка, показ оболочки.</summary>
        public static IViewAdapter ViewAdapter { get; set; }

        /// <summary>Печать списков; null — печати нет.</summary>
        public static IReportPrinter Printer { get; set; }

        /// <summary>Поле «Фото»; null — фото показываются только именем файла.</summary>
        public static IPhotoService PhotoService { get; set; }

        /// <summary>Фабрика динамических страниц по умолчанию, если приложение не передало свою.</summary>
        public static Func<Abstractions.IDynamicViewFactory> DefaultDynamicViewFactory { get; set; }

        /// <summary>Очищать ли историю «назад» при переходе по меню, если в NavigatorBuilder не сказано иное.</summary>
        public static bool ClearHistoryOnMenuNavigationByDefault { get; set; }

        public static bool IsInstalled => ViewAdapter != null;

        /// <summary>Отметить, что платформа подключена (вызывается из Install платформы).</summary>
        public static void MarkInstalled(string name) => Name = name;

        /// <summary>
        /// Подключить платформу, если это ещё не сделано: ищет среди сборок приложения
        /// XamlNavigator.Wpf или XamlNavigator.Maui и вызывает их Install.
        /// </summary>
        public static void EnsureInstalled()
        {
            if (TryEnsureInstalled()) return;

            throw new InvalidOperationException(Tr.T("The navigator platform is not installed: add the XamlNavigator.Wpf or XamlNavigator.Maui package and call WpfPlatform.Install() / MauiPlatform.Install() at startup."));
        }

        /// <summary>Как <see cref="EnsureInstalled"/>, но без исключения: false — платформы среди сборок приложения нет.</summary>
        public static bool TryEnsureInstalled()
        {
            if (IsInstalled) return true;
            lock (Sync)
            {
                if (IsInstalled) return true;

                foreach (var assembly in CandidateAssemblies())
                {
                    var attr = assembly.GetCustomAttribute<NavigatorPlatformAttribute>();
                    var install = attr?.InstallerType.GetMethod("Install", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                    if (install == null) continue;
                    try { install.Invoke(null, null); }
                    catch (TargetInvocationException) { continue; }
                    if (IsInstalled) return true;
                }
                return false;
            }
        }

        private static System.Collections.Generic.IEnumerable<Assembly> CandidateAssemblies()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var name in KnownPlatforms)
            {
                var assembly = loaded.FirstOrDefault(a => a.GetName().Name == name);
                if (assembly == null)
                {
                    try { assembly = Assembly.Load(new AssemblyName(name)); }
                    catch (Exception) { /* платформа не установлена */ }
                }
                if (assembly != null) yield return assembly;
            }
        }
    }
}

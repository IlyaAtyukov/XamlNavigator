using System;
using System.Collections.Generic;
using System.Reflection;

namespace XamlNavigator.Services
{
    /// <summary>
    /// Соглашение об именах: представление модели — тот же полный путь с «View» вместо «ViewModel»
    /// (MyApp.ViewModels.OrdersViewModel → MyApp.Views.OrdersView). Ищется в сборке модели, затем в
    /// сборках из <see cref="AddViewAssembly"/> — так модели можно держать в общей библиотеке для WPF и MAUI.
    /// </summary>
    public static class ViewLocator
    {
        private static readonly object Sync = new object();
        private static readonly List<Assembly> ViewAssemblies = new List<Assembly>();

        /// <summary>Своё соглашение вместо стандартного; null — стандартное.</summary>
        public static Func<Type, Type> Resolver { get; set; }

        /// <summary>Искать представления ещё и в этой сборке (обычно — в сборке приложения).</summary>
        public static void AddViewAssembly(Assembly assembly)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));
            lock (Sync) if (!ViewAssemblies.Contains(assembly)) ViewAssemblies.Add(assembly);
        }

        public static Type ViewTypeFor(Type viewModelType)
        {
            if (viewModelType == null) return null;
            var custom = Resolver?.Invoke(viewModelType);
            if (custom != null) return custom;

            var viewTypeName = viewModelType.FullName?.Replace("ViewModel", "View");
            if (viewTypeName == null) return null;

            var own = viewModelType.Assembly.GetType(viewTypeName);
            if (own != null) return own;

            lock (Sync)
            {
                foreach (var assembly in ViewAssemblies)
                {
                    var found = assembly.GetType(viewTypeName);
                    if (found != null) return found;
                }
            }
            return null;
        }
    }
}

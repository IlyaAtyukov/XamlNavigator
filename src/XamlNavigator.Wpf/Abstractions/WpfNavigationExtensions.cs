using System.Threading.Tasks;
using XamlNavigator.Core;

namespace XamlNavigator.Abstractions
{
    public static class WpfNavigationExtensions
    {
        /// <summary>
        /// Создать окно для <typeparamref name="TViewModel"/>, сделать его главным, показать и закрыть прежнее.
        /// false — не получилось, причина в NavigationFailed.
        /// </summary>
        public static Task<bool> OpenWindowAsync<TViewModel>(this INavigationOrchestrator orchestrator) where TViewModel : BaseShellViewModel =>
            orchestrator.OpenAsync<TViewModel>();
    }
}

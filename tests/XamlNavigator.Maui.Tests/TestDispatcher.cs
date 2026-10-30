using System.Runtime.CompilerServices;
using Microsoft.Maui.Dispatching;

namespace XamlNavigator.Maui.Tests
{
    /// <summary>
    /// Без запущенного приложения у BindableObject нет диспетчера, и любая сработавшая привязка бросает
    /// исключение. Для тестов — диспетчер, который выполняет всё на месте, для любого потока.
    /// </summary>
    internal sealed class InlineDispatcher : IDispatcher, IDispatcherProvider
    {
        [ModuleInitializer]
        internal static void Install() => DispatcherProvider.SetCurrent(new InlineDispatcher());

        public IDispatcher GetForCurrentThread() => this;

        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action)
        {
            action();
            return true;
        }

        public IDispatcherTimer CreateTimer() => throw new NotSupportedException();
    }
}

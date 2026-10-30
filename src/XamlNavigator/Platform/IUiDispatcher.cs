using System;
using System.Threading.Tasks;

namespace XamlNavigator.Platform
{
    /// <summary>Поток интерфейса: Dispatcher в WPF, IDispatcher в MAUI.</summary>
    public interface IUiDispatcher
    {
        /// <summary>Вызов уже идёт в потоке интерфейса (или потока интерфейса нет вовсе).</summary>
        bool CheckAccess();

        /// <summary>Выполнить в потоке интерфейса и дождаться.</summary>
        void Invoke(Action action);

        Task InvokeAsync(Action action);

        /// <summary>Выполнить в потоке интерфейса; задача завершается вместе с задачей <paramref name="action"/>.</summary>
        Task InvokeAsync(Func<Task> action);
    }

    /// <summary>
    /// Выполняет всё на месте. Используется, пока платформа не подключена, и в тестах без приложения.
    /// </summary>
    public sealed class InlineDispatcher : IUiDispatcher
    {
        public static readonly InlineDispatcher Instance = new InlineDispatcher();

        public bool CheckAccess() => true;

        public void Invoke(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public Task InvokeAsync(Func<Task> action) => action();
    }
}

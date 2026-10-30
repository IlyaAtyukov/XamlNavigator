using System;
using System.Collections.Generic;
using XamlNavigator.Core;

namespace XamlNavigator.Platform
{
    /// <summary>
    /// Как команды узнают, что их доступность пора перепроверить. В WPF это CommandManager.RequerySuggested
    /// (перепроверка после любого ввода); в MAUI глобального механизма нет — см. <see cref="ExplicitCommandRequery"/>.
    /// </summary>
    public interface ICommandRequery
    {
        void Subscribe(RelayCommand command, EventHandler handler);
        void Unsubscribe(RelayCommand command, EventHandler handler);

        /// <summary>Перепроверить одну команду.</summary>
        void Invalidate(RelayCommand command);

        /// <summary>Перепроверить все живые команды.</summary>
        void InvalidateAll();
    }

    /// <summary>
    /// Без CommandManager: подписчики хранятся в самой команде, а <see cref="InvalidateAll"/> обходит
    /// все ещё живые команды (слабые ссылки), поэтому RelayCommand.InvalidateRequerySuggested() работает
    /// так же, как в WPF.
    /// </summary>
    public sealed class ExplicitCommandRequery : ICommandRequery
    {
        private readonly object _sync = new object();
        private readonly List<WeakReference<RelayCommand>> _commands = new List<WeakReference<RelayCommand>>();
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<RelayCommand, HandlerList> _handlers =
            new System.Runtime.CompilerServices.ConditionalWeakTable<RelayCommand, HandlerList>();

        private sealed class HandlerList
        {
            public EventHandler Handlers;
        }

        public void Subscribe(RelayCommand command, EventHandler handler)
        {
            lock (_sync)
            {
                HandlerList list;
                if (!_handlers.TryGetValue(command, out list))
                {
                    list = new HandlerList();
                    _handlers.Add(command, list);
                    // Список слабых ссылок растёт с каждой новой командой — время от времени выкидываем умершие.
                    if (_commands.Count >= _pruneAt)
                    {
                        _commands.RemoveAll(w => { RelayCommand alive; return !w.TryGetTarget(out alive); });
                        _pruneAt = Math.Max(64, _commands.Count * 2);
                    }
                    _commands.Add(new WeakReference<RelayCommand>(command));
                }
                list.Handlers += handler;
            }
        }

        private int _pruneAt = 64;

        /// <summary>
        /// Передать все живые подписки другому механизму — когда платформа подключается после того, как
        /// кнопки уже привязались к командам.
        /// </summary>
        public void TransferTo(ICommandRequery target)
        {
            var moved = new List<KeyValuePair<RelayCommand, EventHandler>>();
            lock (_sync)
            {
                foreach (var weak in _commands)
                {
                    RelayCommand command;
                    HandlerList list;
                    if (!weak.TryGetTarget(out command) || !_handlers.TryGetValue(command, out list) || list.Handlers == null) continue;
                    foreach (var handler in list.Handlers.GetInvocationList())
                        moved.Add(new KeyValuePair<RelayCommand, EventHandler>(command, (EventHandler)handler));
                    list.Handlers = null;
                }
                _commands.Clear();
            }
            foreach (var pair in moved) target.Subscribe(pair.Key, pair.Value);
        }

        public void Unsubscribe(RelayCommand command, EventHandler handler)
        {
            lock (_sync)
            {
                HandlerList list;
                if (_handlers.TryGetValue(command, out list)) list.Handlers -= handler;
            }
        }

        public void Invalidate(RelayCommand command)
        {
            EventHandler handlers;
            lock (_sync)
            {
                HandlerList list;
                handlers = _handlers.TryGetValue(command, out list) ? list.Handlers : null;
            }
            if (handlers == null) return;
            NavigatorPlatform.Dispatcher.InvokeAsync(() => handlers(command, EventArgs.Empty));
        }

        public void InvalidateAll()
        {
            var alive = new List<RelayCommand>();
            lock (_sync)
            {
                for (var i = _commands.Count - 1; i >= 0; i--)
                {
                    RelayCommand command;
                    if (_commands[i].TryGetTarget(out command)) alive.Add(command);
                    else _commands.RemoveAt(i);
                }
            }
            foreach (var command in alive) Invalidate(command);
        }
    }
}

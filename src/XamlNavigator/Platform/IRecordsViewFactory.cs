using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace XamlNavigator.Platform
{
    /// <summary>Отфильтрованный список записей динамической страницы, к которому привязан список на экране.</summary>
    public interface IRecordsView
    {
        /// <summary>То, что отдаётся в ItemsSource.</summary>
        IEnumerable Source { get; }

        /// <summary>Заново применить фильтр.</summary>
        void Refresh();

        /// <summary>Записи, прошедшие фильтр, в порядке показа.</summary>
        IList<object> GetVisibleRecords();
    }

    public interface IRecordsViewFactory
    {
        IRecordsView Create(IList<object> records, Predicate<object> filter);
    }

    /// <summary>
    /// Без ICollectionView: ObservableCollection, которая при <see cref="IRecordsView.Refresh"/> пересобирается по фильтру.
    /// Используется в MAUI и в тестах.
    /// </summary>
    public sealed class FilteredRecordsViewFactory : IRecordsViewFactory
    {
        public static readonly FilteredRecordsViewFactory Instance = new FilteredRecordsViewFactory();

        public IRecordsView Create(IList<object> records, Predicate<object> filter) => new FilteredRecordsView(records, filter);

        private sealed class FilteredRecordsView : IRecordsView
        {
            private readonly IList<object> _records;
            private readonly Predicate<object> _filter;
            private readonly ObservableCollection<object> _visible = new ObservableCollection<object>();

            public FilteredRecordsView(IList<object> records, Predicate<object> filter)
            {
                _records = records;
                _filter = filter;
                Refresh();
            }

            public IEnumerable Source => _visible;

            public void Refresh()
            {
                var next = _records.Where(r => _filter == null || _filter(r)).ToList();
                if (next.SequenceEqual(_visible)) return;

                _visible.Clear();
                foreach (var record in next) _visible.Add(record);
            }

            public IList<object> GetVisibleRecords() => _visible.ToList();
        }
    }
}

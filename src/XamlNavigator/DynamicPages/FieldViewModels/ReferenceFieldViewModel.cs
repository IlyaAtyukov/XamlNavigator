using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class ReferenceFieldViewModel : FieldViewModel
    {
        // Ключ записи, которой нет в загруженном справочнике (не загрузился или отфильтрован): сохраняется как есть.
        private long? _unlistedId;

        public ReferenceFieldViewModel(FieldDefinitionModel field, string titleProperty) : base(field)
        {
            TitleProperty = titleProperty;
        }

        /// <summary>Свойство записи справочника, которое показывается как её название (FieldDefinitionModel.ReferenceTitleProperty).</summary>
        public string TitleProperty { get; }

        /// <summary>Прежнее имя <see cref="TitleProperty"/>; текст варианта в списке — <see cref="ReferenceListItem.Display"/>.</summary>
        [Obsolete("Use TitleProperty.")]
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public string DisplayMember => TitleProperty;

        private List<ReferenceListItem> _items = new List<ReferenceListItem>();
        public List<ReferenceListItem> Items
        {
            get { return _items; }
            set
            {
                var keep = Selected?.Id ?? _unlistedId;
                // Сначала снять выбор: иначе список выбора держал бы старый экземпляр со старым названием.
                _selected = null;
                OnPropertyChanged(nameof(Selected));
                _items = value ?? new List<ReferenceListItem>();
                OnPropertyChanged(nameof(Items));
                // Справочник перечитан: выбрать ту же запись уже из нового списка.
                if (keep.HasValue) Select(keep.Value);
            }
        }

        private ReferenceListItem _selected;
        public ReferenceListItem Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                _unlistedId = null;
                OnPropertyChanged(nameof(Selected));
            }
        }

        public override object GetValue() => Selected?.Id ?? _unlistedId;

        public override void Reset() => Selected = null;

        public override void SetValue(object value)
        {
            if (value == null) { Selected = null; return; }
            Select(Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }

        private void Select(long id)
        {
            var item = Items.FirstOrDefault(it => it.Id == id);
            Selected = item;
            // 0 у ненулевого ключа значит «не выбрано» — его не сохраняем как «запись вне справочника».
            if (item == null && id != 0) _unlistedId = id;
        }
    }
}

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    /// <summary>
    /// Вариант поля-ссылки. После перечитывания справочника выбор восстанавливается по <see cref="Id"/>
    /// (ReferenceFieldViewModel.Items), поэтому равенство здесь обычное, по ссылке.
    /// </summary>
    public class ReferenceListItem
    {
        public ReferenceListItem(long id, string display)
        {
            Id = id;
            Display = display;
        }

        public long Id { get; }
        public string Display { get; }
        public override string ToString() => Display;
    }
}

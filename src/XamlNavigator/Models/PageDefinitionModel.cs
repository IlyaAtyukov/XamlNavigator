namespace XamlNavigator.Models
{
    public class PageDefinitionModel
    {
        public long Id { get; set; }
        public long PairedPageId { get; set; }
        public string Title { get; set; } = "";
        public PageDisplayMode DisplayMode { get; set; } = PageDisplayMode.Edit;
        public ListViewMode ListViewMode { get; set; } = ListViewMode.Table;
        public string CustomViewKey { get; set; }
        public string ModelTypeName { get; set; }
        public string TableName { get; set; } = "";
        public string CustomViewBindingsJson { get; set; }
        public int RequiredAccessLevel { get; set; }
        public HighlightRuleModel Highlight { get; set; }
        public PageOperations Operations { get; set; } = PageOperations.All;
    }
}
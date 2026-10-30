using System.Collections.Generic;

namespace XamlNavigator.Platform
{
    public class ReportColumn
    {
        public ReportColumn(string header, bool numeric)
        {
            Header = header;
            Numeric = numeric;
        }

        public string Header { get; }
        public bool Numeric { get; }
    }

    /// <summary>Таблица для печати: то, что сейчас видно в списке динамической страницы.</summary>
    public class Report
    {
        public Report(string title, string subtitle, IList<ReportColumn> columns, IList<IList<string>> rows)
        {
            Title = title;
            Subtitle = subtitle;
            Columns = columns;
            Rows = rows;
        }

        public string Title { get; }
        public string Subtitle { get; }
        public IList<ReportColumn> Columns { get; }
        public IList<IList<string>> Rows { get; }
    }

    /// <summary>Печать списка. В WPF — через PrintDialog; где печати нет, кнопка «Печать» не показывается.</summary>
    public interface IReportPrinter
    {
        /// <summary>Напечатать; false — пользователь передумал.</summary>
        bool Print(Report report);
    }
}

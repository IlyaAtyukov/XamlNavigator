using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using XamlNavigator.Localization;
using XamlNavigator.Platform;

namespace XamlNavigator.DynamicPages
{
    /// <summary>FlowDocument для печати списка динамической страницы.</summary>
    public static class ReportBuilder
    {
        public static FlowDocument Build(Report report) => Build(report.Title, report.Subtitle, report.Columns, report.Rows);

        public static FlowDocument Build(string title, string subtitle, IList<ReportColumn> columns, IList<IList<string>> rows)
        {
            var document = new FlowDocument
            {
                FontFamily = ResolveFont(),
                FontSize = 12,
                PagePadding = new Thickness(50),
                ColumnGap = 0,
                Foreground = Brushes.Black
            };

            document.Blocks.Add(new Paragraph(new Run(title ?? ""))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 4)
            });

            var stamp = Tr.F("Printed: {0}", DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.CurrentCulture));
            if (!string.IsNullOrWhiteSpace(subtitle)) stamp = subtitle + ".   " + stamp;

            document.Blocks.Add(new Paragraph(new Run(stamp))
            {
                FontSize = 11,
                Foreground = Brushes.DimGray,
                Margin = new Thickness(0, 0, 0, 14)
            });

            document.Blocks.Add(BuildTable(columns, rows));

            document.Blocks.Add(new Paragraph(new Run(Tr.F("Total records: {0}", rows.Count)))
            {
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 12, 0, 0)
            });

            return document;
        }

        private static Table BuildTable(IList<ReportColumn> columns, IList<IList<string>> rows)
        {
            var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0) };

            foreach (var column in columns)
                table.Columns.Add(new TableColumn { Width = new GridLength(1, GridUnitType.Star) });

            var header = new TableRowGroup();
            var headerRow = new TableRow();
            foreach (var column in columns) headerRow.Cells.Add(HeaderCell(column));
            header.Rows.Add(headerRow);
            table.RowGroups.Add(header);

            var body = new TableRowGroup();
            for (var i = 0; i < rows.Count; i++)
            {
                var row = new TableRow();
                if (i % 2 == 1) row.Background = new SolidColorBrush(Color.FromRgb(0xF2, 0xF2, 0xF2));

                for (var c = 0; c < columns.Count; c++)
                {
                    var text = c < rows[i].Count ? rows[i][c] : "";
                    row.Cells.Add(BodyCell(text, columns[c].Numeric));
                }
                body.Rows.Add(row);
            }

            if (rows.Count == 0)
            {
                var empty = new TableRow();
                empty.Cells.Add(new TableCell(new Paragraph(new Run(Tr.T("No records")))
                {
                    FontStyle = FontStyles.Italic,
                    Foreground = Brushes.DimGray,
                    Margin = new Thickness(6, 6, 6, 6)
                })
                { ColumnSpan = Math.Max(columns.Count, 1) });
                body.Rows.Add(empty);
            }

            table.RowGroups.Add(body);
            return table;
        }

        private static TableCell HeaderCell(ReportColumn column) => new TableCell(
            new Paragraph(new Run(column.Header ?? ""))
            {
                FontWeight = FontWeights.Bold,
                TextAlignment = column.Numeric ? TextAlignment.Right : TextAlignment.Left,
                Margin = new Thickness(6, 5, 6, 5)
            })
        {
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        private static TableCell BodyCell(string text, bool numeric) => new TableCell(
            new Paragraph(new Run(text ?? ""))
            {
                TextAlignment = numeric ? TextAlignment.Right : TextAlignment.Left,
                Margin = new Thickness(6, 4, 6, 4)
            })
        {
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(0, 0, 0, 1)
        };

        private static FontFamily ResolveFont()
        {
            var app = Application.Current;
            var font = app?.TryFindResource("AppFont") as FontFamily;
            return font ?? new FontFamily("Segoe UI");
        }
    }
}
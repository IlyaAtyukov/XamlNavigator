using System;

namespace XamlNavigator.Samples.Models
{
    public class Category
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public double? ProductsCount { get; set; }
    }

    public class Product
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public long? CategoryId { get; set; }
        public Category Category { get; set; }
        public decimal Price { get; set; }
        public int Quantity { get; set; }
        public double? Total { get; set; }
        public double? Discount { get; set; }
        public DateTime? ArrivedAt { get; set; }
        public bool InStock { get; set; }
        public string Unit { get; set; }
        public string Description { get; set; }
        public string Photo { get; set; }
    }
}

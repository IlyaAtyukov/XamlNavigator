using System.Linq;
using Newtonsoft.Json;
using XamlNavigator.Attributes;
using XamlNavigator.Models;
using XamlNavigator.Services;
using XamlNavigator.Tests.Nav.ViewModels;
using Xunit;

#pragma warning disable CS0618 // здесь нарочно проверяются устаревшие имена

namespace XamlNavigator.Tests
{
    /// <summary>Прежние имена (WpfNavigator, MauiNavigator, Stronghold) продолжают работать после переименования.</summary>
    public class CompatibilityTests
    {
        public CompatibilityTests() => TestPlatform.Install();

        [Fact]
        public void Field_definition_keeps_its_json_names()
        {
            var field = new FieldDefinitionModel { DisplayName = "Цена", OptionsJson = "[\"a\"]" };

            var json = JsonConvert.SerializeObject(field);
            Assert.Contains("\"FieldName\":\"Цена\"", json);
            Assert.Contains("\"Options\":\"[\\\"a\\\"]\"", json);
            Assert.DoesNotContain("DisplayName", json);

            var restored = JsonConvert.DeserializeObject<FieldDefinitionModel>("{\"FieldName\":\"Сумма\",\"Options\":\"[]\",\"FieldType\":5}");
            Assert.Equal("Сумма", restored.DisplayName);
            Assert.Equal("Сумма", restored.FieldName);
            Assert.Equal("[]", restored.OptionsJson);
            Assert.Equal(FieldType.Choice, restored.FieldType);
            Assert.Equal(FieldType.List, FieldType.Choice);
        }

        [Fact]
        public void Old_attribute_and_menu_names_map_to_the_new_ones()
        {
            var attr = new ChildOfAttribute(typeof(HomeViewModel)) { Visible = false };
            Assert.False(attr.ShowInMenu);

            var item = new NavigationItem { Tag = 42L };
            Assert.Equal(42L, item.DynamicPageId);

            var cluster = new NavigationCluster { Index = 3 };
            Assert.Equal(3, cluster.Number);

            var custom = new CustomViewField("Caption", "Подпись", typeof(string), "dp");
            Assert.Same("dp", custom.DependencyProperty);
            Assert.Same(custom, new CustomViewInfo(typeof(object), "x", new[] { custom }).Fields.Single());
        }

        [Fact]
        public void First_menu_group_is_marked_for_the_separator()
        {
            var nav = NavigatorBuilder.Create(new Provider().Add(new Dependency())).ScanAssembly(typeof(CompatibilityTests).Assembly).Build();
            var home = (HomeViewModel)((FakeView)nav.CreateRootView<HomeViewModel>()).BindingContext;

            Assert.True(home.NavigationClusters.First().IsFirst);
            Assert.All(home.NavigationClusters.Skip(1), c => Assert.False(c.IsFirst));

            home.AddDynamicNavigationItem(1, "Раньше всех", new Core.RelayCommand(_ => { }), cluster: -1);
            Assert.Equal(-1, home.NavigationClusters.First().Number);
            Assert.True(home.NavigationClusters.First().IsFirst);
            Assert.Equal(1, home.NavigationClusters.Count(c => c.IsFirst));
        }
    }
}

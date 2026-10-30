using System.Windows;
using System.Windows.Markup;
using XamlNavigator.Platform;

[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]
[assembly: NavigatorPlatform(typeof(WpfPlatform))]

[assembly: XmlnsDefinition("https://github.com/IlyaAtyukov/XamlNavigator", "XamlNavigator.Services")]
[assembly: XmlnsDefinition("https://github.com/IlyaAtyukov/XamlNavigator", "XamlNavigator.DynamicPages")]
[assembly: XmlnsPrefix("https://github.com/IlyaAtyukov/XamlNavigator", "nav")]

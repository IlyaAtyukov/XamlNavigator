using System;

namespace XamlNavigator.Abstractions
{
    /// <summary>Мост к IServiceProvider (Microsoft.Extensions.DependencyInjection и любой другой контейнер).</summary>
    public class ServiceProviderAdapter : INavigationServiceProvider
    {
        private readonly IServiceProvider _provider;

        public ServiceProviderAdapter(IServiceProvider provider)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public object GetService(Type serviceType) => _provider.GetService(serviceType);

        public T GetService<T>() where T : class => GetService(typeof(T)) as T;
    }
}

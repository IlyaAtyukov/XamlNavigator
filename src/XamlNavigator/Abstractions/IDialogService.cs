using System.Threading.Tasks;
using XamlNavigator.Core;

namespace XamlNavigator.Abstractions
{
    public interface IDialogService
    {
        Task<bool?> ShowAsync(BaseDialogViewModel viewModel);
        Task<bool?> ShowAsync<TViewModel>() where TViewModel : BaseDialogViewModel;
    }
}

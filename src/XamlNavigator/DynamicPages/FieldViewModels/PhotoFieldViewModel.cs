using System.Threading.Tasks;
using XamlNavigator.Core;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.DynamicPages.FieldViewModels
{
    public class PhotoFieldViewModel : FieldViewModel
    {
        private const int PreviewHeight = 190;

        private string _value = "";
        private string _problem = "";

        public PhotoFieldViewModel(FieldDefinitionModel field) : base(field) { }

        public string Value
        {
            get { return _value; }
            set
            {
                _value = value ?? "";
                _problem = "";
                Refresh();
            }
        }

        /// <summary>Картинка платформы (ImageSource) или null.</summary>
        public object Preview
        {
            get
            {
                var path = PhotoStore.Resolve(Value);
                return path == null ? null : NavigatorPlatform.PhotoService?.LoadImage(path, PreviewHeight);
            }
        }

        public bool HasPhoto => Preview != null;
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);
        public bool ShowPlaceholder => !HasPhoto;
        public bool CanOpenFolder => PhotoStore.CanOpenFolder;

        public string Status
        {
            get
            {
                if (!string.IsNullOrEmpty(_problem)) return _problem;
                if (IsEmpty) return Tr.T("No photo selected");
                if (HasPhoto) return Tr.F("File \"{0}\"", Value);
                if (PhotoStore.Exists(Value)) return Tr.F("Couldn't open file \"{0}\" — is it an image?", Value);
                return Tr.F("File \"{0}\" not found in the \"{1}\" folder", Value, PhotoStore.FolderName);
            }
        }

        private RelayCommand _pickCommand;
        public RelayCommand PickCommand => _pickCommand ?? (_pickCommand = new RelayCommand(_ => PickAsync(), _ => NavigatorPlatform.PhotoService != null));

        private RelayCommand _clearCommand;
        public RelayCommand ClearCommand => _clearCommand ?? (_clearCommand = new RelayCommand(_ => Value = "", _ => !IsEmpty));

        private RelayCommand _openFolderCommand;
        public RelayCommand OpenFolderCommand => _openFolderCommand ?? (_openFolderCommand = new RelayCommand(_ => PhotoStore.OpenFolder(), _ => PhotoStore.CanOpenFolder));

        private async Task PickAsync()
        {
            var photos = NavigatorPlatform.PhotoService;
            if (photos == null) return;

            PhotoStore.EnsureFolder();
            var picked = await photos.PickImageAsync(PhotoStore.FolderPath);
            if (string.IsNullOrEmpty(picked)) return;

            string error;
            var saved = PhotoStore.Save(picked, out error);
            if (saved == null)
            {
                _problem = error;
                Refresh();
                return;
            }

            Value = saved;
            if (!HasPhoto)
            {
                _problem = Tr.F("Couldn't open file \"{0}\" as an image.", saved);
                Refresh();
            }
        }

        private void Refresh()
        {
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(Preview));
            OnPropertyChanged(nameof(HasPhoto));
            OnPropertyChanged(nameof(ShowPlaceholder));
            OnPropertyChanged(nameof(IsEmpty));
            OnPropertyChanged(nameof(Status));
            _clearCommand?.RaiseCanExecuteChanged();
        }

        public override object GetValue() => IsEmpty ? null : Value;

        public override void SetValue(object value) => Value = value?.ToString() ?? "";

        public override void Reset() => Value = "";
    }
}

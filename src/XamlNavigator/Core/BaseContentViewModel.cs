using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using XamlNavigator.Attributes;
using XamlNavigator.Localization;
using XamlNavigator.Models;
using XamlNavigator.Platform;

namespace XamlNavigator.Core
{
    /// <summary>
    /// Модель экрана-содержимого внутри оболочки (UserControl в WPF, ContentView в MAUI).
    /// Имя в меню — из <see cref="ChildOfAttribute"/>.
    /// </summary>
    public abstract class BaseContentViewModel : BaseViewModel
    {
        private object _param;
        private string _errorMessage;

        public sealed override string DisplayName { get; set; }

        protected BaseContentViewModel()
        {
            var attr = GetType().GetCustomAttribute<ChildOfAttribute>();
            DisplayName = attr?.DisplayName ?? GetType().Name;
        }

        /// <summary>Не используется меню: иконку пункта задаёт [ChildOf(IconKey = …)].</summary>
        [Obsolete("Not used by the menu; set [ChildOf(IconKey = ...)].")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public virtual string NavigationIcon => GetType().GetCustomAttribute<ChildOfAttribute>()?.IconKey;

        /// <summary>Оболочка (окно WPF, страница MAUI), внутри которой показан экран.</summary>
        public BaseShellViewModel ParentShell { get; private set; }

        /// <summary>Прежнее имя <see cref="ParentShell"/>.</summary>
        [Obsolete("Use ParentShell.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public BaseShellViewModel ParentPage => ParentShell;

        public string ErrorMessage
        {
            get { return _errorMessage; }
            set
            {
                if (value == _errorMessage) return;
                _errorMessage = value;
                OnPropertyChanged(nameof(ErrorMessage));
            }
        }

        internal void SetParentShell(BaseShellViewModel parent) => ParentShell = parent;

        /// <summary>Прежнее имя; оболочку экрану назначает навигатор сам.</summary>
        [Obsolete("The navigator sets the parent shell itself.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetParentPage(BaseShellViewModel parent) => SetParentShell(parent);

        /// <summary>Запрещён ли переход; об отказе оболочка сообщает в NavigationFailed, как оркестратор.</summary>
        private bool IsDenied(Type targetViewModelType)
        {
            var policy = ParentShell?.AccessPolicy;
            if (policy == null || policy.CanAccess(targetViewModelType)) return false;
            ParentShell.ReportNavigationFailure(new NavigationErrorEventArgs(Tr.F("You don't have permission to navigate to {0}.", targetViewModelType.Name)));
            return true;
        }

        /// <summary>
        /// Сменить экран своей оболочки без записи в историю «назад» (в отличие от NavigateToAsync оркестратора).
        /// Ошибку не бросает — она приходит в NavigationFailed.
        /// </summary>
        protected Task NavigateParentToAsync<T>() where T : BaseContentViewModel
        {
            if (ParentShell == null) throw new InvalidOperationException(Tr.T("ParentShell is not set."));
            if (IsDenied(typeof(T))) return Task.CompletedTask;
            var parent = ParentShell;
            return parent.ObserveAsync(() => NavigatorPlatform.Dispatcher.InvokeAsync(() => parent.SetCurrentViewAsync<T>()));
        }

        /// <summary>Сменить экран своей оболочки с параметром, без истории «назад». Ошибка — в NavigationFailed.</summary>
        protected Task NavigateParentToAsync<TViewModel, TParameter>(TParameter parameter) where TViewModel : BaseContentViewModel
        {
            if (ParentShell == null) throw new InvalidOperationException(Tr.T("ParentShell is not set."));
            if (IsDenied(typeof(TViewModel))) return Task.CompletedTask;
            var parent = ParentShell;
            return parent.ObserveAsync(() => NavigatorPlatform.Dispatcher.InvokeAsync(() => parent.SetCurrentViewAsync<TViewModel, TParameter>(parameter)));
        }

        /// <summary>Прежнее имя <see cref="NavigateParentToAsync{T}"/>.</summary>
        [Obsolete("Use NavigateParentToAsync.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected Task NavigateParentTo<T>() where T : BaseContentViewModel => NavigateParentToAsync<T>();

        /// <summary>Прежнее имя <see cref="NavigateParentToAsync{TViewModel, TParameter}"/>.</summary>
        [Obsolete("Use NavigateParentToAsync.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected Task NavigateParentTo<TViewModel, TParameter>(TParameter parameter) where TViewModel : BaseContentViewModel =>
            NavigateParentToAsync<TViewModel, TParameter>(parameter);

        public void SetParam<T>(T parameter) => _param = new NavigationParameter<T>(parameter);

        /// <summary>Забрать параметр перехода: после чтения он очищается, второй вызов вернёт default.</summary>
        protected T TakeParam<T>()
        {
            var wrapper = _param as NavigationParameter<T>;
            _param = null;
            return wrapper != null ? wrapper.Value : default(T);
        }

        /// <summary>Прежнее имя <see cref="TakeParam{T}"/>: чтение одноразовое.</summary>
        [Obsolete("Reads once and clears the parameter; use TakeParam.")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        protected T GetParam<T>() => TakeParam<T>();

        /// <summary>
        /// Есть ли непрочитанный параметр. Экран создаётся один раз и инициализируется один раз, поэтому
        /// параметр нового перехода разбирайте в переопределённом <see cref="BaseViewModel.OnAppearingAsync"/>.
        /// </summary>
        protected bool HasParam<T>() => _param is NavigationParameter<T>;
    }
}

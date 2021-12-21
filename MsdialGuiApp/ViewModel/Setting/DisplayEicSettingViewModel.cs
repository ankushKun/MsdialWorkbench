﻿using CompMs.App.Msdial.Model.Setting;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.DataObj;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.ViewModel.Setting {
    class DisplayEicSettingViewModel : ViewModelBase {
        public DisplayEicSettingViewModel(DisplayEicSettingModel model) {
            if (model is null) {
                throw new ArgumentNullException(nameof(model));
            }
            Model = model;
            DiplayEicSettingValues = Model.DiplayEicSettingValues
                .ToReadOnlyReactiveCollection(x => new PeakFeatureSearchValueViewModel(x))
                .AddTo(Disposables);

            var settingHasError = new[] {
                DiplayEicSettingValues.ObserveAddChanged().ToUnit(),
                DiplayEicSettingValues.ObserveRemoveChanged().ToUnit(),
                DiplayEicSettingValues.ObserveElementObservableProperty(vm => vm.HasErrors).ToUnit(),
            }.Merge()
            .Select(_ => DiplayEicSettingValues.Any(vm => vm.HasErrors.Value));

            ObserveHasErrors = new[]
            {
                settingHasError
            }.CombineLatestValuesAreAllFalse()
            .Inverse()
            .ToReadOnlyReactivePropertySlim(true)
            .AddTo(Disposables);

            ApplyCommand = ObserveHasErrors.Inverse()
               .ToReactiveCommand()
               .WithSubscribe(Commit)
               .AddTo(Disposables);
        }

        public DisplayEicSettingModel Model { get; }
        public ReadOnlyReactiveCollection<PeakFeatureSearchValueViewModel> DiplayEicSettingValues { get; }
        public ReadOnlyReactivePropertySlim<bool> ObserveHasErrors { get; }

        public ReactiveCommand ApplyCommand {
            get;
        }
        public void Commit() {
            foreach (var value in DiplayEicSettingValues) {
                value.Commit();
            }
        }
    }
}

﻿using CompMs.App.Msdial.Model.Setting;
using CompMs.App.Msdial.Utility;
using CompMs.App.Msdial.ViewModel.Dims;
using CompMs.App.Msdial.ViewModel.Lcimms;
using CompMs.App.Msdial.ViewModel.Lcms;
using CompMs.Common.Enum;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialDimsCore.Parameter;
using CompMs.MsdialImmsCore.Parameter;
using CompMs.MsdialLcImMsApi.Parameter;
using CompMs.MsdialLcmsApi.Parameter;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;

namespace CompMs.App.Msdial.ViewModel.Setting
{
    public class MethodSettingViewModel : ViewModelBase, ISettingViewModel
    {
        public MethodSettingViewModel(MethodSettingModel model, IObservable<bool> isEnabled) {
            Model = model ?? throw new ArgumentNullException(nameof(model));
            Option = Model.ToReactivePropertySlimAsSynchronized(m => m.Option).AddTo(Disposables);

            var vms = new ISettingViewModel[]
            {
                new DataCollectionSettingViewModel(model.DataCollectionSettingModel, isEnabled).AddTo(Disposables),
                new PeakDetectionSettingViewModel(model.PeakDetectionSettingModel, isEnabled).AddTo(Disposables),
                new DeconvolutionSettingViewModel(model.DeconvolutionSettingModel, isEnabled).AddTo(Disposables),
                new IdentifySettingViewModel(model.IdentifySettingModel, CreateAnnotatorViewModelFactory(Model.Storage), isEnabled).AddTo(Disposables),
                new AdductIonSettingViewModel(model.AdductIonSettingModel, isEnabled).AddTo(Disposables),
                new AlignmentParameterSettingViewModel(model.AlignmentParameterSettingModel, isEnabled).AddTo(Disposables),
                model.MobilitySettingModel is null ? null : new MobilitySettingViewModel(model.MobilitySettingModel, isEnabled).AddTo(Disposables),
                new IsotopeTrackSettingViewModel(model.IsotopeTrackSettingModel, isEnabled).AddTo(Disposables),
            };
            SettingViewModels = new ObservableCollection<ISettingViewModel>(vms.Where(vm => vm != null));

            IsReadOnlyPeakPickParameter = Model.IsReadOnlyPeakPickParameter;
            IsReadOnlyAnnotationParameter = Model.IsReadOnlyAnnotationParameter;
            IsReadOnlyAlignmentParameter = Model.IsReadOnlyAlignmentParameter;

            ObserveChanges = SettingViewModels.ObserveElementObservableProperty(vm => vm.ObserveChanges).Select(pack => pack.Value);

            ObserveHasErrors = SettingViewModels.ObserveElementObservableProperty(vm => vm.ObserveHasErrors)
                .Switch(_ => SettingViewModels.Select(vm => vm.ObserveHasErrors).CombineLatestValuesAreAnyTrue())
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);

            ObserveChangeAfterDecision = SettingViewModels.ObserveElementObservableProperty(vm => vm.ObserveChangeAfterDecision)
                .Switch(_ => SettingViewModels.Select(vm => vm.ObserveChangeAfterDecision).CombineLatestValuesAreAnyTrue())
                .ToReadOnlyReactivePropertySlim()
                .AddTo(Disposables);
        }

        public MethodSettingModel Model { get; }

        public ReactivePropertySlim<ProcessOption> Option { get; }

        public ObservableCollection<ISettingViewModel> SettingViewModels { get; }

        public ReadOnlyReactivePropertySlim<bool> ObserveHasErrors { get; }

        public ReadOnlyReactivePropertySlim<bool> ObserveChangeAfterDecision { get; }

        public bool IsReadOnlyPeakPickParameter { get; }

        public bool IsReadOnlyAnnotationParameter { get; }

        public bool IsReadOnlyAlignmentParameter { get; }

        public IObservable<Unit> ObserveChanges { get; }

        IObservable<bool> ISettingViewModel.ObserveHasErrors => ObserveHasErrors;

        IObservable<bool> ISettingViewModel.ObserveChangeAfterDecision => ObserveChangeAfterDecision;

        public void Next() {
            foreach (var vm in SettingViewModels) {
                vm.Next();
            }           
        }

        public void Run() {
            Model.Run();
        }

        // TODO: delete method
        private static IAnnotatorSettingViewModelFactory CreateAnnotatorViewModelFactory(IMsdialDataStorage<ParameterBase> storage) {
            switch (storage) {
                case IMsdialDataStorage<MsdialLcImMsParameter> _:
                    return new LcimmsAnnotatorSettingViewModelFactory();
                case IMsdialDataStorage<MsdialLcmsParameter> _:
                    return new LcmsAnnotatorSettingViewModelFactory();
                case IMsdialDataStorage<MsdialDimsParameter> _:
                    return new DimsAnnotatorSettingViewModelFactory();
                case IMsdialDataStorage<MsdialImmsParameter> _:
                    throw new NotImplementedException("ImmsAnnotatorSettingViewModelFactory is not implemented");
                    // return new ImmsAnnotatorSettingViewModelFactory();
            }
            throw new NotImplementedException("unknown method acquired.");
        }
    }
}

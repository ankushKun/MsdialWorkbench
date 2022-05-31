﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Lcms;
using CompMs.App.Msdial.View.Normalize;
using CompMs.App.Msdial.ViewModel.Chart;
using CompMs.App.Msdial.ViewModel.Normalize;
using CompMs.App.Msdial.ViewModel.PeakCuration;
using CompMs.App.Msdial.ViewModel.Search;
using CompMs.App.Msdial.ViewModel.Service;
using CompMs.App.Msdial.ViewModel.Table;
using CompMs.CommonMVVM;
using CompMs.CommonMVVM.WindowService;
using CompMs.Graphics.Design;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using Reactive.Bindings.Notifiers;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CompMs.App.Msdial.ViewModel.Lcms
{
    internal sealed class LcmsAlignmentViewModel : AlignmentFileViewModel
    {
        public LcmsAlignmentViewModel(
            LcmsAlignmentModel model,
            IWindowService<CompoundSearchVM> compoundSearchService,
            IWindowService<PeakSpotTableViewModelBase> peakSpotTableService,
            IWindowService<PeakSpotTableViewModelBase> proteomicsTableService,
            IMessageBroker broker,
            FocusControlManager focusControlManager)
            : base(model) {
            if (model is null) {
                throw new ArgumentNullException(nameof(model));
            }

            if (compoundSearchService is null) {
                throw new ArgumentNullException(nameof(compoundSearchService));
            }

            if (peakSpotTableService is null) {
                throw new ArgumentNullException(nameof(peakSpotTableService));
            }

            if (proteomicsTableService is null) {
                throw new ArgumentNullException(nameof(proteomicsTableService));
            }

            if (focusControlManager is null) {
                throw new ArgumentNullException(nameof(focusControlManager));
            }

            this.model = model;
            this.compoundSearchService = compoundSearchService;
            this.peakSpotTableService = peakSpotTableService;
            this.proteomicsTableService = proteomicsTableService;
            _broker = broker;
            Target = this.model.Target.ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            Brushes = this.model.Brushes.AsReadOnly();
            SelectedBrush = this.model.ToReactivePropertySlimAsSynchronized(m => m.SelectedBrush).AddTo(Disposables);

            PeakSpotNavigatorViewModel = new PeakSpotNavigatorViewModel(model.PeakSpotNavigatorModel).AddTo(Disposables);
            PeakFilterViewModel = PeakSpotNavigatorViewModel.PeakFilterViewModel;

            Ms1Spots = CollectionViewSource.GetDefaultView(this.model.Ms1Spots);

            var (peakPlotAction, peakPlotFocused) = focusControlManager.Request();
            PlotViewModel = new AlignmentPeakPlotViewModel(this.model.PlotModel, peakPlotAction, peakPlotFocused).AddTo(Disposables);

            Ms2SpectrumViewModel = new MsSpectrumViewModel(this.model.Ms2SpectrumModel).AddTo(Disposables);
            BarChartViewModel = new BarChartViewModel(this.model.BarChartModel).AddTo(Disposables);
            AlignmentEicViewModel = new AlignmentEicViewModel(this.model.AlignmentEicModel).AddTo(Disposables);
            
            var classBrush = model.ParameterAsObservable
                .Select(p => new KeyBrushMapper<BarItem, string>(
                    p.ProjectParam.ClassnameToColorBytes
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => Color.FromRgb(kvp.Value[0], kvp.Value[1], kvp.Value[2])
                    ),
                    item => item.Class,
                    Colors.Blue));
            AlignmentSpotTableViewModel = new LcmsAlignmentSpotTableViewModel(
                this.model.AlignmentSpotTableModel,
                PeakSpotNavigatorViewModel.MzLowerValue,
                PeakSpotNavigatorViewModel.MzUpperValue,
                PeakSpotNavigatorViewModel.RtLowerValue,
                PeakSpotNavigatorViewModel.RtUpperValue,
                PeakSpotNavigatorViewModel.MetaboliteFilterKeyword,
                PeakSpotNavigatorViewModel.CommentFilterKeyword,
                classBrush)
                .AddTo(Disposables);
            ProteomicsAlignmentTableViewModel = new LcmsProteomicsAlignmentTableViewModel(
                this.model.AlignmentSpotTableModel,
                PeakSpotNavigatorViewModel.MzLowerValue,
                PeakSpotNavigatorViewModel.MzUpperValue,
                PeakSpotNavigatorViewModel.RtLowerValue,
                PeakSpotNavigatorViewModel.RtUpperValue,
                PeakSpotNavigatorViewModel.ProteinFilterKeyword,
                PeakSpotNavigatorViewModel.MetaboliteFilterKeyword,
                PeakSpotNavigatorViewModel.CommentFilterKeyword)
                .AddTo(Disposables);

            SearchCompoundCommand = this.model.CanSearchCompound
                .ToReactiveCommand()
                .WithSubscribe(SearchCompound)
                .AddTo(Disposables);

            FocusNavigatorViewModel = new FocusNavigatorViewModel(model.FocusNavigatorModel).AddTo(Disposables);
        }

        private readonly LcmsAlignmentModel model;
        private readonly IWindowService<CompoundSearchVM> compoundSearchService;
        private readonly IWindowService<PeakSpotTableViewModelBase> peakSpotTableService;
        private readonly IWindowService<PeakSpotTableViewModelBase> proteomicsTableService;
        private readonly IMessageBroker _broker;

        public PeakFilterViewModel PeakFilterViewModel { get; }

        public ReadOnlyCollection<BrushMapData<AlignmentSpotPropertyModel>> Brushes { get; }
        public ReactivePropertySlim<BrushMapData<AlignmentSpotPropertyModel>> SelectedBrush { get; }
        public PeakSpotNavigatorViewModel PeakSpotNavigatorViewModel { get; }
        public ICollectionView Ms1Spots { get; }
        public override ICollectionView PeakSpotsView => Ms1Spots;

        public ReadOnlyReactivePropertySlim<AlignmentSpotPropertyModel> Target { get; }

        public AlignmentPeakPlotViewModel PlotViewModel { get; }
        public MsSpectrumViewModel Ms2SpectrumViewModel { get; }
        public BarChartViewModel BarChartViewModel { get; }
        public AlignmentEicViewModel AlignmentEicViewModel { get; }
        public LcmsAlignmentSpotTableViewModel AlignmentSpotTableViewModel { get; }
        public LcmsProteomicsAlignmentTableViewModel ProteomicsAlignmentTableViewModel { get; }
        public AlignedChromatogramModificationViewModelLegacy AlignedChromatogramModificationViewModel { get; }

        public ReactiveCommand SearchCompoundCommand { get; }

        private void SearchCompound() {
            using (var csm = model.CreateCompoundSearchModel()) {
                if (csm is null) {
                    return;
                }
                using (var vm = new LcmsCompoundSearchViewModel(csm)) {
                    compoundSearchService.ShowDialog(vm);
                }
            }
        }

        public DelegateCommand ShowIonTableCommand => showIonTableCommand ?? (showIonTableCommand = new DelegateCommand(ShowIonTable));
        private DelegateCommand showIonTableCommand;

        private void ShowIonTable() {
            if (model.Parameter.TargetOmics == CompMs.Common.Enum.TargetOmics.Proteomics) {
                proteomicsTableService.Show(ProteomicsAlignmentTableViewModel);
            }
            else {
                peakSpotTableService.Show(AlignmentSpotTableViewModel);
            }
        }

        public FocusNavigatorViewModel FocusNavigatorViewModel { get; }

        public DelegateCommand<Window> SaveSpectraCommand => saveSpectraCommand ?? (saveSpectraCommand = new DelegateCommand<Window>(SaveSpectra, CanSaveSpectra));
        private DelegateCommand<Window> saveSpectraCommand;

        private void SaveSpectra(Window owner) {
            var request = new SaveFileNameRequest(model.SaveSpectra)
            {
                Title = "Save spectra",
                Filter = "NIST format(*.msp)|*.msp|MassBank format(*.txt)|*.txt;|MASCOT format(*.mgf)|*.mgf|MSFINDER format(*.mat)|*.mat;|SIRIUS format(*.ms)|*.ms",
                RestoreDirectory = true,
                AddExtension = true,
            };
            _broker.Publish(request);
        }

        private bool CanSaveSpectra(Window owner) {
            return this.model.CanSaveSpectra();
        }

        public DelegateCommand<Window> NormalizeCommand => normalizeCommand ?? (normalizeCommand = new DelegateCommand<Window>(Normalize));

        private DelegateCommand<Window> normalizeCommand;

        private void Normalize(Window owner) {
            var parameter = model.Parameter;
            using (var vm = new NormalizationSetViewModel(model.Container, model.DataBaseMapper, model.MatchResultEvaluator, parameter, _broker)) {
                var view = new NormalizationSetView {
                    DataContext = vm,
                    Owner = owner,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };
                view.ShowDialog();
            }
        }
    }
}

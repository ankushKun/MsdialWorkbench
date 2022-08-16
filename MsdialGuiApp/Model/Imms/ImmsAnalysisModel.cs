﻿using CompMs.App.Msdial.Model.Chart;
using CompMs.App.Msdial.Model.Core;
using CompMs.App.Msdial.Model.DataObj;
using CompMs.App.Msdial.Model.Information;
using CompMs.App.Msdial.Model.Loader;
using CompMs.App.Msdial.Model.Search;
using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.CommonMVVM.ChemView;
using CompMs.Graphics.Base;
using CompMs.Graphics.Design;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Export;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Utility;
using CompMs.MsdialImmsCore.Parameter;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace CompMs.App.Msdial.Model.Imms
{
    internal sealed class ImmsAnalysisModel : AnalysisModelBase {
        private static readonly double MZ_TOLELANCE = 20;
        private static readonly double DT_TOLELANCE = 0.01;

        private readonly MsdialImmsParameter _parameter;
        private readonly IDataProvider _provider;
        private readonly DataBaseMapper _dataBaseMapper;
        private readonly IMatchResultEvaluator<MsScanMatchResult> _matchResultEvaluator;
        private readonly IBrushMapper<ChromatogramPeakFeatureModel> _brush;
        private readonly IReadOnlyList<IAnnotatorContainer<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult>> _annotatorContainers;

        public ImmsAnalysisModel(
            AnalysisFileBean analysisFile,
            IDataProvider provider,
            IMatchResultEvaluator<MsScanMatchResult> evaluator,
            IReadOnlyList<IAnnotatorContainer<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult>> annotatorContainers,
            DataBaseMapper mapper,
            ParameterBase parameter,
            PeakFilterModel peakFilterModel)
            : base(analysisFile) {

            _provider = provider;
            _dataBaseMapper = mapper;
            _matchResultEvaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
            _annotatorContainers = annotatorContainers;
            _parameter = parameter as MsdialImmsParameter;

            PeakSpotNavigatorModel = new PeakSpotNavigatorModel(Ms1Peaks, peakFilterModel, evaluator, useRtFilter: false, useDtFilter: true);

            var ontologyBrush = new BrushMapData<ChromatogramPeakFeatureModel>(
                    new KeyBrushMapper<ChromatogramPeakFeatureModel, string>(
                        ChemOntologyColor.Ontology2RgbaBrush,
                        peak => peak?.Ontology ?? string.Empty,
                        Color.FromArgb(180, 181, 181, 181)),
                    "Ontology");
            var intensityBrush = new BrushMapData<ChromatogramPeakFeatureModel>(
                    new DelegateBrushMapper<ChromatogramPeakFeatureModel>(
                        peak => Color.FromArgb(
                            180,
                            (byte)(255 * peak.InnerModel.PeakShape.AmplitudeScoreValue),
                            (byte)(255 * (1 - Math.Abs(peak.InnerModel.PeakShape.AmplitudeScoreValue - 0.5))),
                            (byte)(255 - 255 * peak.InnerModel.PeakShape.AmplitudeScoreValue)),
                        enableCache: true),
                    "Ontology");
            var brushes = new[] { intensityBrush, ontologyBrush, };
            BrushMapData<ChromatogramPeakFeatureModel> selectedBrush;
            switch (parameter.TargetOmics) {
                case TargetOmics.Lipidomics:
                    selectedBrush = ontologyBrush;
                    break;
                case TargetOmics.Metabolomics:
                case TargetOmics.Proteomics:
                default:
                    selectedBrush = intensityBrush;
                    break;
            }
            _brush = selectedBrush.Mapper;
            var labelsource = PeakSpotNavigatorModel.ObserveProperty(m => m.SelectedAnnotationLabel).ToReadOnlyReactivePropertySlim().AddTo(Disposables);
            PlotModel = new AnalysisPeakPlotModel(Ms1Peaks, peak => peak.ChromXValue ?? 0, peak => peak.Mass, Target, labelsource, selectedBrush, brushes)
            {
                HorizontalTitle = "Drift time [1/k0]",
                VerticalTitle = "m/z",
                HorizontalProperty = nameof(ChromatogramPeakWrapper.ChromXValue),
                VerticalProperty = nameof(ChromatogramPeakFeatureModel.Mass),
            }.AddTo(Disposables);
            Target.Select(
                t => $"File: {analysisFile.AnalysisFileName}" +
                    (t is null
                        ? string.Empty
                        : $"Spot ID: {t.InnerModel.MasterPeakID} Scan: {t.InnerModel.MS1RawSpectrumIdTop} Mass m/z: {t.InnerModel.Mass:N5}"))
                .Subscribe(title => PlotModel.GraphTitle = title);

            var eicLoader = EicLoader.BuildForAllRange(provider, parameter, ChromXType.Drift, ChromXUnit.Msec, this._parameter.DriftTimeBegin, this._parameter.DriftTimeEnd);
            EicLoader = EicLoader.BuildForPeakRange(provider, parameter, ChromXType.Drift, ChromXUnit.Msec, this._parameter.DriftTimeBegin, this._parameter.DriftTimeEnd);
            EicModel = new EicModel(Target, eicLoader)
            {
                HorizontalTitle = PlotModel.HorizontalTitle,
                VerticalTitle = "Abundance",
            }.AddTo(Disposables);

            var upperSpecBrush = new KeyBrushMapper<SpectrumComment, string>(
               parameter.ProjectParam.SpectrumCommentToColorBytes
               .ToDictionary(
                   kvp => kvp.Key,
                   kvp => Color.FromRgb(kvp.Value[0], kvp.Value[1], kvp.Value[2])
               ),
               item => item.ToString(),
               Colors.Blue);
            var lowerSpecBrush = new DelegateBrushMapper<SpectrumComment>(
                comment =>
                {
                    var commentString = comment.ToString();
                    var projectParameter = parameter.ProjectParam;
                    if (projectParameter.SpectrumCommentToColorBytes.TryGetValue(commentString, out var color)) {
                        return Color.FromRgb(color[0], color[1], color[2]);
                    }
                    else if ((comment & SpectrumComment.doublebond) == SpectrumComment.doublebond
                        && projectParameter.SpectrumCommentToColorBytes.TryGetValue(SpectrumComment.doublebond.ToString(), out color)) {
                        return Color.FromRgb(color[0], color[1], color[2]);
                    }
                    else {
                        return Colors.Red;
                    }
                },
                true);
            var spectraExporter = new NistSpectraExporter(Target.Select(t => t?.InnerModel), mapper, parameter).AddTo(Disposables);
            var rawLoader = new MultiMsRawSpectrumLoader(provider, parameter).AddTo(Disposables);
            Ms2SpectrumModel = new RawDecSpectrumsModel(
                Target,
                rawLoader,
                new MsDecSpectrumLoader(decLoader, Ms1Peaks),
                new MsRefSpectrumLoader(mapper),
                new PropertySelector<SpectrumPeak, float>(peak => peak.Mass),
                new PropertySelector<SpectrumPeak, float>(peak => peak.Intensity),
                new GraphLabels("Measure vs. Reference", "m/z", "Relative aubndance", nameof(SpectrumPeak.Mass), nameof(SpectrumPeak.Intensity)),
                nameof(SpectrumPeak.SpectrumComment),
                Observable.Return(upperSpecBrush),
                Observable.Return(lowerSpecBrush),
                Observable.Return(spectraExporter),
                Observable.Return(spectraExporter),
                Observable.Return((ISpectraExporter)null)).AddTo(Disposables);

            var surveyScanSpectrum = new SurveyScanSpectrum(Target, target => Observable.FromAsync(token => LoadMs1SpectrumAsync(target, token)))
                .AddTo(Disposables);
            SurveyScanModel = new SurveyScanModel(
                surveyScanSpectrum,
                spec => spec.Mass,
                spec => spec.Intensity
            ).AddTo(Disposables);
            SurveyScanModel.Elements.VerticalTitle = "Abundance";
            SurveyScanModel.Elements.HorizontalProperty = nameof(SpectrumPeakWrapper.Mass);
            SurveyScanModel.Elements.VerticalProperty = nameof(SpectrumPeakWrapper.Intensity);

            PeakTableModel = new ImmsAnalysisPeakTableModel(Ms1Peaks, Target, MassMin, MassMax, ChromMin, ChromMax).AddTo(Disposables);

            var mzSpotFocus = new ChromSpotFocus(PlotModel.VerticalAxis, MZ_TOLELANCE, Target.Select(t => t?.Mass ?? 0d), "F3", "m/z", isItalic: true).AddTo(Disposables);
            var dtSpotFocus = new ChromSpotFocus(PlotModel.HorizontalAxis, DT_TOLELANCE, Target.Select(t => t?.ChromXValue ?? 0d), "F3", "Drift time(1/k0)", isItalic: false).AddTo(Disposables);
            var idSpotFocus = new IdSpotFocus<ChromatogramPeakFeatureModel>(
                Target,
                id => Ms1Peaks.Argmin(p => Math.Abs(p.MasterPeakID - id)),
                Target.Select(t => t?.MasterPeakID ?? 0d),
                "Region focus by ID",
                (dtSpotFocus, peak => peak.ChromXValue ?? 0d),
                (mzSpotFocus, peak => peak.Mass)).AddTo(Disposables);
            FocusNavigatorModel = new FocusNavigatorModel(idSpotFocus, dtSpotFocus, mzSpotFocus);

            var peakInformationModel = new PeakInformationAnalysisModel(Target).AddTo(Disposables);
            peakInformationModel.Add(
                t => new MzPoint(t?.InnerModel.ChromXsTop.Mz.Value ?? 0d),
                t => new DriftPoint(t?.InnerModel.ChromXsTop.Drift.Value ?? 0d),
                t => new CcsPoint(t?.InnerModel.CollisionCrossSection ?? 0d));
            peakInformationModel.Add(
                t => new HeightAmount(t?.Intensity ?? 0d),
                t => new AreaAmount(t?.PeakArea ?? 0d));
            PeakInformationModel = peakInformationModel;
            var compoundDetailModel = new CompoundDetailModel(Target.Select(t => t?.ScanMatchResult), mapper).AddTo(Disposables);
            compoundDetailModel.Add(
                r_ => new MzSimilarity(r_?.AcurateMassSimilarity ?? 0d),
                r_ => new CcsSimilarity(r_?.CcsSimilarity ?? 0d),
                r_ => new SpectrumSimilarity(r_?.WeightedDotProduct ?? 0d, r_?.ReverseDotProduct ?? 0d));
            CompoundDetailModel = compoundDetailModel;
        }

        public AnalysisPeakPlotModel PlotModel { get; }

        public EicModel EicModel { get; }

        public RawDecSpectrumsModel Ms2SpectrumModel { get; }

        public SurveyScanModel SurveyScanModel { get; }

        public PeakSpotNavigatorModel PeakSpotNavigatorModel { get; }

        public ImmsAnalysisPeakTableModel PeakTableModel { get; }

        public FocusNavigatorModel FocusNavigatorModel { get; }

        public PeakInformationAnalysisModel PeakInformationModel { get; }
        public CompoundDetailModel CompoundDetailModel { get; }

        public EicLoader EicLoader { get; }

        public double ChromMin => Ms1Peaks.DefaultIfEmpty().Min(peak => peak?.ChromXValue) ?? 0d;
        public double ChromMax => Ms1Peaks.DefaultIfEmpty().Max(peak => peak?.ChromXValue) ?? 0d;
        public double MassMin => Ms1Peaks.DefaultIfEmpty().Min(peak => peak?.Mass) ?? 0d;
        public double MassMax => Ms1Peaks.DefaultIfEmpty().Max(peak => peak?.Mass) ?? 0d;

        private Task<List<SpectrumPeakWrapper>> LoadMs1SpectrumAsync(ChromatogramPeakFeatureModel target, CancellationToken token) {
            if (target is null || target.MS1RawSpectrumIdTop < 0) {
                return Task.FromResult(new List<SpectrumPeakWrapper>(0));
            }

            return Task.Run(async () =>
            {
                var ms1Spectra = await _provider.LoadMs1SpectrumsAsync(token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var spectra = DataAccess.GetCentroidMassSpectra(ms1Spectra[target.MS1RawSpectrumIdTop], _parameter.MSDataType, 0, float.MinValue, float.MaxValue);
                token.ThrowIfCancellationRequested();
                return spectra.Select(peak => new SpectrumPeakWrapper(peak)).ToList();
            }, token);
        }

        public ImmsCompoundSearchModel<ChromatogramPeakFeature> CreateCompoundSearchModel() {
            if (Target.Value?.InnerModel is null || MsdecResult.Value is null) {
                return null;
            }

            return new ImmsCompoundSearchModel<ChromatogramPeakFeature>(
                AnalysisFile,
                Target.Value.InnerModel,
                MsdecResult.Value,
                null,
                _annotatorContainers);
        }

        public void SaveSpectra(string filename) {
            using (var file = File.Open(filename, FileMode.Create)) {
                SpectraExport.SaveSpectraTable(
                    (ExportSpectraFileFormat)Enum.Parse(typeof(ExportSpectraFileFormat), Path.GetExtension(filename).Trim('.')),
                    file,
                    Target.Value.InnerModel,
                    MsdecResult.Value,
                    _provider.LoadMs1Spectrums(),
                    _dataBaseMapper,
                    _parameter);
            }
        }

        public bool CanSaveSpectra() => Target.Value.InnerModel != null && MsdecResult.Value != null;
    }
}

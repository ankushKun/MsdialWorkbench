﻿using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Database;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Extension;
using CompMs.Common.Parameter;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialCore.Utility;
using CompMs.MsdialImmsCore.Algorithm;
using CompMs.MsdialImmsCore.Algorithm.Annotation;
using CompMs.MsdialImmsCore.Parameter;
using CompMs.RawDataHandler.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace CompMs.MsdialImmsCore.Process
{
    public static class FileProcess
    {
        public static void Run(
            AnalysisFileBean file,
            IMsdialDataStorage<MsdialImmsParameter> container,
            bool isGuiProcess = false,
            Action<int> reportAction = null,
            CancellationToken token = default) {

            var mspAnnotator = new ImmsMspAnnotator(new MoleculeDataBase(container.MspDB, "MspDB", DataBaseSource.Msp, SourceType.MspDB), container.Parameter.MspSearchParam, container.Parameter.TargetOmics, "MspDB", -1);
            var textDBAnnotator = new ImmsTextDBAnnotator(new MoleculeDataBase(container.TextDB, "TextDB", DataBaseSource.Text, SourceType.TextDB), container.Parameter.TextDbSearchParam, "TextDB", -1);

            Run(file, container, mspAnnotator, textDBAnnotator, new ImmsAverageDataProviderFactory(0.001, 0.002, retry: 5, isGuiProcess: isGuiProcess), isGuiProcess, reportAction, token);
        }

        public static void Run(
            AnalysisFileBean file,
            IMsdialDataStorage<MsdialImmsParameter> container,
            IAnnotator<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult> mspAnnotator,
            IAnnotator<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult> textDBAnnotator,
            IDataProviderFactory<AnalysisFileBean> providerFactory,
            bool isGuiProcess = false,
            Action<int> reportAction = null, CancellationToken token = default) {

            var parameter = container.Parameter;
            var iupacDB = container.IupacDatabase;
            var annotatorContainers = container.DataBaseMapper.MoleculeAnnotators;

            var rawObj = LoadMeasurement(file, isGuiProcess);
            var provider = providerFactory.Create(file);

            Console.WriteLine("Peak picking started");
            parameter.FileID2CcsCoefficients.TryGetValue(file.AnalysisFileId, out var coeff);
            var chromPeakFeatures = PeakSpotting(provider, parameter, iupacDB, coeff, reportAction);

            var spectrumList = rawObj.SpectrumList;
            var summary = ChromFeatureSummarizer.GetChromFeaturesSummary(spectrumList, chromPeakFeatures, parameter);
            file.ChromPeakFeaturesSummary = summary;

            Console.WriteLine("Deconvolution started");
            var targetCE2MSDecResults = SpectrumDeconvolution(rawObj, provider, chromPeakFeatures, summary, parameter, iupacDB, reportAction, token);

            // annotations
            Console.WriteLine("Annotation started");
            PeakAnnotation(targetCE2MSDecResults, provider, chromPeakFeatures, annotatorContainers, mspAnnotator, textDBAnnotator, parameter, reportAction, token);

            // characterizatin
            PeakCharacterization(targetCE2MSDecResults, provider, chromPeakFeatures, parameter, reportAction, container.DataBaseMapper);

            // file save
            SaveToFile(file, chromPeakFeatures, targetCE2MSDecResults);

            reportAction?.Invoke(100);
        }

        private static RawMeasurement LoadMeasurement(AnalysisFileBean file, bool isGuiProcess) {
            using (var access = new RawDataAccess(file.AnalysisFilePath, 0, false, isGuiProcess)) {
                for (var i = 0; i < 5; i++) {
                    var rawObj = DataAccess.GetRawDataMeasurement(access);
                    if (rawObj != null)
                        return rawObj;
                    Thread.Sleep(5000);
                }
                throw new FileLoadException($"Loading {file.AnalysisFilePath} failed.");
            }
        }

        private static List<ChromatogramPeakFeature> PeakSpotting(
            IDataProvider provider,
            MsdialImmsParameter parameter,
            IupacDatabase iupacDB,
            CoefficientsForCcsCalculation coeff,
            Action<int> reportAction) {

            var chromPeakFeatures = new PeakSpotting(0, 30).Run(provider, parameter, reportAction);
            IsotopeEstimator.Process(chromPeakFeatures, parameter, iupacDB, true);
            CcsEstimator.Process(chromPeakFeatures, parameter, parameter.IonMobilityType, coeff, parameter.IsAllCalibrantDataImported);
            return chromPeakFeatures;
        }

        private static Dictionary<double, List<MSDecResult>> SpectrumDeconvolution(
            RawMeasurement rawObj,
            IDataProvider provider,
            List<ChromatogramPeakFeature> chromPeakFeatures,
            ChromatogramPeaksDataSummary summary,
            MsdialImmsParameter parameter,
            IupacDatabase iupac,
            Action<int> reportAction,
            CancellationToken token) {

            var spectrumList = provider.LoadMsSpectrums();
            var targetCE2MSDecResults = new Dictionary<double, List<MSDecResult>>();
            var initial_msdec = 30.0;
            var max_msdec = 30.0;
            if (parameter.AcquisitionType == Common.Enum.AcquisitionType.AIF) {
                var ceList = rawObj.CollisionEnergyTargets;
                for (int i = 0; i < ceList.Count; i++) {
                    var targetCE = Math.Round(ceList[i], 2); // must be rounded by 2 decimal points
                    if (targetCE <= 0) {
                        Console.WriteLine("No correct CE information in AIF-MSDEC");
                        continue;
                    }
                    var max_msdec_aif = max_msdec / ceList.Count;
                    var initial_msdec_aif = initial_msdec + max_msdec_aif * i;
                    targetCE2MSDecResults[targetCE] = new Ms2Dec(initial_msdec_aif, max_msdec_aif).GetMS2DecResults(
                        spectrumList, chromPeakFeatures, parameter, summary, iupac, targetCE, reportAction, parameter.NumThreads, token);
                }
            }
            else {
                var targetCE = rawObj.CollisionEnergyTargets.IsEmptyOrNull() ? -1 : Math.Round(rawObj.CollisionEnergyTargets[0], 2);
                targetCE2MSDecResults[targetCE] = new Ms2Dec(initial_msdec, max_msdec).GetMS2DecResults(
                       spectrumList, chromPeakFeatures, parameter, summary, iupac, - 1, reportAction, parameter.NumThreads, token);
            }
            return targetCE2MSDecResults;
        }

        private static void PeakAnnotation(
            Dictionary<double, List<MSDecResult>> targetCE2MSDecResults,
            IDataProvider provider,
            List<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyCollection<IAnnotatorContainer<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult>> annotatorContainers,
            IAnnotator<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult> mspAnnotator,
            IAnnotator<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult> textDBAnnotator,
            MsdialImmsParameter parameter,
            Action<int> reportAction, CancellationToken token) {

            var initial_annotation = 60.0;
            var max_annotation = 30.0;
            foreach (var (ce2msdecs, index) in targetCE2MSDecResults.WithIndex()) {
                var targetCE = ce2msdecs.Key;
                var msdecResults = ce2msdecs.Value;
                var max_annotation_local = max_annotation / targetCE2MSDecResults.Count;
                var initial_annotation_local = initial_annotation + max_annotation_local * index;
                new AnnotationProcess(initial_annotation_local, max_annotation_local).Run(
                    provider, chromPeakFeatures, msdecResults,
                    annotatorContainers, mspAnnotator, textDBAnnotator, parameter,
                    reportAction, parameter.NumThreads, token
                );
            }
        }

        private static void PeakCharacterization(
            Dictionary<double, List<MSDecResult>> targetCE2MSDecResults,
            IDataProvider provider,
            List<ChromatogramPeakFeature> chromPeakFeatures,
            MsdialImmsParameter parameter,
            Action<int> reportAction,
            DataBaseMapper mapper) {

            new PeakCharacterEstimator(90, 10).Process(provider.LoadMsSpectrums(), chromPeakFeatures,
                targetCE2MSDecResults.Any() ? targetCE2MSDecResults.Argmin(kvp => kvp.Key).Value : null,
                mapper, parameter, reportAction);
        }

        private static void SaveToFile(
            AnalysisFileBean file,
            List<ChromatogramPeakFeature> chromPeakFeatures,
            Dictionary<double, List<MSDecResult>> targetCE2MSDecResults) {

            var paifile = file.PeakAreaBeanInformationFilePath;
            MsdialPeakSerializer.SaveChromatogramPeakFeatures(paifile, chromPeakFeatures);

            var dclfile = file.DeconvolutionFilePath;
            var dclfiles = new List<string>();
            if (targetCE2MSDecResults.Count == 1) {
                dclfiles.Add(dclfile);
                MsdecResultsWriter.Write(dclfile, targetCE2MSDecResults.Single().Value);
            }
            else {
                var dclDirectory = Path.GetDirectoryName(dclfile);
                var dclName = Path.GetFileNameWithoutExtension(dclfile);
                foreach (var ce2msdecs in targetCE2MSDecResults) {
                    var suffix = Math.Round(ce2msdecs.Key * 100, 0); // CE 34.50 -> 3450
                    var dclfile_suffix = Path.Combine(dclDirectory,  dclName + "_" + suffix + ".dcl");
                    dclfiles.Add(dclfile_suffix);
                    MsdecResultsWriter.Write(dclfile_suffix, ce2msdecs.Value);
                }
            }
            file.DeconvolutionFilePathList = dclfiles;
        }
    }
}

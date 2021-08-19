﻿using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.FormulaGenerator.Function;
using CompMs.Common.Interfaces;
using CompMs.Common.Parameter;
using CompMs.MsdialCore.Algorithm;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Utility;
using CompMs.MsdialImmsCore.Parameter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompMs.MsdialImmsCore.Algorithm
{
    public class AnnotationProcess
    {

        public double InitialProgress { get; set; } = 60.0;
        public double ProgressMax { get; set; } = 30.0;

        public AnnotationProcess(double InitialProgress, double ProgressMax) {
            this.InitialProgress = InitialProgress;
            this.ProgressMax = ProgressMax;
        }

        public void Run(
            IDataProvider provider,
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyList<MSDecResult> msdecResults,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> mspAnnotator,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> textDBAnnotator,
            MsdialImmsParameter parameter,
            Action<int> reportAction) {

            if (chromPeakFeatures.Count != msdecResults.Count)
                throw new ArgumentException("Number of ChromatogramPeakFeature and MSDecResult are different.");

            if (mspAnnotator == null && textDBAnnotator == null) {
                reportAction?.Invoke((int)ProgressMax);
                return;
            }

            var spectrumList = provider.LoadMs1Spectrums();
            for (int i = 0; i < chromPeakFeatures.Count; i++) {
                var chromPeakFeature = chromPeakFeatures[i];
                var msdecResult = msdecResults[i];
                if (chromPeakFeature.PeakCharacter.IsotopeWeightNumber == 0) {
                    ImmsMatchMethod(chromPeakFeature, msdecResult, spectrumList[chromPeakFeature.MS1RawSpectrumIdTop].Spectrum, mspAnnotator, textDBAnnotator, parameter);
                }
                ReportProgress.Show(InitialProgress, ProgressMax, i, chromPeakFeatures.Count, reportAction);
            }
        }

        public void Run(
            IDataProvider provider,
            IReadOnlyList<ChromatogramPeakFeature> chromPeakFeatures,
            IReadOnlyList<MSDecResult> msdecResults,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> mspAnnotator,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> textDBAnnotator,
            MsdialImmsParameter parameter,
            Action<int> reportAction,
            int numThreads, System.Threading.CancellationToken token) {

            if (chromPeakFeatures.Count != msdecResults.Count)
                throw new ArgumentException("Number of ChromatogramPeakFeature and MSDecResult are different.");

            if (mspAnnotator == null && textDBAnnotator == null) {
                reportAction?.Invoke((int)ProgressMax);
                return;
            }

            var spectrumList = provider.LoadMs1Spectrums();
            Enumerable.Range(0, chromPeakFeatures.Count)
                .AsParallel()
                .WithCancellation(token)
                .WithDegreeOfParallelism(numThreads)
                .ForAll(i => {
                    var chromPeakFeature = chromPeakFeatures[i];
                    var msdecResult = msdecResults[i];
                    //Console.WriteLine("mass {0}, isotope {1}", chromPeakFeature.Mass, chromPeakFeature.PeakCharacter.IsotopeWeightNumber);
                    if (chromPeakFeature.PeakCharacter.IsotopeWeightNumber == 0) {
                        ImmsMatchMethod(chromPeakFeature, msdecResult, spectrumList[chromPeakFeature.MS1RawSpectrumIdTop].Spectrum, mspAnnotator, textDBAnnotator, parameter);
                    }
                    ReportProgress.Show(InitialProgress, ProgressMax, i, chromPeakFeatures.Count, reportAction);
                });
        }

        private static void ImmsMatchMethod(
            ChromatogramPeakFeature chromPeakFeature, MSDecResult msdecResult,
            IReadOnlyList<RawPeakElement> spectrum,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> mspAnnotator,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> textDBAnnotator,
            MsdialImmsParameter parameter) {
            //if (Math.Abs(chromPeakFeature.Mass - 770.509484372875) < 0.02) {
            //    Console.WriteLine();
            //}
            var isotopes = DataAccess.GetIsotopicPeaks(spectrum, (float)chromPeakFeature.Mass, parameter.CentroidMs1Tolerance);

            SetMspAnnotationResult(chromPeakFeature, msdecResult, isotopes, mspAnnotator, parameter.MspSearchParam, parameter.TargetOmics);
            SetTextDBAnnotationResult(chromPeakFeature, msdecResult, isotopes, textDBAnnotator, parameter.TextDbSearchParam);
        }

        private static void SetMspAnnotationResult(
            ChromatogramPeakFeature chromPeakFeature, MSDecResult msdecResult, List<IsotopicPeak> isotopes,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> mspAnnotator, MsRefSearchParameterBase mspSearchParameter, TargetOmics omics) {

            if (mspAnnotator == null)
                return;

            var candidates = mspAnnotator.FindCandidates(chromPeakFeature, msdecResult, isotopes, mspSearchParameter);
            var results = mspAnnotator.FilterByThreshold(candidates, mspSearchParameter);
            chromPeakFeature.MSRawID2MspIDs[msdecResult.RawSpectrumID] = results.Select(result => result.LibraryIDWhenOrdered).ToList();
            var matches = mspAnnotator.SelectReferenceMatchResults(results, mspSearchParameter);
            if (matches.Count > 0) {
                var best = matches.Argmax(result => result.TotalScore);
                chromPeakFeature.MSRawID2MspBasedMatchResult[msdecResult.RawSpectrumID] = best;
                chromPeakFeature.MatchResults.AddMspResult(msdecResult.RawSpectrumID, best);
                DataAccess.SetMoleculeMsProperty(chromPeakFeature, mspAnnotator.Refer(best), best);
            }
            else if (results.Count > 0) {
                var best = results.Argmax(result => result.TotalScore);
                chromPeakFeature.MSRawID2MspBasedMatchResult[msdecResult.RawSpectrumID] = best;
                chromPeakFeature.MatchResults.AddMspResult(msdecResult.RawSpectrumID, best);
                DataAccess.SetMoleculeMsPropertyAsSuggested(chromPeakFeature, mspAnnotator.Refer(best), best);
            }
        }

        private static void SetTextDBAnnotationResult(
            ChromatogramPeakFeature chromPeakFeature, MSDecResult msdecResult, List<IsotopicPeak> isotopes,
            IAnnotator<ChromatogramPeakFeature, MSDecResult> textDBAnnotator, MsRefSearchParameterBase textDBSearchParameter) {

            if (textDBAnnotator == null)
                return;
            //if (Math.Abs(chromPeakFeature.Mass - 770.509484372875) < 0.02) {
            //    Console.WriteLine();
            //}
            var candidates = textDBAnnotator.FindCandidates(chromPeakFeature, msdecResult, isotopes, textDBSearchParameter);
            var results = textDBAnnotator.SelectReferenceMatchResults(candidates, textDBSearchParameter);
            chromPeakFeature.TextDbIDs.AddRange(results.Select(result => result.LibraryIDWhenOrdered));
            chromPeakFeature.MatchResults.AddTextDbResults(results);
            if (results.Count > 0) {
                var best = results.Argmax(result => result.TotalScore);
                if (chromPeakFeature.TextDbBasedMatchResult == null || chromPeakFeature.TextDbBasedMatchResult.TotalScore < best.TotalScore) {
                    chromPeakFeature.TextDbBasedMatchResult = best;
                    DataAccess.SetTextDBMoleculeMsProperty(chromPeakFeature, textDBAnnotator.Refer(best), best);
                }
            }
        }
    }
}

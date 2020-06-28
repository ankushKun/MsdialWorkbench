﻿using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Database;
using CompMs.Common.Extension;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Utility;
using CompMs.MsdialLcmsApi.Parameter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompMs.MsdialLcMsApi.Algorithm {
    public class Ms2Dec {
        public List<MSDecResult> GetMS2DecResults(List<RawSpectrum> spectrumList, List<ChromatogramPeakFeature> chromPeakFeatures,
            MsdialLcmsParameter param, ChromatogramPeaksDataSummary summary,
            Action<int> reportAction, System.Threading.CancellationToken token, double targetCE = -1) {

            var msdecResults = new List<MSDecResult>();
            foreach (var spot in chromPeakFeatures) {
                var result = GetMS2DecResult(spectrumList, spot, param, summary, targetCE);
                result.ScanID = spot.PeakID;
            }
            return msdecResults;
        }

        public MSDecResult GetMS2DecResult(List<RawSpectrum> spectrumList,
            ChromatogramPeakFeature chromPeakFeature, MsdialLcmsParameter param, 
            ChromatogramPeaksDataSummary summary, double targetCE = -1) { // targetCE is used in multiple CEs option
            
            //first, the MS/MS spectrum at the scan point of peak top is stored.
            var cSpectrum = DataAccess.GetCentroidMassSpectra(spectrumList, param.DataTypeMS2, chromPeakFeature.MS2RawSpectrumID, 
                param.AmplitudeCutoff, param.Ms2MassRangeBegin, param.Ms2MassRangeEnd);
            if (cSpectrum.IsEmptyOrNull()) return MSDecObjectHandler.GetDefaultMSDecResult(chromPeakFeature);

            var curatedSpectra = new List<SpectrumPeak>(); // used for normalization of MS/MS intensities
            var precursorMz = chromPeakFeature.Mass;
            var threshold = Math.Max(param.AmplitudeCutoff, 0.1);

            foreach (var peak in cSpectrum.Where(n => n.Intensity > threshold)) { //preparing MS/MS chromatograms -> peaklistList
                if (param.RemoveAfterPrecursor && precursorMz + param.KeptIsotopeRange < peak.Mass) continue;
                curatedSpectra.Add(peak);
            }
            if (curatedSpectra.IsEmptyOrNull()) return MSDecObjectHandler.GetDefaultMSDecResult(chromPeakFeature);

            if (param.MethodType == Common.Enum.MethodType.ddMSMS) {
                return MSDecObjectHandler.GetMSDecResultByRawSpectrum(chromPeakFeature, curatedSpectra);
            }

            //check the RT range to be considered for chromatogram deconvolution
            var peakWidth = chromPeakFeature.PeakWidth();
            if (peakWidth >= summary.AveragePeakWidthOnRtAxis + summary.StdevPeakWidthOnRtAxis * 3) peakWidth = summary.AveragePeakWidthOnRtAxis + summary.StdevPeakWidthOnRtAxis * 3; // width should be less than mean + 3*sigma for excluding redundant peak feature
            if (peakWidth <= summary.MedianPeakWidthOnRtAxis) peakWidth = summary.MedianPeakWidthOnRtAxis; // currently, the median peak width is used for very narrow peak feature

            var startRt = (float)(chromPeakFeature.ChromXsTop.Value - peakWidth * 1.5F);
            var endRt = (float)(chromPeakFeature.ChromXsTop.Value + peakWidth * 1.5F);

            //preparing MS1 and MS/MS chromatograms
            //note that the MS1 chromatogram trace (i.e. EIC) is also used as the candidate of model chromatogram
            var ms1Peaklist = DataAccess.GetMs1Peaklist(spectrumList, (float)precursorMz, param.CentroidMs1Tolerance, param.IonMode, ChromXType.RT, ChromXUnit.Min, startRt, endRt);

            var startScanNum = ms1Peaklist[0].ID;
            var endScanNum = ms1Peaklist[ms1Peaklist.Count - 1].ID;
            var minimumDiff = double.MaxValue;
            var minimumID = (int)(ms1Peaklist.Count / 2);

            // Define the scan number of peak top in the array of MS1 chromatogram restricted by the retention time range
            foreach (var (peak, index) in ms1Peaklist.WithIndex()) {
                var diff = Math.Abs(peak.ChromXs.Value - chromPeakFeature.ChromXs.Value);
                if (diff < minimumDiff) {
                    minimumDiff = diff; minimumID = index;
                }
            }
            int topScanNum = minimumID;
            var smoothedMs2ChromPeaksList = new List<List<ChromatogramPeak>>();
            var ms2ChromPeaksList = DataAccess.GetMs2Peaklistlist(spectrumList, precursorMz, startScanNum, endScanNum,
                curatedSpectra.Select(x => x.Mass).ToList(), param, targetCE);

            foreach (var chromPeaks in ms2ChromPeaksList) {
                var sChromPeaks = DataAccess.GetSmoothedPeaklist(chromPeaks, param.SmoothingMethod, param.SmoothingLevel);
                smoothedMs2ChromPeaksList.Add(sChromPeaks);
            }

            //Do MS2Dec deconvolution
            if (smoothedMs2ChromPeaksList.Count > 0) {





                var msdecResult = MSDecHandler.GetMSDecResult(smoothedMs2ChromPeaksList, param, topScanNum);
                if (msdecResult == null) //if null (any pure chromatogram is not found.)
                    return MSDecObjectHandler.GetMSDecResultByRawSpectrum(chromPeakFeature, curatedSpectra);
                else {
                    if (param.KeepOriginalPrecursorIsotopes) { //replace deconvoluted precursor isotopic ions by the original precursor ions
                        msdecResult.Spectrum = MSDecObjectHandler.ReplaceDeconvolutedIsopicIonsToOriginalPrecursorIons(msdecResult, curatedSpectra, chromPeakFeature, param);
                    }
                }
                msdecResult.ChromXs = chromPeakFeature.ChromXs;
                msdecResult.PrecursorMz = precursorMz;
                return msdecResult;
            }
            
            return MSDecObjectHandler.GetDefaultMSDecResult(chromPeakFeature);
        }
    }
}

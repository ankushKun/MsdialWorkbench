﻿using Accord.Diagnostics;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Mathematics.Basic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Components {
    public sealed class Chromatogram_temp2 {
        private readonly IReadOnlyList<ValuePeak> _peaks;
        private readonly ChromXType _type;
        private readonly ChromXUnit _unit;
        private readonly Algorithm.ChromSmoothing.Smoothing _smoother;

        public Chromatogram_temp2(IEnumerable<ValuePeak> peaks, ChromXType type, ChromXUnit unit) {
            _peaks = peaks as IReadOnlyList<ValuePeak> ?? peaks?.ToArray() ?? throw new ArgumentNullException(nameof(peaks));
            _type = type;
            _unit = unit;
            _smoother = new Algorithm.ChromSmoothing.Smoothing();
        }

        private Chromatogram_temp2(ValuePeak[] peaks, ChromXType type, ChromXUnit unit, Algorithm.ChromSmoothing.Smoothing smoother) {
            _peaks = peaks;
            _type = type;
            _unit = unit;
            _smoother = smoother;
        }

        public IReadOnlyList<ValuePeak> Peaks => _peaks;
        public bool IsEmpty => _peaks.Count == 0;
        public int Length => _peaks.Count;

        public double Time(int index) {
            return _peaks[index].Time;
        }

        public ChromXs PeakChromXs(double chromValue, double mz) {
            var result = new ChromXs(chromValue, _type, _unit);
            if (_type != ChromXType.Mz) {
                result.Mz = new MzValue(mz);
            }
            return result;
        }

        public bool IsValidPeakTop(int topId) {
            return topId - 1 >= 0 && topId + 1 <= _peaks.Count - 1
                && _peaks[topId - 1].Intensity > 0 && _peaks[topId + 1].Intensity > 0;
        }

        public int CountSpikes(int leftId, int rightId, double threshold) {
            var leftBound = Math.Max(leftId, 1);
            var rightBound = Math.Min(rightId, _peaks.Count - 2);

            var counter = 0;
            double? spikeMax = null, spikeMin = null;
            for (int i = leftBound; i <= rightBound; i++) {
                if (IsPeakTop(i)) {
                    spikeMax = _peaks[i].Intensity;
                }
                else if (IsBottom(i)) {
                    spikeMin = _peaks[i].Intensity;
                }
                if (spikeMax.HasValue && spikeMin.HasValue) {
                    var noise = Math.Abs(spikeMax.Value - spikeMin.Value) / 2;
                    if (noise > threshold) {
                        counter++;
                    }
                    spikeMax = null; spikeMin = null;
                }
            }
            return counter;
        }

        public bool IsPeakTop(int topId) {
            return _peaks[topId - 1].Intensity <= _peaks[topId].Intensity
                && _peaks[topId].Intensity >= _peaks[topId + 1].Intensity;
        }

        public bool IsBottom(int bottomId) {
            return _peaks[bottomId - 1].Intensity >= _peaks[bottomId].Intensity
                && _peaks[bottomId].Intensity <= _peaks[bottomId + 1].Intensity;
        }

        public ValuePeak[] TrimPeaks(int left, int right) {
            var result = new ValuePeak[right - left + 1];
            for (int i = 0; i < result.Length; i++) {
                result[i] = _peaks[i + left];
            }
            return result;
        }

        public double GetIntensityMedian() {
            return BasicMathematics.InplaceSortMedian(_peaks.Select(peak => peak.Intensity).ToArray());
        }

        public double GetMaximumIntensity() {
            return _peaks.Select(peak => peak.Intensity).DefaultIfEmpty().Max();
        }

        public double GetMinimumIntensity() {
            return _peaks.Select(peak => peak.Intensity).DefaultIfEmpty().Min();
        }

        public double GetMinimumNoiseLevel(int binSize, int minWindowSize, double minNoiseLevel) {
            var amplitudeDiffs = _peaks
                .Chunk(binSize)
                .Where(bin => bin.Length >= 1)
                .Select(bin => bin.Max(peak => peak.Intensity) - bin.Min(peak => peak.Intensity))
                .Where(diff => diff > 0)
                .ToArray();
            if (amplitudeDiffs.Length >= minWindowSize) {
                return BasicMathematics.InplaceSortMedian(amplitudeDiffs);
            }
            else {
                return minNoiseLevel;
            }
        }

        public Chromatogram_temp2 Difference(Chromatogram_temp2 other) {
            Debug.Assert(_type == other._type);
            Debug.Assert(_unit == other._unit);
            var peaks = new ValuePeak[_peaks.Count];
            for (int i = 0; i < peaks.Length; i++) {
                peaks[i] = new ValuePeak(_peaks[i].Id, _peaks[i].Time, _peaks[i].Mz, Math.Max(0, _peaks[i].Intensity - other._peaks[i].Intensity));
            }
            return new Chromatogram_temp2(peaks, _type, _unit, _smoother);
        }

        public Chromatogram_temp2 ChromatogramSmoothing(SmoothingMethod method, int level) {
            switch (method) {
                case SmoothingMethod.SimpleMovingAverage:
                    return new Chromatogram_temp2(Algorithm.ChromSmoothing.Smoothing.SimpleMovingAverage(_peaks, level), _type, _unit);
                case SmoothingMethod.SavitzkyGolayFilter:
                    return new Chromatogram_temp2(Algorithm.ChromSmoothing.Smoothing.SavitxkyGolayFilter(_peaks, level), _type, _unit);
                case SmoothingMethod.BinomialFilter:
                    return new Chromatogram_temp2(Algorithm.ChromSmoothing.Smoothing.BinomialFilter(_peaks, level), _type, _unit);
                case SmoothingMethod.LowessFilter:
                    return new Chromatogram_temp2(Algorithm.ChromSmoothing.Smoothing.LowessFilter(_peaks, level), _type, _unit);
                case SmoothingMethod.LoessFilter:
                    return new Chromatogram_temp2(Algorithm.ChromSmoothing.Smoothing.LoessFilter(_peaks, level), _type, _unit);
                case SmoothingMethod.LinearWeightedMovingAverage:
                default:
                    return new Chromatogram_temp2(_smoother.LinearWeightedMovingAverage(_peaks, level), _type, _unit, _smoother);
            }
        }

        public ChromatogramGlobalProperty_temp2 GetProperty(int noiseEstimateBin, int minNoiseWindowSize, double minNoiseLevel, double noiseFactor) {
            var ssChromatogram = ChromatogramSmoothing(SmoothingMethod.LinearWeightedMovingAverage, 1).ChromatogramSmoothing(SmoothingMethod.LinearWeightedMovingAverage, 1);
            var baselineChromatogram = ChromatogramSmoothing(SmoothingMethod.LinearWeightedMovingAverage, 20);
            var baselineCorrectedChromatogram = ssChromatogram.Difference(baselineChromatogram);
            var noise = baselineCorrectedChromatogram.GetMinimumNoiseLevel(noiseEstimateBin, minNoiseWindowSize, minNoiseLevel) * noiseFactor;

            // checking chromatogram properties
            var baselineMedian = GetIntensityMedian();
            var maxChromIntensity = GetMaximumIntensity();
            var minChromIntensity = GetMinimumIntensity();
            var isHighBaseline = baselineMedian > (maxChromIntensity + minChromIntensity) * 0.5;
            return new ChromatogramGlobalProperty_temp2(maxChromIntensity, minChromIntensity, baselineMedian, noise, isHighBaseline, ssChromatogram, baselineChromatogram, baselineCorrectedChromatogram);
        }
    }
}

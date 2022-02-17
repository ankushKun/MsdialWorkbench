﻿using CompMs.Common.DataObj.Result;
using CompMs.Common.Extension;
using CompMs.Common.Parameter;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.MsdialCore.Algorithm.Annotation
{
    public class MsScanMatchResultEvaluator : IMatchResultEvaluator<MsScanMatchResult>
    {
        private readonly MsRefSearchParameterBase searchParameter;

        private MsScanMatchResultEvaluator(MsRefSearchParameterBase searchParameter) {
            this.searchParameter = searchParameter ?? throw new ArgumentNullException(nameof(searchParameter));
        }

        public List<MsScanMatchResult> FilterByThreshold(IEnumerable<MsScanMatchResult> results) {
            if (results is null) {
                throw new ArgumentNullException(nameof(results));
            }

            return results.Where(result => result.IsAnnotationSuggested || result.IsReferenceMatched).ToList();
        }

        public bool IsAnnotationSuggested(MsScanMatchResult result) {
            if (result is null) {
                throw new ArgumentNullException(nameof(result));
            }
            return result.IsAnnotationSuggested;
        }

        public bool IsReferenceMatched(MsScanMatchResult result) {
            if (result is null) {
                throw new ArgumentNullException(nameof(result));
            }

            return result.IsReferenceMatched;
        }

        public List<MsScanMatchResult> SelectReferenceMatchResults(IEnumerable<MsScanMatchResult> results) {
            if (results is null) {
                throw new ArgumentNullException(nameof(results));
            }

            return results.Where(result => result.IsReferenceMatched).ToList();
        }

        public MsScanMatchResult SelectTopHit(IEnumerable<MsScanMatchResult> results) {
            if (results is null) {
                throw new ArgumentNullException(nameof(results));
            }

            return results.DefaultIfEmpty().Argmax(result => (result?.IsReferenceMatched ?? false, result?.IsAnnotationSuggested ?? false, result?.TotalScore ?? double.MinValue));
        }

        public static MsScanMatchResultEvaluator CreateEvaluator(MsRefSearchParameterBase searchParameter) {
            if (searchParameter is null) {
                throw new ArgumentNullException(nameof(searchParameter));
            }

            return new MsScanMatchResultEvaluator(searchParameter);
        }
    }
}

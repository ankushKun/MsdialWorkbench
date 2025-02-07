﻿using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.MessagePack;
using MessagePack;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.MsdialCore.DataObj {
    [MessagePackObject]
    public class AlignmentResultContainer
    {
        [Key(0)]
        public Ionization Ionization { get; set; }
        [Key(1)]
        public int AlignmentResultFileID { get; set; }
        [Key(2)]
        public int TotalAlignmentSpotCount { get; set; }
        [Key(3)]
        public ObservableCollection<AlignmentSpotProperty> AlignmentSpotProperties { get; set; }
        [Key(4)]
        public bool IsNormalized { get; set; }
        [IgnoreMember]
        public Task LoadAlginedPeakPropertiesTask { get; private set; } = Task.CompletedTask;
        //public AnalysisParametersBean AnalysisParamForLC { get; set; }
        //public AnalysisParamOfMsdialGcms AnalysisParamForGC { get; set; }

        public void Save(AlignmentFileBean file) {
            var peakProperty = AlignmentSpotProperties.Select(x => x.AlignedPeakProperties).ToList();
            var driftProperty = AlignmentSpotProperties.Select(prop => prop.AlignmentDriftSpotFeatures).ToList();
            foreach (var b in AlignmentSpotProperties) {
                b.AlignedPeakProperties = null;
                b.AlignmentDriftSpotFeatures = null;
            }

            MessagePackHandler.SaveToFile(this, file.FilePath);
            LoadAlginedPeakPropertiesTask.Wait();
            var chromatogramPeakFile = Path.Combine(Path.GetDirectoryName(file.FilePath), Path.GetFileNameWithoutExtension(file.FilePath) + "_PeakProperties" + Path.GetExtension(file.FilePath));
            var driftSpotFile = Path.Combine(Path.GetDirectoryName(file.FilePath), Path.GetFileNameWithoutExtension(file.FilePath) + "_DriftSopts" + Path.GetExtension(file.FilePath));
            MessagePackDefaultHandler.SaveLargeListToFile(peakProperty, chromatogramPeakFile);
            MessagePackDefaultHandler.SaveLargeListToFile(driftProperty, driftSpotFile);

            for (var i = 0; i < peakProperty.Count; i++) {
                AlignmentSpotProperties[i].AlignedPeakProperties = peakProperty[i];
                AlignmentSpotProperties[i].AlignmentDriftSpotFeatures = driftProperty[i];
            }
        }

        public static AlignmentResultContainer Load(AlignmentFileBean file)
        {
            var containerFile = file.FilePath;
            var chromatogramPeakFile = Path.Combine(Path.GetDirectoryName(file.FilePath), Path.GetFileNameWithoutExtension(file.FilePath) + "_PeakProperties" + Path.GetExtension(file.FilePath));
            var driftSpotFile = Path.Combine(Path.GetDirectoryName(file.FilePath), Path.GetFileNameWithoutExtension(file.FilePath) + "_DriftSopts" + Path.GetExtension(file.FilePath));

            var result = MessagePackHandler.LoadFromFile<AlignmentResultContainer>(containerFile);
            if (result is null) {
                return null;
            }

            var collection = result.AlignmentSpotProperties;

            if (collection != null && collection.Count > 0 && collection[0].AlignedPeakProperties is null)
            {
                if (File.Exists(chromatogramPeakFile)) {
                    var alignmentChromPeakFeatures = MessagePackDefaultHandler.LoadLargerListFromFile<List<AlignmentChromPeakFeature>>(chromatogramPeakFile);
                    for (var i = 0; i < alignmentChromPeakFeatures.Count; i++) {
                        collection[i].AlignedPeakProperties = alignmentChromPeakFeatures[i];
                    }
                }
                if (File.Exists(driftSpotFile)) {
                    var alignmentDriftSpotProperties = MessagePackDefaultHandler.LoadLargerListFromFile<List<AlignmentSpotProperty>>(driftSpotFile);
                    for (var i = 0; i < alignmentDriftSpotProperties.Count; i++) {
                        collection[i].AlignmentDriftSpotFeatures = alignmentDriftSpotProperties[i];
                    }
                }
            }
            return result;
        }

        public static AlignmentResultContainer LoadLazy(AlignmentFileBean file, CancellationToken token = default) {
            var containerFile = file.FilePath;
            var result = MessagePackHandler.LoadFromFile<AlignmentResultContainer>(containerFile);
            if (result is null) {
                return null;
            }

            var collection = result.AlignmentSpotProperties;
            if (collection != null && collection.Count > 0 && collection[0].AlignedPeakProperties is null) {
                var driftSpotFile = Path.Combine(Path.GetDirectoryName(file.FilePath), Path.GetFileNameWithoutExtension(file.FilePath) + "_DriftSopts" + Path.GetExtension(file.FilePath));
                if (File.Exists(driftSpotFile)) {
                    var alignmentDriftSpotProperties = MessagePackDefaultHandler.LoadLargerListFromFile<List<AlignmentSpotProperty>>(driftSpotFile);
                    foreach (var (alignmentChromPeakFeature, i) in alignmentDriftSpotProperties.WithIndex()) {
                        collection[i].AlignmentDriftSpotFeatures = alignmentChromPeakFeature;
                    }
                }

                var chromatogramPeakFile = Path.Combine(Path.GetDirectoryName(file.FilePath), Path.GetFileNameWithoutExtension(file.FilePath) + "_PeakProperties" + Path.GetExtension(file.FilePath));
                if (File.Exists(chromatogramPeakFile)) {
                    var task = Task.FromResult<List<AlignmentChromPeakFeature>>(null);
                    var alignmentChromPeakFeatures = MessagePackDefaultHandler.LoadIncrementalLargerListFromFile<List<AlignmentChromPeakFeature>>(chromatogramPeakFile).SelectMany(featuress => featuress);
                    var enumerator = alignmentChromPeakFeatures.GetEnumerator();
                    foreach (var c in collection) {
                        c.AlignedPeakPropertiesTask = task = task.ContinueWith(_ =>
                        {
                            if (enumerator.MoveNext()) {
                                return enumerator.Current;
                            }
                            return new List<AlignmentChromPeakFeature>(0);
                        },
                        token);
                    }
                    var allTaskCompleted = Task.Run(async () =>
                    {
                        try {
                            await task.ConfigureAwait(false);
                        }
                        finally {
                            enumerator.Dispose();
                        }
                    }, token);
                    result.LoadAlginedPeakPropertiesTask = allTaskCompleted;
                }
            }
            return result;
        }
    }
}

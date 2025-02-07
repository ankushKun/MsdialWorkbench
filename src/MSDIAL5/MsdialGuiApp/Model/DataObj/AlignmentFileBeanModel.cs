﻿using CompMs.App.Msdial.Model.Loader;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialCore.Parser;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace CompMs.App.Msdial.Model.DataObj
{
    public sealed class AlignmentFileBeanModel : BindableBase, IFileBean
    {
        private readonly AlignmentFileBean _alignmentFile;
        private readonly SemaphoreSlim _alignmentResultSem;

        public AlignmentFileBeanModel(AlignmentFileBean alignmentFile) {
            _alignmentFile = alignmentFile;
            _alignmentResultSem = new SemaphoreSlim(1, 1);
        }

        public string FileName => _alignmentFile.FileName;

        public async Task<AlignmentResultContainer> LoadAlignmentResultAsync(CancellationToken token = default) {
            await _alignmentResultSem.WaitAsync(token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            try {
                var container = AlignmentResultContainer.LoadLazy(_alignmentFile, token);
                _ = container.LoadAlginedPeakPropertiesTask.ContinueWith(_ => _alignmentResultSem.Release());
                return container;
            }
            catch {
                _alignmentResultSem.Release();
                throw;
            }
        }

        public async Task SaveAlignmentResultAsync(AlignmentResultContainer container) {
            await _alignmentResultSem.WaitAsync().ConfigureAwait(false);
            try {
                container.Save(_alignmentFile);
            }
            finally {
                _alignmentResultSem.Release();
            }
        }

        public MSDecLoader CreateMSDecLoader() {
            return new MSDecLoader(_alignmentFile.SpectraFilePath);
        }

        public List<MSDecResult> ReadMSDecResults() {
            return MsdecResultsReader.ReadMSDecResults(_alignmentFile.SpectraFilePath, out _, out _);
        }

        public AlignmentEicLoader CreateEicLoader(ChromatogramSerializer<ChromatogramSpotInfo> deserializer, AnalysisFileBeanModelCollection analysisFiles, ProjectBaseParameterModel projectBaseParameter) {
            return new AlignmentEicLoader(deserializer, _alignmentFile.EicFilePath, analysisFiles, projectBaseParameter);
        }

        public ProteinResultContainer LoadProteinResult() {
            return MsdialProteomicsSerializer.LoadProteinResultContainer(_alignmentFile.ProteinAssembledResultFilePath);
        }

        // Implements IFileBean interface
        int IFileBean.FileID => _alignmentFile.FileID;
        string IFileBean.FilePath => _alignmentFile.FilePath;
    }
}

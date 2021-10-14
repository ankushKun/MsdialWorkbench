﻿using CompMs.Common.MessagePack;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Parser;
using CompMs.MsdialGcMsApi.Parameter;
using MessagePack;
using System.IO;
using System.Threading.Tasks;

namespace CompMs.MsdialGcMsApi.DataObj
{
    [MessagePackObject]
    public class MsdialGcmsDataStorage : MsdialDataStorageBase, IMsdialDataStorage<MsdialGcmsParameter> {
        [Key(6)]
        public MsdialGcmsParameter MsdialGcmsParameter { get; set; }

        MsdialGcmsParameter IMsdialDataStorage<MsdialGcmsParameter>.Parameter => MsdialGcmsParameter;

        protected override void SaveMsdialDataStorageCore(Stream stream) {
            MessagePackDefaultHandler.SaveToStream(this, stream);
        }

        public static IMsdialSerializer Serializer { get; } = new MsdialGcmsSerializer();

        class MsdialGcmsSerializer : MsdialSerializerInner, IMsdialSerializer
        {
            protected override async Task<IMsdialDataStorage<ParameterBase>> LoadMsdialDataStorageCoreAsync(IStreamManager streamManager, string path) {
                using (var stream = await streamManager.Get(path).ConfigureAwait(false)) {
                    return MessagePackDefaultHandler.LoadFromStream<MsdialGcmsDataStorage>(stream);
                }
            }
        }
    }
}

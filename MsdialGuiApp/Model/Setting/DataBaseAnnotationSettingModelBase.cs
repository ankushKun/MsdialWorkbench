﻿using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Parameter;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.Parameter;
using CompMs.MsdialCore.Utility;
using System.Collections.Generic;

namespace CompMs.App.Msdial.Model.Setting
{

    public abstract class DataBaseAnnotationSettingModelBase : BindableBase, IAnnotationSettingModel
    {
        public DataBaseAnnotationSettingModelBase() {

        }

        public DataBaseAnnotationSettingModelBase(DataBaseAnnotationSettingModelBase model) {
            DataBasePath = model.DataBasePath;
            DataBaseID = model.DataBaseID;
            DBSource = model.DBSource;
            AnnotationSource = model.AnnotationSource;
            AnnotatorID = model.AnnotatorID;
            Parameter = model.Parameter;
        }

        public string DataBasePath {
            get => dataBasePath;
            set => SetProperty(ref dataBasePath, value);
        }
        private string dataBasePath = string.Empty;

        public string DataBaseID {
            get => dataBaseID;
            set => SetProperty(ref dataBaseID, value);
        }
        private string dataBaseID = string.Empty;

        public DataBaseSource DBSource {
            get => source;
            set => SetProperty(ref source, value);
        }
        private DataBaseSource source = DataBaseSource.None;

        public SourceType AnnotationSource {
            get => annotationSource;
            set => SetProperty(ref annotationSource, value);
        }
        private SourceType annotationSource;

        public string AnnotatorID {
            get => annotatorID;
            set => SetProperty(ref annotatorID, value);
        }
        private string annotatorID;

        public MsRefSearchParameterBase Parameter {
            get => parameter;
            set => SetProperty(ref parameter, value);
        }
        private MsRefSearchParameterBase parameter = new MsRefSearchParameterBase();

        public abstract ISerializableAnnotatorContainer<IAnnotationQuery, MoleculeMsReference, MsScanMatchResult> Build(ParameterBase parameter);

        protected static List<MoleculeMsReference> LoadMspDataBase(string path, DataBaseSource source, ParameterBase parameter) {
            List<MoleculeMsReference> db;
            switch (source) {
                case DataBaseSource.Msp:
                    db = LibraryHandler.ReadMspLibrary(path);
                    break;
                case DataBaseSource.Lbm:
                    db = LibraryHandler.ReadLipidMsLibrary(path, parameter);
                    break;
                default:
                    db = new List<MoleculeMsReference>(0);
                    break;
            }
            for(int i = 0; i< db.Count; i++) {
                db[i].ScanID = i;
            }
            return db;
        }
    }
}
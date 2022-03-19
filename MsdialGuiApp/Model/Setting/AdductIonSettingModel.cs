﻿using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.Extension;
using CompMs.Common.Parser;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.Parameter;
using System.Collections.ObjectModel;
using System.Linq;

namespace CompMs.App.Msdial.Model.Setting
{
    public class AdductIonSettingModel : BindableBase
    {
        public AdductIonSettingModel(ParameterBase parameter, ProcessOption process) {
            referenceParameter = parameter.ReferenceFileParam;
            if (referenceParameter.SearchedAdductIons.IsEmptyOrNull()) {
                referenceParameter.SearchedAdductIons = AdductResourceParser.GetAdductIonInformationList(parameter.IonMode);
            }
            AdductIons = new ObservableCollection<AdductIon>(referenceParameter.SearchedAdductIons);
            IsReadOnly = (process & ProcessOption.Identification) == 0;
        }

        public bool IsReadOnly { get; }

        private readonly ReferenceBaseParameter referenceParameter;

        public string UserDefinedAdductName {
            get => userDefinedAdductName;
            set => SetProperty(ref userDefinedAdductName, value);
        }
        private string userDefinedAdductName;

        public AdductIon UserDefinedAdduct => AdductIon.GetAdductIon(userDefinedAdductName);

        public ObservableCollection<AdductIon> AdductIons { get; }

        public void AddAdductIon() {
            var adduct = UserDefinedAdduct;
            if (adduct?.FormatCheck ?? false) {
                AdductIons.Add(adduct);
            }
        }

        public void RemoveAdductIon(AdductIon adduct) {
            if (AdductIons.Contains(adduct)) {
                AdductIons.Remove(adduct);
            }
        }

        public void Commit() {
            if (IsReadOnly) {
                return;
            }
            referenceParameter.SearchedAdductIons = AdductIons.ToList();
        }
    }
}

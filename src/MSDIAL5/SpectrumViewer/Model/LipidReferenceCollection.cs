﻿using CompMs.Common.DataObj.Property;
using CompMs.Common.Interfaces;
using CompMs.Common.Lipidomics;
using CompMs.CommonMVVM;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace CompMs.App.SpectrumViewer.Model
{
    public class LipidReferenceCollection : BindableBase, IScanCollection
    {
        public LipidReferenceCollection() {
            Adducts = new List<AdductIon> {
                AdductIon.GetAdductIon("[M+H]+"),
                AdductIon.GetAdductIon("[M+NH4]+"),
                AdductIon.GetAdductIon("[M+Na]+"),
            }.AsReadOnly();
            Adduct = Adducts.First();
            Scans = new ObservableCollection<IMSScanProperty>();

            lipidParser = FacadeLipidParser.Default;
            lipidGenerator = new LipidGenerator(new TotalChainVariationGenerator(new Omega3nChainGenerator(), 6));
            spectrumGenerator = FacadeLipidSpectrumGenerator.Default;
        }

        public string Name { get => Lipid.ToString(); }

        public ILipid Lipid {
            get => lipid;
            private set {
                if (SetProperty(ref lipid, value)) {
                    OnPropertyChanged(nameof(Name));
                }
            }
        }
        private ILipid lipid;

        public AdductIon Adduct {
            get => adduct;
            set => SetProperty(ref adduct, value);
        }
        private AdductIon adduct;

        public ReadOnlyCollection<AdductIon> Adducts { get; }

        public ObservableCollection<IMSScanProperty> Scans { get; }

        private readonly LipidGenerator lipidGenerator;
        private readonly ILipidSpectrumGenerator spectrumGenerator;
        private readonly ILipidParser lipidParser;

        public void SetLipid(string lipidStr) {
            Lipid = lipidParser.Parse(lipidStr);
        }

        public void Generate(string lipidStr) {
            SetLipid(lipidStr);
            Scans.Clear();
            if (Lipid == null) {
                return;
            }
            foreach (var lipid in GenerateLipid(Lipid, lipidGenerator)) {
                Scans.Add(lipid.GenerateSpectrum(spectrumGenerator, Adduct));
            }
        }

        private static IEnumerable<ILipid> GenerateLipid(ILipid lipid, ILipidGenerator generator) {
            yield return lipid;
            foreach (var lipid_ in lipid.Generate(generator).SelectMany(l => GenerateLipid(l, generator))) {
                yield return lipid_;
            }
        }
    }
}

﻿using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.Interfaces;
using System.Collections.Generic;

namespace CompMs.Common.Lipidomics
{
    public enum LipidDescription { None = 0, Class = 1, Chain = 2, SnPosition = 4, DoubleBondPosition = 8, EZ = 16 }
    public interface ILipid
    {
        string Name { get; }
        LbmClass LipidClass { get; }
        double Mass { get; } // TODO: Formula class maybe better.
        int AnnotationLevel { get; }
        LipidDescription Description { get; }
        int ChainCount { get; }
        ITotalChain Chains { get; }

        IEnumerable<ILipid> Generate(ILipidGenerator generator);
        IMSScanProperty GenerateSpectrum(ILipidSpectrumGenerator generator, AdductIon adduct, IMoleculeProperty molecule = null); 
    }

    public class Lipid : ILipid
    {
        public Lipid(LbmClass lipidClass, double mass, ITotalChain chains) {
            LipidClass = lipidClass;
            Mass = mass;
            AnnotationLevel = GetAnnotationLevel(chains);
            Chains = chains ?? throw new System.ArgumentNullException(nameof(chains));
            Description = chains.Description;
        }

        public string Name => ToString();
        public LbmClass LipidClass { get; }
        public double Mass { get; }
        public int AnnotationLevel { get; } = 1;
        public LipidDescription Description { get; } = LipidDescription.None;

        public int ChainCount => Chains.CarbonCount;
        public ITotalChain Chains { get; }

        public IEnumerable<ILipid> Generate(ILipidGenerator generator) {
            return generator.Generate(this);
        }

        public IMSScanProperty GenerateSpectrum(ILipidSpectrumGenerator generator, AdductIon adduct, IMoleculeProperty molecule = null) {
            return generator.Generate(this, adduct, molecule);
        }

        // temporary ToString method.
        public override string ToString() {
            return $"{LipidClassDictionary.Default.LbmItems[LipidClass].DisplayName} {Chains}";
        }

        private static int GetAnnotationLevel(ITotalChain chains) {
            switch (chains) {
                case TotalChain _:
                    return 1;
                case MolecularSpeciesLevelChains _:
                    return 2;
                case PositionLevelChains _:
                    return 3;
                default:
                    return 0;
            }
        }
    }
}

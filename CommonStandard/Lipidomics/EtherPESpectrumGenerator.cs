﻿using CompMs.Common.Components;
using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.FormulaGenerator.DataObj;
using CompMs.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Lipidomics
{
    public class EtherPESpectrumGenerator : ILipidSpectrumGenerator
    {
        private static readonly double C2H8NO4P = new[]
        {
            MassDiffDictionary.CarbonMass * 2,
            MassDiffDictionary.HydrogenMass * 8,
            MassDiffDictionary.NitrogenMass,
            MassDiffDictionary.OxygenMass * 4,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double C2H5N = new[]
        {
            MassDiffDictionary.CarbonMass * 2,
            MassDiffDictionary.HydrogenMass * 5,
            MassDiffDictionary.NitrogenMass,
        }.Sum();

        private static readonly double H3PO4 = new[]
        {
            MassDiffDictionary.HydrogenMass * 3,
            MassDiffDictionary.PhosphorusMass,
            MassDiffDictionary.OxygenMass * 4,
        }.Sum();

        private static readonly double Gly_C = new[] {
            MassDiffDictionary.CarbonMass * 5,
            MassDiffDictionary.HydrogenMass * 12,
            MassDiffDictionary.NitrogenMass,
            MassDiffDictionary.OxygenMass * 4,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double Gly_O = new[] {
            MassDiffDictionary.CarbonMass * 4,
            MassDiffDictionary.HydrogenMass * 10,
            MassDiffDictionary.NitrogenMass,
            MassDiffDictionary.OxygenMass * 5,
            MassDiffDictionary.PhosphorusMass,
        }.Sum();

        private static readonly double CH2 = new[]
        {
            MassDiffDictionary.HydrogenMass * 2,
            MassDiffDictionary.CarbonMass,
        }.Sum();

        private static readonly double H2O = new[]
        {
            MassDiffDictionary.HydrogenMass * 2,
            MassDiffDictionary.OxygenMass,
        }.Sum();

        public EtherPESpectrumGenerator()
        {
            spectrumGenerator = new SpectrumPeakGenerator();
        }

        public EtherPESpectrumGenerator(ISpectrumPeakGenerator peakGenerator)
        {
            this.spectrumGenerator = peakGenerator ?? throw new System.ArgumentNullException(nameof(peakGenerator));
        }

        private readonly ISpectrumPeakGenerator spectrumGenerator;


        public bool CanGenerate(ILipid lipid, AdductIon adduct)
        {
            if (lipid.LipidClass == LbmClass.EtherPE)
            {
                if (adduct.AdductIonName == "[M+H]+" || adduct.AdductIonName == "[M+Na]+")
                {
                    return true;
                }
            }
            return false;
        }

        public IMSScanProperty Generate(Lipid lipid, AdductIon adduct, IMoleculeProperty molecule = null)
        {
            var spectrum = new List<SpectrumPeak>();
            spectrum.AddRange(GetEtherPESpectrum(lipid, adduct));
            if (lipid.Chains is PositionLevelChains plChains)
            {
                spectrum.AddRange(GetAlkylPositionSpectrum(lipid, plChains.Chains[0], adduct));
                if (plChains.Chains[0] is AlkylChain alkyl)
                {
                    if (alkyl.DoubleBond.Bonds.Any(b => b.Position == 1))
                    {
                        spectrum.AddRange(GetEtherPEPSpectrum(lipid, alkyl, plChains.Chains[1], adduct));
                    }
                    else
                    {
                        spectrum.AddRange(GetEtherPEOSpectrum(lipid, plChains.Chains[0], plChains.Chains[1], adduct));
                    }
                    spectrum.AddRange(spectrumGenerator.GetAlkylDoubleBondSpectrum(lipid, alkyl, adduct, 0d, 30d));
                    //spectrum.AddRange(spectrumGenerator.GetAlkylDoubleBondSpectrum(lipid, alkyl, adduct, nlMass: C2H8NO4P, 30d));
                }
                if (plChains.Chains[1] is AcylChain acyl)
                {
                    spectrum.AddRange(spectrumGenerator.GetAcylDoubleBondSpectrum(lipid, acyl, adduct, 0d, 50d));
                    //spectrum.AddRange(spectrumGenerator.GetAcylDoubleBondSpectrum(lipid, acyl, adduct, nlMass: C2H8NO4P, 50d));
                }
            }
            spectrum = spectrum.GroupBy(spec => spec, comparer)
                .Select(specs => new SpectrumPeak(specs.First().Mass, specs.Sum(n => n.Intensity), string.Join(", ", specs.Select(spec => spec.Comment))))
                .OrderBy(peak => peak.Mass)
                .ToList();
            return CreateReference(lipid, adduct, spectrum, molecule);
        }

        private MoleculeMsReference CreateReference(ILipid lipid, AdductIon adduct, List<SpectrumPeak> spectrum, IMoleculeProperty molecule)
        {
            return new MoleculeMsReference
            {
                PrecursorMz = lipid.Mass + adduct.AdductIonAccurateMass,
                IonMode = adduct.IonMode,
                Spectrum = spectrum,
                Name = lipid.Name,
                Formula = molecule?.Formula,
                Ontology = molecule?.Ontology,
                SMILES = molecule?.SMILES,
                InChIKey = molecule?.InChIKey,
                AdductType = adduct,
                CompoundClass = lipid.LipidClass.ToString(),
                Charge = adduct.ChargeNumber,
            };
        }

        private SpectrumPeak[] GetEtherPESpectrum(ILipid lipid, AdductIon adduct)
        {
            var spectrum = new List<SpectrumPeak>
            {
                new SpectrumPeak(lipid.Mass + adduct.AdductIonAccurateMass, 999d, "Precursor") { SpectrumComment = SpectrumComment.precursor },
                new SpectrumPeak(C2H8NO4P + adduct.AdductIonAccurateMass, 200d, "Header"),
                new SpectrumPeak(Gly_C + adduct.AdductIonAccurateMass, 200d, "Gly-C"),
                new SpectrumPeak(Gly_O + adduct.AdductIonAccurateMass, 200d, "Gly-O"),
            };
            if (adduct.AdductIonName == "[M+Na]+")
            {
                spectrum.AddRange
                (
                     new[]
                     {
                        new SpectrumPeak(lipid.Mass - C2H8NO4P + MassDiffDictionary.ProtonMass, 150d, "Precursor -C2H8NO4P"),
                        new SpectrumPeak(lipid.Mass - C2H5N + adduct.AdductIonAccurateMass, 150d, "Precursor -C2H5N"),
                        new SpectrumPeak(lipid.Mass - C2H5N - H2O + MassDiffDictionary.HydrogenMass + adduct.AdductIonAccurateMass, 500d, "Precursor -C2H6NO"),
                     }
                );
            }
            else
            {
                spectrum.AddRange
                (
                     new[]
                     {
                        new SpectrumPeak(lipid.Mass - C2H8NO4P + MassDiffDictionary.ProtonMass, 500d, "Precursor -C2H8NO4P"),
                     }
                );

            }
            return spectrum.ToArray();
        }

        private SpectrumPeak[] GetEtherPEPSpectrum(ILipid lipid, IChain alkylChain, IChain acylChain, AdductIon adduct)
        {
            var lipidMass = lipid.Mass + adduct.AdductIonAccurateMass;
            var alkylChainMass = alkylChain.Mass;
            var acylChainMass = acylChain.Mass ;
            var spectrum = new List<SpectrumPeak>
            {
                new SpectrumPeak(lipidMass - alkylChainMass + MassDiffDictionary.HydrogenMass , 100d, $"-{alkylChain}"),
                new SpectrumPeak(lipidMass - acylChainMass , 80d, $"-{acylChain}"),
                new SpectrumPeak(lipidMass - alkylChainMass - MassDiffDictionary.OxygenMass , 150, $"-{alkylChain}-O"),
                new SpectrumPeak(lipidMass - acylChainMass  - H2O, 100d, $"-{acylChain}-O"),
            };
            if (adduct.AdductIonName == "[M+H]+")
            {
                spectrum.AddRange
                (
                     new[]
                     {
                        new SpectrumPeak(alkylChainMass + C2H8NO4P - MassDiffDictionary.HydrogenMass + MassDiffDictionary.ProtonMass, 250d, "Sn1Ether+C2H8NO3P"), // Sn1 + O + C2H8NO3P
                        new SpectrumPeak(alkylChainMass + C2H8NO4P - H3PO4 - MassDiffDictionary.HydrogenMass + MassDiffDictionary.ProtonMass, 150d, "Sn1Ether+C2H8NO3P-H3PO4"),
                        new SpectrumPeak(lipidMass - C2H8NO4P - alkylChain.Mass + MassDiffDictionary.HydrogenMass, 300, "NL of C2H8NO4P+Sn1"),
                     }
                );
            }
            return spectrum.ToArray();
        }

        private SpectrumPeak[] GetEtherPEOSpectrum(ILipid lipid, IChain alkylChain, IChain acylChain, AdductIon adduct)
        {
            var lipidMass = lipid.Mass + adduct.AdductIonAccurateMass;
            var alkylChainMass = alkylChain.Mass - MassDiffDictionary.HydrogenMass;
            var acylChainMass = acylChain.Mass - MassDiffDictionary.HydrogenMass;
            return new[]
            {
                //new SpectrumPeak(acylChain.Mass - MassDiffDictionary.HydrogenMass + MassDiffDictionary.ProtonMass, 750d, "Sn2 acyl"),
                new SpectrumPeak(lipidMass - alkylChainMass, 80d, $"-{alkylChain}"),
                new SpectrumPeak(lipidMass - acylChainMass , 80d, $"-{acylChain}"),
                new SpectrumPeak(lipidMass - alkylChainMass - MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass , 150d, $"-{alkylChain}-O"),
                new SpectrumPeak(lipidMass - acylChainMass - H2O - MassDiffDictionary.HydrogenMass , 150d, $"-{acylChain}-O"),

                new SpectrumPeak(lipidMass - alkylChainMass- C2H8NO4P, 80d, $"-{alkylChain}"),
                new SpectrumPeak(lipidMass - acylChainMass- C2H8NO4P , 150d, $"-{acylChain}"),
                new SpectrumPeak(lipidMass - alkylChainMass- C2H8NO4P - H2O , 150d, $"-{alkylChain}-O"),
                new SpectrumPeak(lipidMass - acylChainMass- C2H8NO4P - H2O - MassDiffDictionary.HydrogenMass , 150d, $"-{acylChain}-O"),

            };
        }

        private SpectrumPeak[] GetAlkylPositionSpectrum(ILipid lipid, IChain alkylChain, AdductIon adduct)
        {
            var lipidMass = lipid.Mass + adduct.AdductIonAccurateMass;
            var chainMass = alkylChain.Mass - MassDiffDictionary.HydrogenMass;
            return new[]
            {
                new SpectrumPeak(lipidMass - chainMass - MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass - CH2 , 50d, "-CH2(Sn1)"),
                new SpectrumPeak(lipidMass - chainMass - C2H8NO4P - MassDiffDictionary.OxygenMass - MassDiffDictionary.HydrogenMass - CH2  , 50d, "-Header -CH2(Sn1)"),
            };
        }

        private IEnumerable<SpectrumPeak> GetAlkylDoubleBondSpectrum(ILipid lipid, AlkylChain alkylChain, AdductIon adduct)
        {
            var chainLoss = lipid.Mass - alkylChain.Mass + adduct.AdductIonAccurateMass;
            var diffs = new double[alkylChain.CarbonCount];
            for (int i = 0; i < alkylChain.CarbonCount; i++)
            {
                diffs[i] = CH2;
            }
            foreach (var bond in alkylChain.DoubleBond.Bonds)
            {
                diffs[bond.Position - 1] -= MassDiffDictionary.HydrogenMass;
                diffs[bond.Position] -= MassDiffDictionary.HydrogenMass;
            }
            for (int i = 1; i < alkylChain.CarbonCount; i++)
            {
                diffs[i] += diffs[i - 1];
            }
            return diffs.Take(alkylChain.CarbonCount - 1)
                .Select((diff, i) => new SpectrumPeak(chainLoss + diff, 30d, $"{alkylChain} C{i + 1}"));
        }

        private static readonly IEqualityComparer<SpectrumPeak> comparer = new SpectrumEqualityComparer();
    }
}

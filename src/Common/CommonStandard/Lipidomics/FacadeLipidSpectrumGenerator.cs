﻿using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Lipidomics
{
    public class FacadeLipidSpectrumGenerator : ILipidSpectrumGenerator
    {
        private readonly Dictionary<LbmClass, List<ILipidSpectrumGenerator>> map = new Dictionary<LbmClass, List<ILipidSpectrumGenerator>>();

        public bool CanGenerate(ILipid lipid, AdductIon adduct) {
            if (map.TryGetValue(lipid.LipidClass, out var generators)) {
                return generators.Any(gen => gen.CanGenerate(lipid, adduct));
            }
            return false;
        }

        public IMSScanProperty Generate(Lipid lipid, AdductIon adduct, IMoleculeProperty molecule = null) {
            if (map.TryGetValue(lipid.LipidClass, out var generators)) {
                var generator = generators.FirstOrDefault(gen => gen.CanGenerate(lipid, adduct));
                return generator?.Generate(lipid, adduct, molecule);
            }
            return null;
        }

        public void Add(LbmClass lipidClass, ILipidSpectrumGenerator generator) {
            if (!map.ContainsKey(lipidClass)) {
                map.Add(lipidClass, new List<ILipidSpectrumGenerator>());
            }
            map[lipidClass].Add(generator);
        }

        public void Remove(LbmClass lipidClass, ILipidSpectrumGenerator generator) {
            if (map.ContainsKey(lipidClass)) {
                map[lipidClass].Remove(generator);
            }
        }

        public static ILipidSpectrumGenerator Default {
            get {
                if (@default is null) {
                    var generator = new FacadeLipidSpectrumGenerator();
                    generator.Add(LbmClass.EtherPC, new EtherPCSpectrumGenerator());
                    generator.Add(LbmClass.EtherPE, new EtherPESpectrumGenerator());
                    generator.Add(LbmClass.LPC, new LPCSpectrumGenerator());
                    generator.Add(LbmClass.LPE, new LPESpectrumGenerator());
                    generator.Add(LbmClass.LPG, new LPGSpectrumGenerator());
                    generator.Add(LbmClass.LPI, new LPISpectrumGenerator());
                    generator.Add(LbmClass.LPS, new LPSSpectrumGenerator());
                    generator.Add(LbmClass.PA, new PASpectrumGenerator());
                    generator.Add(LbmClass.PC, new PCSpectrumGenerator());
                    generator.Add(LbmClass.PE, new PESpectrumGenerator());
                    generator.Add(LbmClass.PG, new PGSpectrumGenerator());
                    generator.Add(LbmClass.PI, new PISpectrumGenerator());
                    generator.Add(LbmClass.PS, new PSSpectrumGenerator());
                    generator.Add(LbmClass.MG, new MGSpectrumGenerator());
                    generator.Add(LbmClass.DG, new DGSpectrumGenerator());
                    generator.Add(LbmClass.TG, new TGSpectrumGenerator());
                    generator.Add(LbmClass.BMP, new BMPSpectrumGenerator());
                    generator.Add(LbmClass.HBMP, new HBMPSpectrumGenerator());
                    generator.Add(LbmClass.CL, new CLSpectrumGenerator());
                    generator.Add(LbmClass.SM, new SMSpectrumGenerator());
                    generator.Add(LbmClass.Cer_NS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_NDS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_NP, new CeramidePhytoSphSpectrumGenerator());
                    generator.Add(LbmClass.Cer_AS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_ADS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_AP, new CeramidePhytoSphSpectrumGenerator());
                    generator.Add(LbmClass.Cer_BS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_BDS, new CeramideSpectrumGenerator());
                    //generator.Add(LbmClass.Cer_HS, new CeramideSpectrumGenerator());
                    //generator.Add(LbmClass.Cer_HDS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.HexCer_NS, new HexCerSpectrumGenerator());
                    generator.Add(LbmClass.DGTA, new DGTASpectrumGenerator());
                    generator.Add(LbmClass.DGTS, new DGTSSpectrumGenerator());
                    generator.Add(LbmClass.LDGTA, new LDGTASpectrumGenerator());
                    generator.Add(LbmClass.LDGTS, new LDGTSSpectrumGenerator());
                    generator.Add(LbmClass.GM3, new GM3SpectrumGenerator());
                    generator.Add(LbmClass.SHexCer, new SHexCerSpectrumGenerator());
                    generator.Add(LbmClass.CAR, new CARSpectrumGenerator());
                    generator.Add(LbmClass.DMEDFAHFA, new DMEDFAHFASpectrumGenerator());
                    generator.Add(LbmClass.DMEDFA, new DMEDFASpectrumGenerator());
                    generator.Add(LbmClass.DMEDOxFA, new DMEDFASpectrumGenerator());

                    @default = generator;
                }
                return @default;
            }
        }
        private static ILipidSpectrumGenerator @default;

        public static ILipidSpectrumGenerator OadLipidGenerator {
            get {
                if (@oadlipidgenerator is null) {
                    var generator = new OadDefaultSpectrumGenerator();
                    @oadlipidgenerator = generator;
                }
                return @oadlipidgenerator;
            }
        }
        private static ILipidSpectrumGenerator @oadlipidgenerator;
        public static ILipidSpectrumGenerator EidLipidGenerator
        {
            get
            {
                if (@eidlipidgenerator is null)
                {
                    var generator = new FacadeLipidSpectrumGenerator();
                    generator.Add(LbmClass.PC, new PCEidSpectrumGenerator());
                    generator.Add(LbmClass.LPC, new LPCEidSpectrumGenerator());
                    generator.Add(LbmClass.EtherPC, new EtherPCEidSpectrumGenerator());
                    generator.Add(LbmClass.PE, new PEEidSpectrumGenerator());
                    generator.Add(LbmClass.LPE, new LPEEidSpectrumGenerator());
                    generator.Add(LbmClass.EtherPE, new EtherPEEidSpectrumGenerator());
                    generator.Add(LbmClass.PG, new PGEidSpectrumGenerator());
                    generator.Add(LbmClass.PI, new PIEidSpectrumGenerator());
                    generator.Add(LbmClass.PS, new PSEidSpectrumGenerator());
                    generator.Add(LbmClass.PA, new PAEidSpectrumGenerator());
                    generator.Add(LbmClass.LPG, new LPGEidSpectrumGenerator());
                    generator.Add(LbmClass.LPI, new LPIEidSpectrumGenerator());
                    generator.Add(LbmClass.LPS, new LPSEidSpectrumGenerator());
                    generator.Add(LbmClass.CL, new CLEidSpectrumGenerator());
                    generator.Add(LbmClass.MG, new MGEidSpectrumGenerator());
                    generator.Add(LbmClass.DG, new DGEidSpectrumGenerator());
                    generator.Add(LbmClass.TG, new TGEidSpectrumGenerator());
                    generator.Add(LbmClass.BMP, new BMPEidSpectrumGenerator());
                    generator.Add(LbmClass.HBMP, new HBMPEidSpectrumGenerator());
                    // below here are EID not implemented
                    generator.Add(LbmClass.SM, new SMSpectrumGenerator());
                    generator.Add(LbmClass.Cer_NS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_NDS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_NP, new CeramidePhytoSphSpectrumGenerator());
                    generator.Add(LbmClass.Cer_AS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_ADS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_AP, new CeramidePhytoSphSpectrumGenerator());
                    generator.Add(LbmClass.Cer_BS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.Cer_BDS, new CeramideSpectrumGenerator());
                    //generator.Add(LbmClass.Cer_HS, new CeramideSpectrumGenerator());
                    //generator.Add(LbmClass.Cer_HDS, new CeramideSpectrumGenerator());
                    generator.Add(LbmClass.HexCer_NS, new HexCerSpectrumGenerator());
                    generator.Add(LbmClass.DGTA, new DGTASpectrumGenerator());
                    generator.Add(LbmClass.DGTS, new DGTSSpectrumGenerator());
                    generator.Add(LbmClass.LDGTA, new LDGTASpectrumGenerator());
                    generator.Add(LbmClass.LDGTS, new LDGTSSpectrumGenerator());
                    generator.Add(LbmClass.GM3, new GM3SpectrumGenerator());
                    generator.Add(LbmClass.SHexCer, new SHexCerSpectrumGenerator());
                    generator.Add(LbmClass.CAR, new CARSpectrumGenerator());
                    @eidlipidgenerator = generator;
                }
                return @eidlipidgenerator;
            }
        }
        private static ILipidSpectrumGenerator @eidlipidgenerator;

    }
}

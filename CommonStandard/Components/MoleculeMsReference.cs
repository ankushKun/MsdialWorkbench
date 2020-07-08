﻿using CompMs.Common.DataObj.Property;
using CompMs.Common.Enum;
using CompMs.Common.Interfaces;
using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace CompMs.Common.Components {
    [MessagePackObject]
    public class MoleculeMsReference: IMoleculeMsProperty {

        public MoleculeMsReference() { }
        // set for IMMScanProperty
        [Key(0)]
        public int ScanID { get; set; }
        [Key(1)]
        public double PrecursorMz { get; set; }
        [Key(2)]
        public ChromXs ChromXs { get; set; } = new ChromXs();
        [Key(3)]
        public IonMode IonMode { get; set; }
        [Key(4)]
        public List<SpectrumPeak> Spectrum { get; set; } = new List<SpectrumPeak>();

        // set for IMoleculeProperty
        [Key(5)]
        public string Name { get; set; } = string.Empty;
        [Key(6)]
        public Formula Formula { get; set; } = new Formula();
        [Key(7)]
        public string Ontology { get; set; } = string.Empty;
        [Key(8)]
        public string SMILES { get; set; } = string.Empty;
        [Key(9)]
        public string InChIKey { get; set; } = string.Empty;

        // ion physiochemical information
        [Key(10)]
        public AdductIon AdductType { get; set; } = new AdductIon();
        [Key(11)]
        public double CollisionCrossSection { get; set; }
        [Key(12)]
        public List<IsotopicPeak> IsotopicPeaks { get; set; } = new List<IsotopicPeak>();
        [Key(13)]
        public double QuantMass { get; set; } // used for GCMS project

        // other additional metadata
        [Key(14)]
        public string CompoundClass { get; set; } // lipidomics
        [Key(15)]
        public string Comment { get; set; } = string.Empty;
        [Key(16)]
        public string InstrumentModel { get; set; } = string.Empty;
        [Key(17)]
        public string InstrumentType { get; set; } = string.Empty;
        [Key(18)]
        public string Links { get; set; } = string.Empty; // used to link molecule record to databases. Each database must be separated by semi-colon (;)
        [Key(19)]
        public float CollisionEnergy { get; set; }
        [Key(20)]
        public int BinId { get; set; } // used for binbase
        [Key(21)]
        public int Charge { get; set; }
        [Key(22)]
        public int MsLevel { get; set; }
        [Key(23)]
        public float RetentionTimeTolerance { get; set; } = 0.05F; // used for text library searching
        [Key(24)]
        public float MassTolerance { get; set; } = 0.05F; // used for text library searching
        [Key(25)]
        public float MinimumPeakHeight { get; set; } = 1000F; // used for text library searching
        [Key(26)]
        public bool IsTargetMolecule { get; set; } = true; // used for text library searching


        public void AddPeak(double mass, double intensity, string comment = null) {
            Spectrum.Add(new SpectrumPeak(mass, intensity, comment));
        }
    }
}

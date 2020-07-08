﻿using MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompMs.MsdialCore.DataObj {
    [MessagePackObject]
    public class StandardCompound {
        [Key(0)]
        public string StandardName { get; set; }
        [Key(1)]
        public double MolecularWeight { get; set; }
        [Key(2)]
        public double Concentration { get; set; } // uM
        [Key(3)]
        public string TargetClass { get; set; } // for lipids. "Any others" means that the standard is applied to all of peaks.
        [Key(4)]
        public double DilutionRate { get; set; } // default 1
        [Key(5)]
        public int PeakID { get; set; } // is used for normalization
    }
}

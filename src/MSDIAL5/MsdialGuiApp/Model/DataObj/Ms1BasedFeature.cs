﻿using CompMs.CommonMVVM;

namespace CompMs.App.Msdial.Model.DataObj
{
    internal sealed class Ms1BasedFeature : BindableBase
    {
        public MoleculeModel Molecule { get; }
        public ScanModel Scan { get; }
        public MsScanMatchResultContainerModel MatchResults { get; }
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CompMs.App.Msdial.Model.DataObj;

namespace CompMs.App.Msdial.ViewModel.DataObj
{
    internal class ProteinViewModel
    {
        public ProteinViewModel(ProteinModel model)
        {
            _model = model;
            DatabaseId = model.DatabaseId;
            Header = model.fastaProperty.Header;
            Sequence = model.fastaProperty.Sequence;
            Description = model.fastaProperty.Description;
            DB = model.fastaProperty.DB;
            UniqueIdentifier = model.fastaProperty.UniqueIdentifier;
            EntryName = model.fastaProperty.EntryName;
            OrganismName = model.fastaProperty.OrganismName;
            OrganismIdentifier = model.fastaProperty.OrganismIdentifier;
            GeneName = model.fastaProperty.GeneName;
            SequenceVersion = model.fastaProperty.SequenceVersion;
        }
        private readonly ProteinModel _model;
        public string DatabaseId { get; }
        public string Header { get; }
        public string Sequence { get; }
        public string Description { get; }
        public string DB { get; }
        public string UniqueIdentifier { get; }
        public string EntryName { get; }
        public string OrganismName { get; }
        public string OrganismIdentifier { get; }
        public string GeneName { get; }
        public string ProteinExistence { get; }
        public string SequenceVersion { get; }
    }
}

﻿using CompMs.Common.DataObj.Property;
using CompMs.Common.FormulaGenerator.Parser;
using CompMs.Common.Interfaces;
using CompMs.CommonMVVM;

namespace CompMs.App.Msdial.Model.DataObj
{
    internal sealed class MoleculeModel : BindableBase
    {
        private readonly IMoleculeProperty _molecule;

        public MoleculeModel(IMoleculeProperty molecule) {
            _molecule = molecule;
        }

        public string Name {
            get => _molecule.Name;
            set {
                if (_molecule.Name != value) {
                    _molecule.Name = value;
                    OnPropertyChanged(nameof(Name));
                }
            }
        }

        public string Formula {
            get => _molecule.Formula.FormulaString;
            set {
                if (_molecule.Formula.FormulaString != value && FormulaStringParcer.Convert2FormulaObjV2(value) is Formula formula) {
                    _molecule.Formula = formula;
                    OnPropertyChanged(nameof(Formula));
                }
            }
        }

        public string Ontology {
            get => _molecule.Ontology;
            set {
                if (_molecule.Ontology != value) {
                    _molecule.Ontology = value;
                    OnPropertyChanged(nameof(Ontology));
                }
            }
        }

        public string SMILES {
            get => _molecule.SMILES;
            set {
                if (_molecule.SMILES != value) {
                    _molecule.SMILES = value;
                    OnPropertyChanged(nameof(SMILES));
                }
            }
        }

        public string InChIKey {
            get => _molecule.InChIKey;
            set {
                if (_molecule.InChIKey != value) {
                    _molecule.InChIKey = value;
                    OnPropertyChanged(nameof(InChIKey));
                }
            }
        }
    }
}

﻿using CompMs.App.Msdial.Model.DataObj;
using CompMs.Common.Components;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.CommonMVVM;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Normalize;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CompMs.App.Msdial.Model.Statistics
{
    internal sealed class InternalStandardSetModel : DisposableModelBase
    {
        public InternalStandardSetModel(IEnumerable<AlignmentSpotPropertyModel> spots, TargetMsMethod targetMsMethod) {
            if (spots is null) {
                throw new ArgumentNullException(nameof(spots));
            }

            var spotModels = spots.Select(spot => new NormalizationSpotPropertyModel(spot.innerModel));
            Spots = new ObservableCollection<NormalizationSpotPropertyModel>(spotModels);
            TargetMsMethod = targetMsMethod ?? throw new ArgumentNullException(nameof(targetMsMethod));

            var ids = Spots.Select(s => s.Id).ToHashSet();
            var isPositive = new ConcurrentDictionary<NormalizationSpotPropertyModel, byte>();
            var someSpotSet = new Subject<bool>().AddTo(Disposables);
            Spots.ObserveElementProperty(s => s.InternalStandardId)
                .Subscribe(pack =>
                {
                    if (ids.Contains(pack.Value)) {
                        if (isPositive.TryAdd(pack.Instance, 0x0)) {
                            someSpotSet.OnNext(isPositive.Count > 0);
                        }
                    }
                    else {
                        if (isPositive.TryRemove(pack.Instance, out _)) {
                            someSpotSet.OnNext(isPositive.Count > 0);
                        }
                    }
                }).AddTo(Disposables);
            SomeSpotSetInternalStandard = someSpotSet.ToReactiveProperty(false).AddTo(Disposables);
        }

        public ObservableCollection<NormalizationSpotPropertyModel> Spots { get; }
        public TargetMsMethod TargetMsMethod { get; }

        public IObservable<bool> SomeSpotSetInternalStandard { get; }
    }

    internal sealed class NormalizationSpotPropertyModel : BindableBase, INormalizationTarget
    {
        private readonly AlignmentSpotProperty _spot;

        public NormalizationSpotPropertyModel(AlignmentSpotProperty spot) {
            _spot = spot;
            Id = _spot.MasterAlignmentID;
            Metabolite = _spot.Name;
            Adduct = _spot.AdductType.AdductIonName;
            Mz = _spot.MassCenter;
            Rt = _spot.TimesCenter.RT.Value;
            Mobility = _spot.TimesCenter.Drift.Value;
            InternalStandardId = _spot.InternalStandardAlignmentID;
        }

        public int Id { get; }
        public string Metabolite { get; }
        public string Adduct { get; }
        public double Mz { get; }
        public double Rt { get; }
        public double Mobility { get; }

        public int InternalStandardId {
            get => _spot.InternalStandardAlignmentID;
            set {
                _spot.InternalStandardAlignmentID = value;
                OnPropertyChanged(nameof(InternalStandardId));
            }
        }

        IonAbundanceUnit INormalizationTarget.IonAbundanceUnit { get => _spot.IonAbundanceUnit; set => _spot.IonAbundanceUnit = value; }

        IReadOnlyList<INormalizationTarget> INormalizationTarget.Children => ((INormalizationTarget)_spot).Children;

        IReadOnlyList<INormalizableValue> INormalizationTarget.Values => ((INormalizationTarget)_spot).Values;


        bool INormalizationTarget.IsReferenceMatched(IMatchResultEvaluator<MsScanMatchResult> evaluator) {
            return ((INormalizationTarget)_spot).IsReferenceMatched(evaluator);
        }

        MoleculeMsReference INormalizationTarget.RetriveReference(IMatchResultRefer<MoleculeMsReference, MsScanMatchResult> refer) {
            return ((INormalizationTarget)_spot).RetriveReference(refer);
        }
    }
}

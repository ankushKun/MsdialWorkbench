﻿using CompMs.Common.DataStructure;
using CompMs.Common.FormulaGenerator.DataObj;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CompMs.Common.Lipidomics
{
    public class AcylChain : IChain, IVisitableElement<AcylChain>
    {
        public AcylChain(int carbonCount, IDoubleBond doubleBond, IOxidized oxidized) {
            CarbonCount = carbonCount;
            DoubleBond = doubleBond ?? throw new ArgumentNullException(nameof(doubleBond));
            Oxidized = oxidized ?? throw new ArgumentNullException(nameof(oxidized));
        }

        public IDoubleBond DoubleBond { get; }

        public IOxidized Oxidized { get; }

        public int CarbonCount { get; }

        public int DoubleBondCount => DoubleBond.Count;

        public int OxidizedCount => Oxidized.Count;

        public double Mass => CalculateAcylMass(CarbonCount, DoubleBondCount, OxidizedCount);

        public IEnumerable<IChain> GetCandidates(IChainGenerator generator) {
            return generator.Generate(this);
        }

        public override string ToString() {
            return $"{CarbonCount}:{FormatDoubleBond(DoubleBond)}{Oxidized}";
        }

        private static string FormatDoubleBond(IDoubleBond doubleBond) {
            if (doubleBond.DecidedCount >= 1) {
                return $"{doubleBond.Count}({string.Join(",", doubleBond.Bonds)})";
            }
            else {
                return doubleBond.Count.ToString();
            }
        }

        static double CalculateAcylMass(int carbon, int doubleBond, int oxidize) {
            if (carbon == 0 && doubleBond == 0 && oxidize == 0) {
                return MassDiffDictionary.HydrogenMass;
            }
            return carbon * MassDiffDictionary.CarbonMass + (2 * carbon - 2 * doubleBond - 1) * MassDiffDictionary.HydrogenMass + (1 + oxidize) * MassDiffDictionary.OxygenMass;
        }

        public TResult Accept<TResult, TDecomposed>(IAcyclicVisitor visitor, IDecomposer<TResult, IChain, TDecomposed> decomposer) {
            return decomposer.Decompose(visitor, this);
        }

        public TResult Accept<TResult, TDecomposed>(IAcyclicVisitor visitor, IDecomposer<TResult, AcylChain, TDecomposed> decomposer) {
            return decomposer.Decompose(visitor, this);
        }
    }

    public class AlkylChain : IChain, IVisitableElement<AlkylChain>
    {
        public AlkylChain(int carbonCount, IDoubleBond doubleBond, IOxidized oxidized) {
            CarbonCount = carbonCount;
            DoubleBond = doubleBond ?? throw new ArgumentNullException(nameof(doubleBond));
            Oxidized = oxidized ?? throw new ArgumentNullException(nameof(oxidized));
        }
        public IDoubleBond DoubleBond { get; }

        public IOxidized Oxidized { get; }

        public int CarbonCount { get; }

        public int DoubleBondCount => DoubleBond.Count;

        public int OxidizedCount => Oxidized.Count;

        public double Mass => CalculateAlkylMass(CarbonCount, DoubleBondCount, OxidizedCount);

        static double CalculateAlkylMass(int carbon, int doubleBond, int oxidize) {
            return carbon * MassDiffDictionary.CarbonMass + (2 * carbon - 2 * doubleBond + 1) * MassDiffDictionary.HydrogenMass + oxidize * MassDiffDictionary.OxygenMass;
        }

        public IEnumerable<IChain> GetCandidates(IChainGenerator generator) {
            return generator.Generate(this);
        }

        public override string ToString() {
            if (IsPlasmalogen) {
                return $"P-{CarbonCount}:{FormatDoubleBondWhenPlasmalogen(DoubleBond)}{Oxidized}";
            }
            else {
                return $"O-{CarbonCount}:{FormatDoubleBond(DoubleBond)}{Oxidized}";
            }
        }

        public bool IsPlasmalogen => DoubleBond.Bonds.Any(b => b.Position == 1);

        private static string FormatDoubleBond(IDoubleBond doubleBond) {
            if (doubleBond.DecidedCount >= 1) {
                return $"{doubleBond.Count}({string.Join(",", doubleBond.Bonds)})";
            }
            else {
                return doubleBond.Count.ToString();
            }
        }

        private static string FormatDoubleBondWhenPlasmalogen(IDoubleBond doubleBond) {
            if (doubleBond.DecidedCount > 1) {
                return $"{doubleBond.Count - 1}({string.Join(",", doubleBond.Bonds.Where(b => b.Position != 1))})";
            }
            else if (doubleBond.DecidedCount == 1) {
                return $"{doubleBond.Count - 1}";
            }
            else {
                throw new ArgumentException("Plasmalogens must have more than 1 double bonds.");
            }
        }

        public TResult Accept<TResult, TDecomposed>(IAcyclicVisitor visitor, IDecomposer<TResult, IChain, TDecomposed> decomposer) {
            return decomposer.Decompose(visitor, this);
        }

        public TResult Accept<TResult, TDecomposed>(IAcyclicVisitor visitor, IDecomposer<TResult, AlkylChain, TDecomposed> decomposer) {
            return decomposer.Decompose(visitor, this);
        }
    }

    public class SphingoChain : IChain, IVisitableElement<SphingoChain>
    {
        public SphingoChain(int carbonCount, IDoubleBond doubleBond, IOxidized oxidized) {
            if (oxidized is null) {
                throw new ArgumentNullException(nameof(oxidized));
            }
            //if (!oxidized.Oxidises.Contains(1) || !oxidized.Oxidises.Contains(3))
            //{
            if (!oxidized.Oxidises.Contains(1)) {
                throw new ArgumentException(nameof(oxidized));
            }

            CarbonCount = carbonCount;
            DoubleBond = doubleBond ?? throw new ArgumentNullException(nameof(doubleBond));
            Oxidized = oxidized;
        }

        public int CarbonCount { get; }

        public IDoubleBond DoubleBond { get; }

        public IOxidized Oxidized { get; }

        public int DoubleBondCount => DoubleBond.Count;

        public int OxidizedCount => Oxidized.Count;

        public double Mass => CalculateSphingosineMass(CarbonCount, DoubleBondCount, OxidizedCount);

        static double CalculateSphingosineMass(int carbon, int doubleBond, int oxidize) {
            return carbon * MassDiffDictionary.CarbonMass + (2 * carbon - 2 * doubleBond + 2) * MassDiffDictionary.HydrogenMass + oxidize * MassDiffDictionary.OxygenMass + MassDiffDictionary.NitrogenMass;
        }

        public IEnumerable<IChain> GetCandidates(IChainGenerator generator) {
            return generator.Generate(this);
        }

        public TResult Accept<TResult, TDecomposed>(IAcyclicVisitor visitor, IDecomposer<TResult, IChain, TDecomposed> decomposer) {
            return decomposer.Decompose(visitor, this);
        }

        public TResult Accept<TResult, TDecomposed>(IAcyclicVisitor visitor, IDecomposer<TResult, SphingoChain, TDecomposed> decomposer) {
            return decomposer.Decompose(visitor, this);
        }

        public override string ToString() {
            return $"{CarbonCount}:{DoubleBond}{Oxidized}";
        }
    }
}

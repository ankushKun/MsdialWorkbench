﻿using System.Linq;
using System.Text.RegularExpressions;

namespace CompMs.Common.Lipidomics
{
    public class TotalChainParser {
        private static readonly string CarbonPattern = @"(?<carbon>\d+)";
        private static readonly string DoubleBondPattern = @"(?<db>\d+)";
        private static readonly string OxidizedPattern = @";(?<ox>O(?<oxnum>\d+)?)";

        private static readonly string TotalChainPattern = $"(?<TotalChain>(?<plasm>[de]?[OP]-)?{CarbonPattern}:{DoubleBondPattern}({OxidizedPattern})?)";
        private static readonly string ChainsPattern = $"(?<Chain>{AlkylChainParser.Pattern}|{AcylChainParser.Pattern})";

        private static readonly AlkylChainParser AlkylParser = new AlkylChainParser();
        private static readonly AcylChainParser AcylParser = new AcylChainParser();

        public TotalChainParser(int chainCount) {
            ChainCount = chainCount;
            var molecularSpeciesLevelPattern = $"(?<MolecularSpeciesLevel>({ChainsPattern}_?){{{ChainCount}}})";
            var positionLevelPattern = $"(?<PositionLevel>({ChainsPattern}/?){{{ChainCount}}})";
            if (ChainCount == 1) {
                var postionLevelExpression = new Regex(positionLevelPattern, RegexOptions.Compiled);
                Pattern = positionLevelPattern;
                Expression = postionLevelExpression;
            }
            else {
                var totalPattern = string.Join("|", new[] { positionLevelPattern, molecularSpeciesLevelPattern, TotalChainPattern });
                var totalExpression = new Regex(totalPattern, RegexOptions.Compiled);
                Pattern = totalPattern;
                Expression = totalExpression;
            }
        }

        public int ChainCount { get; }
        public string Pattern { get; }

        private readonly Regex Expression;

        public ITotalChain Parse(string lipidStr) {
            var match = Expression.Match(lipidStr);
            if (match.Success) {
                var groups = match.Groups;
                if (groups["PositionLevel"].Success) {
                    return ParsePositionLevelChains(groups);
                }
                if (groups["MolecularSpeciesLevel"].Success) {
                    return ParseMolecularSpeciesLevelChains(groups);
                }
                if (ChainCount > 1 && groups["TotalChain"].Success) {
                    return ParseTotalChains(groups, ChainCount);
                }
            }
            return null;
        }

        private PositionLevelChains ParsePositionLevelChains(GroupCollection groups) {
            return new PositionLevelChains(
                groups["Chain"].Captures.Cast<Capture>()
                    .Select(c => AlkylParser.Parse(c.Value) ?? AcylParser.Parse(c.Value))
                    .ToArray());
        }

        private MolecularSpeciesLevelChains ParseMolecularSpeciesLevelChains(GroupCollection groups) {
            return new MolecularSpeciesLevelChains(
                groups["Chain"].Captures.Cast<Capture>()
                    .Select(c => AlkylParser.Parse(c.Value) ?? AcylParser.Parse(c.Value))
                    .ToArray());
        }

        private TotalChains ParseTotalChains(GroupCollection groups, int chainCount) {
            var carbon = int.Parse(groups["carbon"].Value);
            var db = int.Parse(groups["db"].Value);
            var ox = !groups["ox"].Success ? 0 : !groups["oxnum"].Success ? 1 : int.Parse(groups["oxnum"].Value);
            
            switch (groups["plasm"].Value) {
                case "O-":
                    return new TotalChains(carbon, db, ox, chainCount, 1);
                case "dO-":
                    return new TotalChains(carbon, db, ox, chainCount, 2);
                case "eO-":
                    return new TotalChains(carbon, db, ox, chainCount, 4);
                case "P-":
                    return new TotalChains(carbon, db + 1, ox, chainCount, 1);
                case "dP-":
                    return new TotalChains(carbon, db + 2, ox, chainCount, 2);
                case "eP-":
                    return new TotalChains(carbon, db + 4, ox, chainCount, 4);
                case "":
                    return new TotalChains(carbon, db, ox, chainCount);
            }
            return null;
        }
    }
}

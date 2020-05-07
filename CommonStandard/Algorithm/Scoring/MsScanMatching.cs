﻿using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.Common.DataObj.Property;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Interfaces;
using CompMs.Common.Lipidomics;
using CompMs.Common.Parameter;
using CompMs.Common.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CompMs.Common.Algorithm.Scoring {
   
    public sealed class MsScanMatching {
        private MsScanMatching() { }

        private static bool IsComparedAvailable(List<IsotopicPeak> obj1, List<IsotopicPeak> obj2) {
            if (obj1 == null || obj2 == null || obj1.Count == 0 || obj2.Count == 0) return false;
            return true;
        }

        private static bool IsComparedAvailable(IMSScanProperty obj1, IMSScanProperty obj2) {
            if (obj1.Spectrum == null || obj2.Spectrum == null || obj1.Spectrum.Count == 0 || obj2.Spectrum.Count == 0) return false;
            return true;
        }

        public static MsScanMatchResult CompareMSScanProperties(IMSScanProperty scanProp, MoleculeMsReference refSpec,
            MsRefSearchParameterBase param,
            TargetOmics targetOmics = TargetOmics.Metablomics) {
            
            var isMs1Match = false;
            var isMs2Match = false;
            var isRtMatch = false;
            var isRiMatch = false;

            var isLipidClassMatch = false;
            var isLipidChainsMatch = false;
            var isLipidPositionMatch = false;
            var isOtherLipidMatch = false;

            var name = refSpec.Name;
            var refID = refSpec.ScanID;

            var weightedDotProduct = GetWeightedDotProduct(scanProp, refSpec, param.Ms2Tolerance, param.MassRangeBegin, param.MassRangeEnd);
            var simpleDotProduct = GetSimpleDotProduct(scanProp, refSpec, param.Ms2Tolerance, param.MassRangeBegin, param.MassRangeEnd);
            var reverseDotProduct = GetReverseDotProduct(scanProp, refSpec, param.Ms2Tolerance, param.MassRangeBegin, param.MassRangeEnd);
            var matchedPeaksScores = GetMatchedPeaksScores(scanProp, refSpec, param.Ms2Tolerance, param.MassRangeBegin, param.MassRangeEnd, targetOmics);
            
            if (weightedDotProduct >= param.WeightedDotProductCutOff &&
                simpleDotProduct  >= param.SimpleDotProductCutOff &&
                reverseDotProduct >= param.ReverseDotProductCutOff &&
                matchedPeaksScores[0] >= param.MatchedPeaksPercentageCutOff &&
                matchedPeaksScores[1] >= param.MinimumSpectrumMatch) {
                isMs2Match = true;
            }

            if (targetOmics == TargetOmics.Lipidomics) {
                name = GetRefinedLipidAnnotationLevel(scanProp, refSpec, param.Ms2Tolerance,
                    out isLipidClassMatch, out isLipidChainsMatch, out isLipidPositionMatch, out isOtherLipidMatch);
            }

            var rtSimilarity = GetGaussianSimilarity(scanProp.ChromXs.RT, refSpec.ChromXs.RT, param.RtTolerance, out isRtMatch);
            var riSimilarity = GetGaussianSimilarity(scanProp.ChromXs.RI, refSpec.ChromXs.RI, param.RiTolerance, out isRiMatch);
            var ms1Similarity = GetGaussianSimilarity(scanProp.PrecursorMz, refSpec.PrecursorMz, param.Ms1Tolerance, out isMs1Match);

            var result = new MsScanMatchResult() {
                Name = name, LibraryID = refID, InChIKey = refSpec.InChIKey, WeightedDotProduct = (float)weightedDotProduct,
                SimpleDotProduct = (float)simpleDotProduct, ReverseDotProduct = (float)reverseDotProduct,
                MatchedPeaksCount = (float)matchedPeaksScores[1], MatchedPeaksPercentage = (float)matchedPeaksScores[0],
                RtSimilarity = (float)rtSimilarity, RiSimilarity = (float)riSimilarity, AcurateMassSimilarity = (float)ms1Similarity,
                IsMs1Match = isMs1Match, IsMs2Match = isMs2Match, IsRtMatch = isRtMatch, IsRiMatch = isRiMatch,
                IsLipidChainsMatch = isLipidChainsMatch, IsLipidClassMatch = isLipidClassMatch, IsLipidPositionMatch = isLipidPositionMatch, IsOtherLipidMatch = isOtherLipidMatch
            };

            return result;
        }



        /// <summary>
        /// This method returns the similarity score between theoretical isotopic ratios and experimental isotopic patterns in MS1 axis.
        /// This method will utilize up to [M+4] for their calculations.
        /// </summary>
        /// <param name="peaks1">
        /// Add the MS1 spectrum with respect to the focused data point.
        /// </param>
        /// <param name="peaks2">
        /// Add the theoretical isotopic abundances. The theoretical patterns are supposed to be calculated in MSP parcer.
        /// </param>
        /// <param name="targetedMz">
        /// Add the experimental precursor mass.
        /// </param>
        /// <param name="tolerance">
        /// Add the torelance to merge the spectrum of experimental MS1.
        /// </param>
        /// <returns>
        /// The similarity score which is standadized from 0 (no similarity) to 1 (consistency) will be return.
        /// </returns>
        public static double GetIsotopeRatioSimilarity(List<IsotopicPeak> peaks1, List<IsotopicPeak> peaks2, float targetedMz, float tolerance) {
            if (!IsComparedAvailable(peaks1, peaks2)) return -1;

            double similarity = 0;
            double ratio1 = 0, ratio2 = 0;
            if (peaks1[0].RelativeAbundance <= 0 || peaks2[0].RelativeAbundance <= 0) return -1;

            var minimum = Math.Min(peaks1.Count, peaks2.Count);
            for (int i = 1; i < minimum; i++) {
                ratio1 = peaks1[i].RelativeAbundance / peaks1[0].RelativeAbundance;
                ratio2 = peaks2[i].RelativeAbundance / peaks2[0].RelativeAbundance;

                if (ratio1 <= 1 && ratio2 <= 1) similarity += Math.Abs(ratio1 - ratio2);
                else {
                    if (ratio1 > ratio2) {
                        similarity += 1 - ratio2 / ratio1;
                    }
                    else if (ratio2 > ratio1) {
                        similarity += 1 - ratio1 / ratio2;
                    }
                }
            }
            return 1 - similarity;
        }

        /// <summary>
        /// This method returns the presence similarity (% of matched fragments) between the experimental MS/MS spectrum and the standard MS/MS spectrum.
        /// So, this program will calculate how many fragments of library spectrum are found in the experimental spectrum and will return the %.
        /// double[] [0]m/z[1]intensity
        /// 
        /// </summary>
        /// <param name="peaks1">
        /// Add the experimental MS/MS spectrum.
        /// </param>
        /// <param name="peaks2">
        /// Add the theoretical MS/MS spectrum. The theoretical MS/MS spectrum is supposed to be retreived in MSP parcer.
        /// </param>
        /// <param name="bin">
        /// Add the bin value to merge the abundance of m/z.
        /// </param>
        /// <returns>
        /// [0] The similarity score which is standadized from 0 (no similarity) to 1 (consistency) will be returned.
        /// [1] MatchedPeaksCount is also returned.
        /// </returns>
        public static double[] GetMatchedPeaksScores(IMSScanProperty prop1, IMSScanProperty prop2, float bin,
            float massBegin, float massEnd) {
            if (!IsComparedAvailable(prop1, prop2)) return new double[2] { 0, 0 };

            var peaks1 = prop1.Spectrum;
            var peaks2 = prop2.Spectrum;

            double sumM = 0, sumL = 0;
            double minMz = peaks2[0].Mass;
            double maxMz = peaks2[peaks2.Count - 1].Mass;

            if (massBegin > minMz) minMz = massBegin;
            if (maxMz > massEnd) maxMz = massEnd;

            double focusedMz = minMz;
            double maxLibIntensity = peaks2.Max(n => n.Intensity);
            int remaindIndexM = 0, remaindIndexL = 0;
            int counter = 0;
            int libCounter = 0;

            List<double[]> measuredMassList = new List<double[]>();
            List<double[]> referenceMassList = new List<double[]>();

            while (focusedMz <= maxMz) {
                sumL = 0;
                for (int i = remaindIndexL; i < peaks2.Count; i++) {
                    if (peaks2[i].Mass < focusedMz - bin) continue;
                    else if (focusedMz - bin <= peaks2[i].Mass && peaks2[i].Mass < focusedMz + bin)
                        sumL += peaks2[i].Intensity;
                    else { remaindIndexL = i; break; }
                }

                if (sumL >= 0.01 * maxLibIntensity) {
                    libCounter++;
                }

                sumM = 0;
                for (int i = remaindIndexM; i < peaks1.Count; i++) {
                    if (peaks1[i].Mass < focusedMz - bin) continue;
                    else if (focusedMz - bin <= peaks1[i].Mass && peaks1[i].Mass < focusedMz + bin)
                        sumM += peaks1[i].Intensity;
                    else { remaindIndexM = i; break; }
                }

                if (sumM > 0 && sumL >= 0.01 * maxLibIntensity) {
                    counter++;
                }

                if (focusedMz + bin > peaks2[peaks2.Count - 1].Mass) break;
                focusedMz = peaks2[remaindIndexL].Mass;
            }

            if (libCounter == 0) return new double[2] { 0, 0 };
            else
                return new double[2] { (double)counter / (double)libCounter, libCounter };
        }

        /// <summary>
        /// This method returns the presence similarity (% of matched fragments) between the experimental MS/MS spectrum and the standard MS/MS spectrum.
        /// So, this program will calculate how many fragments of library spectrum are found in the experimental spectrum and will return the %.
        /// double[] [0]m/z[1]intensity
        /// 
        /// </summary>
        /// <param name="peaks1">
        /// Add the experimental MS/MS spectrum.
        /// </param>
        /// <param name="refSpec">
        /// Add the theoretical MS/MS spectrum. The theoretical MS/MS spectrum is supposed to be retreived in MSP parcer.
        /// </param>
        /// <param name="bin">
        /// Add the bin value to merge the abundance of m/z.
        /// </param>
        /// <returns>
        /// [0] The similarity score which is standadized from 0 (no similarity) to 1 (consistency) will be return.
        /// [1] MatchedPeaksCount is also returned.
        /// </returns>
        public static double[] GetMatchedPeaksScores(IMSScanProperty msScanProp, MoleculeMsReference molMsRef,
            float bin, float massBegin, float massEnd, TargetOmics omics) {

            if (!IsComparedAvailable(msScanProp, molMsRef)) return new double[] { 0, 0 };
            if (omics == TargetOmics.Metablomics) return GetMatchedPeaksScores(msScanProp, molMsRef, bin, massBegin, massEnd);

            // in lipidomics project, currently, the well-known lipid classes now including
            // PC, PE, PI, PS, PG, BMP, SM, TAG are now evaluated.
            // if the lipid class diagnostic fragment (like m/z 184 in PC and SM in ESI(+)) is observed, 
            // the bonus 0.5 is added to the normal presence score

            var resultArray = GetMatchedPeaksScores(msScanProp, molMsRef, bin, massBegin, massEnd); // [0] matched ratio [1] matched count
            var compClass = molMsRef.CompoundClass;
            var adductname = molMsRef.AdductType.AdductIonName;
            var comment = molMsRef.Comment;
            if (comment != "SPLASH" && compClass != "Unknown" && compClass != "Others") {
                var molecule = LipidomicsConverter.ConvertMsdialLipidnameToLipidMoleculeObjectVS2(molMsRef);
                if (molecule == null || molecule.Adduct == null) return resultArray;
                if (molecule.LipidClass == LbmClass.EtherPE && molMsRef.Spectrum.Count == 3) return resultArray;

                var result = GetLipidMoleculeAnnotationResult(msScanProp, molecule, bin);
                if (result != null) {
                    if (result.AnnotationLevel == 1) {
                        if (compClass == "SM" && molecule.LipidName.Contains("3O")) {
                            resultArray[0] += 1.0;
                            return resultArray; // add bonus
                        }
                        else {
                            resultArray[0] += 0.5;
                            return resultArray; // add bonus
                        }
                    }
                    else if (result.AnnotationLevel == 2) {
                        resultArray[0] += 1.0;
                        return resultArray; // add bonus
                    }
                    else
                        return resultArray;
                }
                else {
                    return resultArray;
                }
            }
            else { // currently default value is retured for other lipids
                return resultArray;
            }
        }

        public static string GetRefinedLipidAnnotationLevel(IMSScanProperty msScanProp, MoleculeMsReference molMsRef, float bin,
            out bool isLipidClassMatched, out bool isLipidChainMatched, out bool isLipidPositionMatched, out bool isOthers) {

            isLipidClassMatched = false;
            isLipidChainMatched = false;
            isLipidPositionMatched = false;
            isOthers = false;
            if (!IsComparedAvailable(msScanProp, molMsRef)) return string.Empty;

            // in lipidomics project, currently, the well-known lipid classes now including
            // PC, PE, PI, PS, PG, SM, TAG are now evaluated.
            // if the lipid class diagnostic fragment (like m/z 184 in PC and SM in ESI(+)) is observed, 
            // the bonus 0.5 is added to the normal presence score

            var compClass = molMsRef.CompoundClass;
            var adductname = molMsRef.AdductType.AdductIonName;
            var comment = molMsRef.Comment;
           
            if (comment != "SPLASH" && compClass != "Unknown" && compClass != "Others") {

                if (compClass == "Cholesterol" || compClass == "CholesterolSulfate" ||
                    compClass == "Undefined" || compClass == "BileAcid" ||
                    compClass == "Ac2PIM1" || compClass == "Ac2PIM2" || compClass == "Ac3PIM2" || compClass == "Ac4PIM2" ||
                    compClass == "LipidA") {
                    isOthers = true;
                    return molMsRef.Name; // currently default value is retured for these lipids
                }

                var molecule = LipidomicsConverter.ConvertMsdialLipidnameToLipidMoleculeObjectVS2(molMsRef);
                if (molecule == null || molecule.Adduct == null) {
                    isOthers = true;
                    return molMsRef.Name;
                }

                var result = GetLipidMoleculeAnnotationResult(msScanProp, molecule, bin);
                if (result != null) {
                    var refinedName = string.Empty;
                    if (result.AnnotationLevel == 1) {
                        refinedName = result.SublevelLipidName;
                        isLipidClassMatched = true;
                        isLipidChainMatched = false;
                        isLipidPositionMatched = false;
                    }
                    else if (result.AnnotationLevel == 2) {
                        isLipidClassMatched = true;
                        isLipidChainMatched = true;
                        isLipidPositionMatched = false;
                        if (result.SublevelLipidName == result.LipidName) {
                            refinedName = result.SublevelLipidName;
                        }
                        else {
                            refinedName = result.SublevelLipidName + "|" + result.LipidName;
                        }
                    }
                    else
                        return string.Empty;
                    //refinedName += "; " + molecule.Adduct.AdductIonName;

                    //if (refinedName == "HexCer-AP 70:5; [M+H]+") {
                    //    Console.WriteLine();
                    //}
                    //if (query.IonMode == IonMode.Negative && query.CompoundClass == "PG") {
                    //}
                    return refinedName;
                }
                else {
                    return string.Empty;
                }
            }
            else { // currently default value is retured for other lipids
                isOthers = true;
                return molMsRef.Name;
            }
        }

        public static LipidMolecule GetLipidMoleculeAnnotationResult(IMSScanProperty msScanProp,
            LipidMolecule molecule, double ms2tol) {

            var lipidclass = molecule.LipidClass;
            var refMz = molecule.Mz;
            var adduct = molecule.Adduct;

            var totalCarbon = molecule.TotalCarbonCount;
            var totalDbBond = molecule.TotalDoubleBondCount;
            var totalOxidized = molecule.TotalOxidizedCount;

            var sn1Carbon = molecule.Sn1CarbonCount;
            var sn1DbBond = molecule.Sn1DoubleBondCount;
            var sn1Oxidized = molecule.Sn1Oxidizedount;

            var sn2Oxidized = molecule.Sn2Oxidizedount;

            // Console.WriteLine(molecule.LipidName);
            var lipidheader = LipidomicsConverter.GetLipidHeaderString(molecule.LipidName);
            // Console.WriteLine(lipidheader + "\t" + lipidclass.ToString());

            switch (lipidclass) {
                case LbmClass.PC:
                    return LipidMsmsCharacterization.JudgeIfPhosphatidylcholine(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.PE:
                    return LipidMsmsCharacterization.JudgeIfPhosphatidylethanolamine(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.PS:
                    return LipidMsmsCharacterization.JudgeIfPhosphatidylserine(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.PG:
                    return LipidMsmsCharacterization.JudgeIfPhosphatidylglycerol(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.BMP:
                    return LipidMsmsCharacterization.JudgeIfBismonoacylglycerophosphate(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.PI:
                    return LipidMsmsCharacterization.JudgeIfPhosphatidylinositol(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.SM:
                    if (molecule.TotalChainString.Contains("3O")) {
                        return LipidMsmsCharacterization.JudgeIfSphingomyelinPhyto(msScanProp, ms2tol, refMz,
                       totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                    }
                    else {
                        return LipidMsmsCharacterization.JudgeIfSphingomyelin(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                    }
                case LbmClass.LNAPE:
                    return LipidMsmsCharacterization.JudgeIfNacylphosphatidylethanolamine(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.LNAPS:
                    return LipidMsmsCharacterization.JudgeIfNacylphosphatidylserine(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.CE:
                    return LipidMsmsCharacterization.JudgeIfCholesterylEster(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct);
                case LbmClass.CAR:
                    return LipidMsmsCharacterization.JudgeIfAcylcarnitine(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct);

                case LbmClass.DG:
                    return LipidMsmsCharacterization.JudgeIfDag(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.MG:
                    return LipidMsmsCharacterization.JudgeIfMag(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.MGDG:
                    return LipidMsmsCharacterization.JudgeIfMgdg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.DGDG:
                    return LipidMsmsCharacterization.JudgeIfDgdg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.PMeOH:
                    return LipidMsmsCharacterization.JudgeIfPmeoh(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.PEtOH:
                    return LipidMsmsCharacterization.JudgeIfPetoh(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.PBtOH:
                    return LipidMsmsCharacterization.JudgeIfPbtoh(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.LPC:
                    return LipidMsmsCharacterization.JudgeIfLysopc(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.LPE:
                    return LipidMsmsCharacterization.JudgeIfLysope(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.PA:
                    return LipidMsmsCharacterization.JudgeIfPhosphatidicacid(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.LPA:
                    return LipidMsmsCharacterization.JudgeIfLysopa(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.LPG:
                    return LipidMsmsCharacterization.JudgeIfLysopg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.LPI:
                    return LipidMsmsCharacterization.JudgeIfLysopi(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.LPS:
                    return LipidMsmsCharacterization.JudgeIfLysops(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.EtherPC:
                    return LipidMsmsCharacterization.JudgeIfEtherpc(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.EtherPE:
                    return LipidMsmsCharacterization.JudgeIfEtherpe(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.EtherLPC:
                    return LipidMsmsCharacterization.JudgeIfEtherlysopc(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.EtherLPE:
                    return LipidMsmsCharacterization.JudgeIfEtherlysope(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.OxPC:
                    return LipidMsmsCharacterization.JudgeIfOxpc(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized, sn1Oxidized, sn2Oxidized);

                case LbmClass.OxPE:
                    return LipidMsmsCharacterization.JudgeIfOxpe(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized, sn1Oxidized, sn2Oxidized);

                case LbmClass.OxPG:
                    return LipidMsmsCharacterization.JudgeIfOxpg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized, sn1Oxidized, sn2Oxidized);

                case LbmClass.OxPI:
                    return LipidMsmsCharacterization.JudgeIfOxpi(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized, sn1Oxidized, sn2Oxidized);

                case LbmClass.OxPS:
                    return LipidMsmsCharacterization.JudgeIfOxps(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized, sn1Oxidized, sn2Oxidized);

                case LbmClass.EtherMGDG:
                    return LipidMsmsCharacterization.JudgeIfEthermgdg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.EtherDGDG:
                    return LipidMsmsCharacterization.JudgeIfEtherdgdg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.DGTS:
                    return LipidMsmsCharacterization.JudgeIfDgts(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.LDGTS:
                    return LipidMsmsCharacterization.JudgeIfLdgts(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.DGCC:
                    return LipidMsmsCharacterization.JudgeIfDgcc(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.LDGCC:
                    return LipidMsmsCharacterization.JudgeIfLdgcc(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.DGGA:
                    return LipidMsmsCharacterization.JudgeIfGlcadg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.SQDG:
                    return LipidMsmsCharacterization.JudgeIfSqdg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.DLCL:
                    return LipidMsmsCharacterization.JudgeIfDilysocardiolipin(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.FA:
                    return LipidMsmsCharacterization.JudgeIfFattyacid(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.FAHFA:
                    return LipidMsmsCharacterization.JudgeIfFahfa(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.OxFA:
                    return LipidMsmsCharacterization.JudgeIfOxfattyacid(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized);

                case LbmClass.EtherOxPC:
                    return LipidMsmsCharacterization.JudgeIfEtheroxpc(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized, sn1Oxidized, sn2Oxidized);

                case LbmClass.EtherOxPE:
                    return LipidMsmsCharacterization.JudgeIfEtheroxpe(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized, sn1Oxidized, sn2Oxidized);

                case LbmClass.Cer_NS:
                    return LipidMsmsCharacterization.JudgeIfCeramidens(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_NDS:
                    return LipidMsmsCharacterization.JudgeIfCeramidends(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.HexCer_NS:
                    return LipidMsmsCharacterization.JudgeIfHexceramidens(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.HexCer_NDS:
                    return LipidMsmsCharacterization.JudgeIfHexceramidends(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Hex2Cer:
                    return LipidMsmsCharacterization.JudgeIfHexhexceramidens(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Hex3Cer:
                    return LipidMsmsCharacterization.JudgeIfHexhexhexceramidens(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_AP:
                    return LipidMsmsCharacterization.JudgeIfCeramideap(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.HexCer_AP:
                    return LipidMsmsCharacterization.JudgeIfHexceramideap(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);


                case LbmClass.SHexCer:
                    return LipidMsmsCharacterization.JudgeIfShexcer(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized);

                case LbmClass.GM3:
                    return LipidMsmsCharacterization.JudgeIfGm3(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.DHSph:
                    return LipidMsmsCharacterization.JudgeIfSphinganine(msScanProp, ms2tol, refMz,
                        molecule.TotalCarbonCount, molecule.TotalDoubleBondCount, adduct);

                case LbmClass.Sph:
                    return LipidMsmsCharacterization.JudgeIfSphingosine(msScanProp, ms2tol, refMz,
                        molecule.TotalCarbonCount, molecule.TotalDoubleBondCount, adduct);

                case LbmClass.PhytoSph:
                    return LipidMsmsCharacterization.JudgeIfPhytosphingosine(msScanProp, ms2tol, refMz,
                        molecule.TotalCarbonCount, molecule.TotalDoubleBondCount, adduct);

                case LbmClass.TG:
                    var sn2Carbon = molecule.Sn2CarbonCount;
                    var sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfTriacylglycerol(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond,
                        sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.ADGGA:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfAcylglcadg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);
                case LbmClass.HBMP:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfHemiismonoacylglycerophosphate(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.EtherTG:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfEthertag(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.MLCL:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfLysocardiolipin(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.Cer_EOS:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfCeramideeos(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.Cer_EODS:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfCeramideeods(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.HexCer_EOS:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfHexceramideeos(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.ASM:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfAcylsm(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon,
                         sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.Cer_EBDS:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfAcylcerbds(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.AHexCer:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    return LipidMsmsCharacterization.JudgeIfAcylhexceras(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, adduct);

                case LbmClass.CL:
                    sn2Carbon = molecule.Sn2CarbonCount;
                    sn2DbBond = molecule.Sn2DoubleBondCount;
                    var sn3Carbon = molecule.Sn3CarbonCount;
                    var sn3DbBond = molecule.Sn3DoubleBondCount;
                    if (sn3Carbon < 1) {
                        return LipidMsmsCharacterization.JudgeIfCardiolipin(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                    }
                    else {
                        return LipidMsmsCharacterization.JudgeIfCardiolipin(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond,
                        sn2Carbon, sn2Carbon, sn2DbBond, sn2DbBond, sn3Carbon, sn3Carbon, sn3DbBond, sn3DbBond, adduct);
                    }

                //add 10/04/19
                case LbmClass.EtherPI:
                    return LipidMsmsCharacterization.JudgeIfEtherpi(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.EtherPS:
                    return LipidMsmsCharacterization.JudgeIfEtherps(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.EtherDG:
                    return LipidMsmsCharacterization.JudgeIfEtherDAG(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.PI_Cer:
                    return LipidMsmsCharacterization.JudgeIfPicermide(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized);
                case LbmClass.PE_Cer:
                    return LipidMsmsCharacterization.JudgeIfPecermide(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized);

                //add 13/5/19
                case LbmClass.DCAE:
                    return LipidMsmsCharacterization.JudgeIfDcae(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct, totalOxidized);

                case LbmClass.GDCAE:
                    return LipidMsmsCharacterization.JudgeIfGdcae(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct, totalOxidized);

                case LbmClass.GLCAE:
                    return LipidMsmsCharacterization.JudgeIfGlcae(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct, totalOxidized);

                case LbmClass.TDCAE:
                    return LipidMsmsCharacterization.JudgeIfTdcae(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct, totalOxidized);

                case LbmClass.TLCAE:
                    return LipidMsmsCharacterization.JudgeIfTlcae(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct, totalOxidized);

                case LbmClass.NAE:
                    return LipidMsmsCharacterization.JudgeIfAnandamide(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct);

                case LbmClass.NAGly:
                    return LipidMsmsCharacterization.JudgeIfFahfamidegly(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.NAGlySer:
                    return LipidMsmsCharacterization.JudgeIfFahfamideglyser(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);


                case LbmClass.SL:
                    return LipidMsmsCharacterization.JudgeIfSulfonolipid(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct, totalOxidized);

                case LbmClass.EtherPG:
                    return LipidMsmsCharacterization.JudgeIfEtherpg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.EtherLPG:
                    return LipidMsmsCharacterization.JudgeIfEtherlysopg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.CoQ:
                    return LipidMsmsCharacterization.JudgeIfCoenzymeq(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, adduct);


                case LbmClass.Vitamin_E:
                    return LipidMsmsCharacterization.JudgeIfVitaminmolecules(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, adduct);


                case LbmClass.VAE:
                    return LipidMsmsCharacterization.JudgeIfVitaminaestermolecules(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, adduct);


                case LbmClass.NAOrn:
                    return LipidMsmsCharacterization.JudgeIfFahfamideorn(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);


                case LbmClass.BRSE:
                    return LipidMsmsCharacterization.JudgeIfBrseSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);

                case LbmClass.CASE:
                    return LipidMsmsCharacterization.JudgeIfCaseSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);

                case LbmClass.SISE:
                    return LipidMsmsCharacterization.JudgeIfSiseSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);

                case LbmClass.STSE:
                    return LipidMsmsCharacterization.JudgeIfStseSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);


                case LbmClass.AHexBRS:
                    return LipidMsmsCharacterization.JudgeIfAhexbrseSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);

                case LbmClass.AHexCAS:
                    return LipidMsmsCharacterization.JudgeIfAhexcaseSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);

                case LbmClass.AHexCS:
                    return LipidMsmsCharacterization.JudgeIfAhexceSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);

                case LbmClass.AHexSIS:
                    return LipidMsmsCharacterization.JudgeIfAhexsiseSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);

                case LbmClass.AHexSTS:
                    return LipidMsmsCharacterization.JudgeIfAhexstseSpecies(msScanProp, ms2tol, refMz,
                    totalCarbon, totalDbBond, adduct);


                // add 27/05/19
                case LbmClass.Cer_AS:
                    return LipidMsmsCharacterization.JudgeIfCeramideas(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_ADS:
                    return LipidMsmsCharacterization.JudgeIfCeramideads(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_BS:
                    return LipidMsmsCharacterization.JudgeIfCeramidebs(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_BDS:
                    return LipidMsmsCharacterization.JudgeIfCeramidebds(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_NP:
                    return LipidMsmsCharacterization.JudgeIfCeramidenp(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_OS:
                    return LipidMsmsCharacterization.JudgeIfCeramideos(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                //add 190528
                case LbmClass.Cer_HS:
                    return LipidMsmsCharacterization.JudgeIfCeramideo(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_HDS:
                    return LipidMsmsCharacterization.JudgeIfCeramideo(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.Cer_NDOS:
                    return LipidMsmsCharacterization.JudgeIfCeramidedos(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.HexCer_HS:
                    return LipidMsmsCharacterization.JudgeIfHexceramideo(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                case LbmClass.HexCer_HDS:
                    return LipidMsmsCharacterization.JudgeIfHexceramideo(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                //190801
                case LbmClass.SHex:
                    return LipidMsmsCharacterization.JudgeIfSterolHexoside(molecule.LipidName, molecule.LipidClass,
                        msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, adduct);
                case LbmClass.BAHex:
                    return LipidMsmsCharacterization.JudgeIfSterolHexoside(molecule.LipidName, molecule.LipidClass,
                        msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, adduct);
                case LbmClass.SSulfate:
                    return LipidMsmsCharacterization.JudgeIfSterolSulfate(molecule.LipidName, molecule.LipidClass,
                        msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, adduct);
                case LbmClass.BASulfate:
                    return LipidMsmsCharacterization.JudgeIfSterolSulfate(molecule.LipidName, molecule.LipidClass,
                        msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, adduct);

                // added 190811
                case LbmClass.CerP:
                    return LipidMsmsCharacterization.JudgeIfCeramidePhosphate(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);

                ///2019/11/25 add
                case LbmClass.SMGDG:
                    return LipidMsmsCharacterization.JudgeIfSmgdg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                case LbmClass.EtherSMGDG:
                    return LipidMsmsCharacterization.JudgeIfEtherSmgdg(msScanProp, ms2tol, refMz,
                         totalCarbon, totalDbBond, sn1Carbon, sn1Carbon, sn1DbBond, sn1DbBond, adduct);
                //add 20200218
                case LbmClass.LCAE:
                    return LipidMsmsCharacterization.JudgeIfLcae(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct, totalOxidized);

                case LbmClass.KLCAE:
                    return LipidMsmsCharacterization.JudgeIfKlcae(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct, totalOxidized);

                case LbmClass.KDCAE:
                    return LipidMsmsCharacterization.JudgeIfKdcae(msScanProp, ms2tol, refMz,
                        totalCarbon, totalDbBond, adduct, totalOxidized);

                default:
                    return null;
            }
        }

        /// <summary>
        /// This program will return so called reverse dot product similarity as described in the previous resport.
        /// Stein, S. E. An Integrated Method for Spectrum Extraction. J.Am.Soc.Mass.Spectrom, 10, 770-781, 1999.
        /// The spectrum similarity of MS/MS with respect to library spectrum will be calculated in this method.
        /// </summary>
        /// <param name="peaks1">
        /// Add the experimental MS/MS spectrum.
        /// </param>
        /// <param name="peaks2">
        /// Add the theoretical MS/MS spectrum. The theoretical MS/MS spectrum is supposed to be retreived in MSP parcer.
        /// </param>
        /// <param name="bin">
        /// Add the bin value to merge the abundance of m/z.
        /// </param>
        /// <returns>
        /// The similarity score which is standadized from 0 (no similarity) to 1 (consistency) will be return.
        /// </returns>
        public static double GetReverseDotProduct(IMSScanProperty prop1, IMSScanProperty prop2, float bin,
            float massBegin, float massEnd) {
            double scalarM = 0, scalarR = 0, covariance = 0;
            double sumM = 0, sumL = 0;
            if (!IsComparedAvailable(prop1, prop2)) return 0;

            var peaks1 = prop1.Spectrum;
            var peaks2 = prop2.Spectrum;

            double minMz = peaks2[0].Mass;
            double maxMz = peaks2[peaks2.Count - 1].Mass;

            if (massBegin > minMz) minMz = massBegin;
            if (maxMz > massEnd) maxMz = massEnd;

            double focusedMz = minMz;
            int remaindIndexM = 0, remaindIndexL = 0;
            int counter = 0;

            List<double[]> measuredMassList = new List<double[]>();
            List<double[]> referenceMassList = new List<double[]>();

            double sumMeasure = 0, sumReference = 0, baseM = double.MinValue, baseR = double.MinValue;

            while (focusedMz <= maxMz) {
                sumL = 0;
                for (int i = remaindIndexL; i < peaks2.Count; i++) {
                    if (peaks2[i].Mass < focusedMz - bin) continue;
                    else if (focusedMz - bin <= peaks2[i].Mass && peaks2[i].Mass < focusedMz + bin)
                        sumL += peaks2[i].Intensity;
                    else { remaindIndexL = i; break; }
                }

                sumM = 0;
                for (int i = remaindIndexM; i < peaks1.Count; i++) {
                    if (peaks1[i].Mass < focusedMz - bin) continue;
                    else if (focusedMz - bin <= peaks1[i].Mass && peaks1[i].Mass < focusedMz + bin)
                        sumM += peaks1[i].Intensity;
                    else { remaindIndexM = i; break; }
                }

                if (sumM <= 0) {
                    measuredMassList.Add(new double[] { focusedMz, sumM });
                    if (sumM > baseM) baseM = sumM;

                    referenceMassList.Add(new double[] { focusedMz, sumL });
                    if (sumL > baseR) baseR = sumL;
                }
                else {
                    measuredMassList.Add(new double[] { focusedMz, sumM });
                    if (sumM > baseM) baseM = sumM;

                    referenceMassList.Add(new double[] { focusedMz, sumL });
                    if (sumL > baseR) baseR = sumL;

                    counter++;
                }

                if (focusedMz + bin > peaks2[peaks2.Count - 1].Mass) break;
                focusedMz = peaks2[remaindIndexL].Mass;
            }

            if (baseM == 0 || baseR == 0) return 0;

            var eSpectrumCounter = 0;
            var lSpectrumCounter = 0;
            for (int i = 0; i < measuredMassList.Count; i++) {
                measuredMassList[i][1] = measuredMassList[i][1] / baseM;
                referenceMassList[i][1] = referenceMassList[i][1] / baseR;
                sumMeasure += measuredMassList[i][1];
                sumReference += referenceMassList[i][1];

                if (measuredMassList[i][1] > 0.1) eSpectrumCounter++;
                if (referenceMassList[i][1] > 0.1) lSpectrumCounter++;
            }

            var peakCountPenalty = 1.0;
            if (lSpectrumCounter == 1) peakCountPenalty = 0.75;
            else if (lSpectrumCounter == 2) peakCountPenalty = 0.88;
            else if (lSpectrumCounter == 3) peakCountPenalty = 0.94;
            else if (lSpectrumCounter == 4) peakCountPenalty = 0.97;

            double wM, wR;

            if (sumMeasure - 0.5 == 0) wM = 0;
            else wM = 1 / (sumMeasure - 0.5);

            if (sumReference - 0.5 == 0) wR = 0;
            else wR = 1 / (sumReference - 0.5);

            var cutoff = 0.01;

            for (int i = 0; i < measuredMassList.Count; i++) {
                if (referenceMassList[i][1] < cutoff)
                    continue;

                scalarM += measuredMassList[i][1] * measuredMassList[i][0];
                scalarR += referenceMassList[i][1] * referenceMassList[i][0];
                covariance += Math.Sqrt(measuredMassList[i][1] * referenceMassList[i][1]) * measuredMassList[i][0];

                //scalarM += measuredMassList[i][1];
                //scalarR += referenceMassList[i][1];
                //covariance += Math.Sqrt(measuredMassList[i][1] * referenceMassList[i][1]);
            }

            if (scalarM == 0 || scalarR == 0) { return 0; }
            else { return Math.Pow(covariance, 2) / scalarM / scalarR * peakCountPenalty; }
        }

        /// <summary>
        /// This program will return so called dot product similarity as described in the previous resport.
        /// Stein, S. E. An Integrated Method for Spectrum Extraction. J.Am.Soc.Mass.Spectrom, 10, 770-781, 1999.
        /// The spectrum similarity of MS/MS will be calculated in this method.
        /// </summary>
        /// <param name="peaks1">
        /// Add the experimental MS/MS spectrum.
        /// </param>
        /// <param name="peaks2">
        /// Add the theoretical MS/MS spectrum. The theoretical MS/MS spectrum is supposed to be retreived in MSP parcer.
        /// </param>
        /// <param name="bin">
        /// Add the bin value to merge the abundance of m/z.
        /// </param>
        /// <returns>
        /// The similarity score which is standadized from 0 (no similarity) to 1 (consistency) will be return.
        /// </returns>
        public static double GetWeightedDotProduct(IMSScanProperty prop1, IMSScanProperty prop2, float bin,
            float massBegin, float massEnd) {
            double scalarM = 0, scalarR = 0, covariance = 0;
            double sumM = 0, sumR = 0;

            if (!IsComparedAvailable(prop1, prop2)) return 0;

            var peaks1 = prop1.Spectrum;
            var peaks2 = prop2.Spectrum;

            double minMz = Math.Min(peaks1[0].Mass, peaks2[0].Mass);
            double maxMz = Math.Max(peaks1[peaks1.Count - 1].Mass, peaks2[peaks2.Count - 1].Mass);

            if (massBegin > minMz) minMz = massBegin;
            if (maxMz > massEnd) maxMz = massEnd;

            double focusedMz = minMz;
            int remaindIndexM = 0, remaindIndexL = 0;

            List<double[]> measuredMassList = new List<double[]>();
            List<double[]> referenceMassList = new List<double[]>();

            double sumMeasure = 0, sumReference = 0, baseM = double.MinValue, baseR = double.MinValue;

            while (focusedMz <= maxMz) {
                sumM = 0;
                for (int i = remaindIndexM; i < peaks1.Count; i++) {
                    if (peaks1[i].Mass < focusedMz - bin) { continue; }
                    else if (focusedMz - bin <= peaks1[i].Mass && peaks1[i].Mass < focusedMz + bin) sumM += peaks1[i].Intensity;
                    else { remaindIndexM = i; break; }
                }

                sumR = 0;
                for (int i = remaindIndexL; i < peaks2.Count; i++) {
                    if (peaks2[i].Mass < focusedMz - bin) continue;
                    else if (focusedMz - bin <= peaks2[i].Mass && peaks2[i].Mass < focusedMz + bin)
                        sumR += peaks2[i].Intensity;
                    else { remaindIndexL = i; break; }
                }

                if (sumM <= 0 && sumR > 0) {
                    measuredMassList.Add(new double[] { focusedMz, sumM });
                    if (sumM > baseM) baseM = sumM;

                    referenceMassList.Add(new double[] { focusedMz, sumR });
                    if (sumR > baseR) baseR = sumR;
                }
                else {
                    measuredMassList.Add(new double[] { focusedMz, sumM });
                    if (sumM > baseM) baseM = sumM;

                    referenceMassList.Add(new double[] { focusedMz, sumR });
                    if (sumR > baseR) baseR = sumR;
                }

                if (focusedMz + bin > Math.Max(peaks1[peaks1.Count - 1].Mass, peaks2[peaks2.Count - 1].Mass)) break;
                if (focusedMz + bin > peaks2[remaindIndexL].Mass && focusedMz + bin <= peaks1[remaindIndexM].Mass)
                    focusedMz = peaks1[remaindIndexM].Mass;
                else if (focusedMz + bin <= peaks2[remaindIndexL].Mass && focusedMz + bin > peaks1[remaindIndexM].Mass)
                    focusedMz = peaks2[remaindIndexL].Mass;
                else
                    focusedMz = Math.Min(peaks1[remaindIndexM].Mass, peaks2[remaindIndexL].Mass);
            }

            if (baseM == 0 || baseR == 0) return 0;


            var eSpectrumCounter = 0;
            var lSpectrumCounter = 0;
            for (int i = 0; i < measuredMassList.Count; i++) {
                measuredMassList[i][1] = measuredMassList[i][1] / baseM;
                referenceMassList[i][1] = referenceMassList[i][1] / baseR;
                sumMeasure += measuredMassList[i][1];
                sumReference += referenceMassList[i][1];

                if (measuredMassList[i][1] > 0.1) eSpectrumCounter++;
                if (referenceMassList[i][1] > 0.1) lSpectrumCounter++;
            }

            var peakCountPenalty = 1.0;
            if (lSpectrumCounter == 1) peakCountPenalty = 0.75;
            else if (lSpectrumCounter == 2) peakCountPenalty = 0.88;
            else if (lSpectrumCounter == 3) peakCountPenalty = 0.94;
            else if (lSpectrumCounter == 4) peakCountPenalty = 0.97;

            double wM, wR;

            if (sumMeasure - 0.5 == 0) wM = 0;
            else wM = 1 / (sumMeasure - 0.5);

            if (sumReference - 0.5 == 0) wR = 0;
            else wR = 1 / (sumReference - 0.5);

            var cutoff = 0.01;
            for (int i = 0; i < measuredMassList.Count; i++) {
                if (measuredMassList[i][1] < cutoff)
                    continue;

                scalarM += measuredMassList[i][1] * measuredMassList[i][0];
                scalarR += referenceMassList[i][1] * referenceMassList[i][0];
                covariance += Math.Sqrt(measuredMassList[i][1] * referenceMassList[i][1]) * measuredMassList[i][0];

                //scalarM += measuredMassList[i][1];
                //scalarR += referenceMassList[i][1];
                //covariance += Math.Sqrt(measuredMassList[i][1] * referenceMassList[i][1]);
            }

            if (scalarM == 0 || scalarR == 0) { return 0; }
            else { return Math.Pow(covariance, 2) / scalarM / scalarR * peakCountPenalty; }
        }

        public static double GetSimpleDotProduct(IMSScanProperty prop1, IMSScanProperty prop2, float bin, float massBegin, float massEnd) {
            double scalarM = 0, scalarR = 0, covariance = 0;
            double sumM = 0, sumR = 0;

            if (!IsComparedAvailable(prop1, prop2)) return 0;

            var peaks1 = prop1.Spectrum;
            var peaks2 = prop2.Spectrum;

            double minMz = Math.Min(peaks1[0].Mass, peaks2[0].Mass);
            double maxMz = Math.Max(peaks1[peaks1.Count - 1].Mass, peaks2[peaks2.Count - 1].Mass);
            double focusedMz = minMz;
            int remaindIndexM = 0, remaindIndexL = 0;

            if (massBegin > minMz) minMz = massBegin;
            if (maxMz > massEnd) maxMz = massEnd;


            List<double[]> measuredMassList = new List<double[]>();
            List<double[]> referenceMassList = new List<double[]>();

            double sumMeasure = 0, sumReference = 0, baseM = double.MinValue, baseR = double.MinValue;

            while (focusedMz <= maxMz) {
                sumM = 0;
                for (int i = remaindIndexM; i < peaks1.Count; i++) {
                    if (peaks1[i].Mass < focusedMz - bin) { continue; }
                    else if (focusedMz - bin <= peaks1[i].Mass && peaks1[i].Mass < focusedMz + bin) sumM += peaks1[i].Intensity;
                    else { remaindIndexM = i; break; }
                }

                sumR = 0;
                for (int i = remaindIndexL; i < peaks2.Count; i++) {
                    if (peaks2[i].Mass < focusedMz - bin) continue;
                    else if (focusedMz - bin <= peaks2[i].Mass && peaks2[i].Mass < focusedMz + bin)
                        sumR += peaks2[i].Intensity;
                    else { remaindIndexL = i; break; }
                }

                measuredMassList.Add(new double[] { focusedMz, sumM });
                if (sumM > baseM) baseM = sumM;

                referenceMassList.Add(new double[] { focusedMz, sumR });
                if (sumR > baseR) baseR = sumR;

                if (focusedMz + bin > Math.Max(peaks1[peaks1.Count - 1].Mass, peaks2[peaks2.Count - 1].Mass)) break;
                if (focusedMz + bin > peaks2[remaindIndexL].Mass && focusedMz + bin <= peaks1[remaindIndexM].Mass)
                    focusedMz = peaks1[remaindIndexM].Mass;
                else if (focusedMz + bin <= peaks2[remaindIndexL].Mass && focusedMz + bin > peaks1[remaindIndexM].Mass)
                    focusedMz = peaks2[remaindIndexL].Mass;
                else
                    focusedMz = Math.Min(peaks1[remaindIndexM].Mass, peaks2[remaindIndexL].Mass);
            }

            if (baseM == 0 || baseR == 0) return 0;

            for (int i = 0; i < measuredMassList.Count; i++) {
                measuredMassList[i][1] = measuredMassList[i][1] / baseM * 999;
                referenceMassList[i][1] = referenceMassList[i][1] / baseR * 999;
            }

            for (int i = 0; i < measuredMassList.Count; i++) {
                scalarM += measuredMassList[i][1];
                scalarR += referenceMassList[i][1];
                covariance += Math.Sqrt(measuredMassList[i][1] * referenceMassList[i][1]);
            }

            if (scalarM == 0 || scalarR == 0) { return 0; }
            else {
                return Math.Pow(covariance, 2) / scalarM / scalarR;
            }
        }

        public static double GetGaussianSimilarity(ChromX actual, ChromX reference, float tolerance, out bool isInTolerance) {
            isInTolerance = false;
            if (actual == null || reference == null) return -1;
            if (actual.Value <= 0 || reference.Value <= 0) return -1;
            if (Math.Abs(actual.Value - reference.Value) <= tolerance) isInTolerance = true;
            var similarity = GetGaussianSimilarity(actual.Value, reference.Value, tolerance);
            return similarity;
        }

        public static double GetGaussianSimilarity(double actual, double reference, float tolerance, out bool isInTolerance) {
            isInTolerance = false;
            if (actual <= 0 || reference <= 0) return -1;
            if (Math.Abs(actual - reference) <= tolerance) isInTolerance = true;
            var similarity = GetGaussianSimilarity(actual, reference, tolerance);
            return similarity;
        }

        /// <summary>
        /// This method is to calculate the similarity of retention time differences or precursor ion difference from the library information as described in the previous report.
        /// Tsugawa, H. et al. Anal.Chem. 85, 5191-5199, 2013.
        /// </summary>
        /// <param name="actual">
        /// Add the experimental m/z or retention time.
        /// </param>
        /// <param name="reference">
        /// Add the theoretical m/z or library's retention time.
        /// </param>
        /// <param name="tolrance">
        /// Add the user-defined search tolerance.
        /// </param>
        /// <returns>
        /// The similarity score which is standadized from 0 (no similarity) to 1 (consistency) will be return.
        /// </returns>
        public static double GetGaussianSimilarity(double actual, double reference, float tolrance) {
            return Math.Exp(-0.5 * Math.Pow((actual - reference) / tolrance, 2));
        }

        /// <summary>
        /// MS-DIAL program utilizes the total similarity score to rank the compound candidates.
        /// This method is to calculate it from four scores including RT, isotopic ratios, m/z, and MS/MS similarities.
        /// </summary>
        /// <param name="accurateMassSimilarity"></param>
        /// <param name="rtSimilarity"></param>
        /// <param name="isotopeSimilarity"></param>
        /// <param name="spectraSimilarity"></param>
        /// <param name="reverseSearchSimilarity"></param>
        /// <param name="presenceSimilarity"></param>
        /// <returns>
        /// The similarity score which is standadized from 0 (no similarity) to 1 (consistency) will be return.
        /// </returns>
        public static double GetTotalSimilarity(double accurateMassSimilarity, double rtSimilarity, double isotopeSimilarity,
            double spectraSimilarity, double reverseSearchSimilarity, double presenceSimilarity, bool spectrumPenalty, TargetOmics targetOmics, bool isUseRT) {
            var dotProductFactor = 3.0;
            var revesrseDotProdFactor = 2.0;
            var presensePercentageFactor = 1.0;

            var msmsFactor = 2.0;
            var rtFactor = 1.0;
            var massFactor = 1.0;
            var isotopeFactor = 0.0;

            if (targetOmics == TargetOmics.Lipidomics) {
                dotProductFactor = 1.0; revesrseDotProdFactor = 2.0; presensePercentageFactor = 3.0; msmsFactor = 1.5; rtFactor = 0.5;
            }

            var msmsSimilarity =
                (dotProductFactor * spectraSimilarity + revesrseDotProdFactor * reverseSearchSimilarity + presensePercentageFactor * presenceSimilarity) /
                (dotProductFactor + revesrseDotProdFactor + presensePercentageFactor);

            if (spectrumPenalty == true && targetOmics == TargetOmics.Metablomics) msmsSimilarity = msmsSimilarity * 0.5;

            if (!isUseRT) {
                if (isotopeSimilarity < 0) {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity)
                        / (msmsFactor + massFactor);
                }
                else {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity)
                        / (msmsFactor + massFactor + isotopeFactor);
                }
            }
            else {
                if (rtSimilarity < 0 && isotopeSimilarity < 0) {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity)
                        / (msmsFactor + massFactor + rtFactor);
                }
                else if (rtSimilarity < 0 && isotopeSimilarity >= 0) {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity)
                        / (msmsFactor + massFactor + isotopeFactor + rtFactor);
                }
                else if (isotopeSimilarity < 0 && rtSimilarity >= 0) {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity)
                        / (msmsFactor + massFactor + rtFactor);
                }
                else {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + isotopeFactor * isotopeSimilarity)
                        / (msmsFactor + massFactor + rtFactor + isotopeFactor);
                }
            }
        }

        public static double GetTotalSimilarity(double accurateMassSimilarity, double rtSimilarity, double ccsSimilarity, double isotopeSimilarity,
            double spectraSimilarity, double reverseSearchSimilarity, double presenceSimilarity, bool spectrumPenalty, TargetOmics targetOmics, bool isUseRT, bool isUseCcs) {
            var dotProductFactor = 3.0;
            var revesrseDotProdFactor = 2.0;
            var presensePercentageFactor = 1.0;

            var msmsFactor = 2.0;
            var rtFactor = 1.0;
            var massFactor = 1.0;
            var isotopeFactor = 0.0;
            var ccsFactor = 2.0;

            if (targetOmics == TargetOmics.Lipidomics) {
                dotProductFactor = 1.0; revesrseDotProdFactor = 2.0; presensePercentageFactor = 3.0; msmsFactor = 1.5; rtFactor = 0.5; ccsFactor = 1.0F;
            }

            var msmsSimilarity =
                (dotProductFactor * spectraSimilarity + revesrseDotProdFactor * reverseSearchSimilarity + presensePercentageFactor * presenceSimilarity) /
                (dotProductFactor + revesrseDotProdFactor + presensePercentageFactor);

            if (spectrumPenalty == true && targetOmics == TargetOmics.Metablomics) msmsSimilarity = msmsSimilarity * 0.5;

            var useRtScore = true;
            var useCcsScore = true;
            var useIsotopicScore = true;
            if (!isUseRT || rtSimilarity < 0) useRtScore = false;
            if (!isUseCcs || ccsSimilarity < 0) useCcsScore = false;
            if (isotopeSimilarity < 0) useIsotopicScore = false;

            if (useRtScore == true && useCcsScore == true && useIsotopicScore == true) {
                return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + isotopeFactor * isotopeSimilarity + ccsFactor * ccsSimilarity)
                        / (msmsFactor + massFactor + rtFactor + isotopeFactor + ccsFactor);
            }
            else if (useRtScore == true && useCcsScore == true && useIsotopicScore == false) {
                return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + ccsFactor * ccsSimilarity)
                        / (msmsFactor + massFactor + rtFactor + ccsFactor);
            }
            else if (useRtScore == true && useCcsScore == false && useIsotopicScore == true) {
                return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + isotopeFactor * isotopeSimilarity)
                        / (msmsFactor + massFactor + rtFactor + isotopeFactor);
            }
            else if (useRtScore == false && useCcsScore == true && useIsotopicScore == true) {
                return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity + ccsFactor * ccsSimilarity)
                        / (msmsFactor + massFactor + isotopeFactor + ccsFactor);
            }
            else if (useRtScore == false && useCcsScore == true && useIsotopicScore == false) {
                return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + ccsFactor * ccsSimilarity)
                      / (msmsFactor + massFactor + ccsFactor);
            }
            else if (useRtScore == true && useCcsScore == false && useIsotopicScore == false) {
                return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity)
                        / (msmsFactor + massFactor + rtFactor);
            }
            else if (useRtScore == false && useCcsScore == false && useIsotopicScore == true) {
                return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity)
                        / (msmsFactor + massFactor + isotopeFactor);
            }
            else {
                return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity)
                        / (msmsFactor + massFactor);
            }
            //if (!isUseRT) {
            //    if (isotopeSimilarity < 0 && ccsSimilarity < 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity)
            //            / (msmsFactor + massFactor);
            //    }
            //    else if (isotopeSimilarity < 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + ccsFactor * ccsSimilarity)
            //            / (msmsFactor + massFactor + ccsFactor);
            //    }
            //    else {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity + ccsFactor * ccsSimilarity)
            //            / (msmsFactor + massFactor + isotopeFactor + ccsFactor);
            //    }
            //}
            //else {
            //    if (rtSimilarity < 0 && isotopeSimilarity < 0 && ccsSimilarity < 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity)
            //            / (msmsFactor + massFactor);
            //    }
            //    else if (rtSimilarity < 0 && isotopeSimilarity >= 0 && ccsSimilarity < 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity)
            //            / (msmsFactor + massFactor + isotopeFactor);
            //    }
            //    else if (isotopeSimilarity < 0 && rtSimilarity >= 0 && ccsSimilarity < 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity)
            //            / (msmsFactor + massFactor + rtFactor);
            //    }
            //    else if (isotopeSimilarity < 0 && rtSimilarity < 0 && ccsSimilarity >= 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + ccsFactor * ccsSimilarity)
            //            / (msmsFactor + massFactor + ccsFactor);
            //    }
            //    else if (rtSimilarity >= 0 && isotopeSimilarity >= 0 && ccsSimilarity < 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity + rtFactor * rtSimilarity)
            //            / (msmsFactor + massFactor + isotopeFactor + rtFactor);
            //    }
            //    else if (isotopeSimilarity < 0 && rtSimilarity >= 0 && ccsSimilarity >= 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + ccsFactor * ccsSimilarity)
            //            / (msmsFactor + massFactor + rtFactor + ccsFactor);
            //    }
            //    else if (isotopeSimilarity >= 0 && rtSimilarity < 0 && ccsSimilarity >= 0) {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + ccsFactor * ccsSimilarity + isotopeFactor * isotopeSimilarity)
            //            / (msmsFactor + massFactor + ccsFactor + isotopeFactor);
            //    }
            //    else {
            //        return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + isotopeFactor * isotopeSimilarity + ccsFactor * ccsSimilarity)
            //            / (msmsFactor + massFactor + rtFactor + isotopeFactor + ccsFactor);
            //    }
            //}
        }

        /// <summary>
        /// MS-DIAL program also calculate the total similarity score without the MS/MS similarity scoring.
        /// It means that the total score will be calculated from RT, m/z, and isotopic similarities.
        /// </summary>
        /// <param name="accurateMassSimilarity"></param>
        /// <param name="rtSimilarity"></param>
        /// <param name="isotopeSimilarity"></param>
        /// <returns>
        /// The similarity score which is standadized from 0 (no similarity) to 1 (consistency) will be return.
        /// </returns>
        public static double GetTotalSimilarity(double accurateMassSimilarity, double rtSimilarity, double isotopeSimilarity, bool isUseRT) {
            if (!isUseRT) {
                if (isotopeSimilarity < 0) {
                    return accurateMassSimilarity;
                }
                else {
                    return (accurateMassSimilarity + 0.5 * isotopeSimilarity) / 1.5;
                }
            }
            else {
                if (rtSimilarity < 0 && isotopeSimilarity < 0) {
                    return accurateMassSimilarity * 0.5;
                }
                else if (rtSimilarity < 0 && isotopeSimilarity >= 0) {
                    return (accurateMassSimilarity + 0.5 * isotopeSimilarity) / 2.5;
                }
                else if (isotopeSimilarity < 0 && rtSimilarity >= 0) {
                    return (accurateMassSimilarity + rtSimilarity) * 0.5;
                }
                else {
                    return (accurateMassSimilarity + rtSimilarity + 0.5 * isotopeSimilarity) * 0.4;
                }
            }
        }

        public static double GetTotalSimilarity(double accurateMassSimilarity, double rtSimilarity, double ccsSimilarity, double isotopeSimilarity, bool isUseRT, bool isUseCcs) {

            var rtFactor = 1.0;
            var massFactor = 1.0;
            var isotopeFactor = 0.0;
            var ccsFactor = 2.0;

            var useRtScore = true;
            var useCcsScore = true;
            var useIsotopicScore = true;
            if (!isUseRT || rtSimilarity < 0) useRtScore = false;
            if (!isUseCcs || ccsSimilarity < 0) useCcsScore = false;
            if (isotopeSimilarity < 0) useIsotopicScore = false;

            if (useRtScore == true && useCcsScore == true && useIsotopicScore == true) {
                return (massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + isotopeFactor * isotopeSimilarity + ccsFactor * ccsSimilarity)
                        / (massFactor + rtFactor + isotopeFactor + ccsFactor);
            }
            else if (useRtScore == true && useCcsScore == true && useIsotopicScore == false) {
                return (massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + ccsFactor * ccsSimilarity)
                        / (massFactor + rtFactor + ccsFactor);
            }
            else if (useRtScore == true && useCcsScore == false && useIsotopicScore == true) {
                return (massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + isotopeFactor * isotopeSimilarity)
                        / (massFactor + rtFactor + isotopeFactor);
            }
            else if (useRtScore == false && useCcsScore == true && useIsotopicScore == true) {
                return (massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity + ccsFactor * ccsSimilarity)
                        / (massFactor + isotopeFactor + ccsFactor);
            }
            else if (useRtScore == false && useCcsScore == true && useIsotopicScore == false) {
                return (massFactor * accurateMassSimilarity + ccsFactor * ccsSimilarity)
                      / (massFactor + ccsFactor);
            }
            else if (useRtScore == true && useCcsScore == false && useIsotopicScore == false) {
                return (massFactor * accurateMassSimilarity + rtFactor * rtSimilarity)
                        / (massFactor + rtFactor);
            }
            else if (useRtScore == false && useCcsScore == false && useIsotopicScore == true) {
                return (massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity)
                        / (massFactor + isotopeFactor);
            }
            else {
                return (massFactor * accurateMassSimilarity)
                        / massFactor;
            }
        }

        public static double GetTotalSimilarityUsingSimpleDotProduct(double accurateMassSimilarity, double rtSimilarity, double isotopeSimilarity,
            double dotProductSimilarity, bool spectrumPenalty, TargetOmics targetOmics, bool isUseRT) {
            var msmsFactor = 2.0;
            var rtFactor = 1.0;
            var massFactor = 1.0;
            var isotopeFactor = 0.0;

            var msmsSimilarity = dotProductSimilarity;

            if (spectrumPenalty == true && targetOmics == TargetOmics.Metablomics) msmsSimilarity = msmsSimilarity * 0.5;

            if (!isUseRT) {
                if (isotopeSimilarity < 0) {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity)
                        / (msmsFactor + massFactor);
                }
                else {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity)
                        / (msmsFactor + massFactor + isotopeFactor);
                }
            }
            else {
                if (rtSimilarity < 0 && isotopeSimilarity < 0) {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity)
                        / (msmsFactor + massFactor + rtFactor);
                }
                else if (rtSimilarity < 0 && isotopeSimilarity >= 0) {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + isotopeFactor * isotopeSimilarity)
                        / (msmsFactor + massFactor + isotopeFactor + rtFactor);
                }
                else if (isotopeSimilarity < 0 && rtSimilarity >= 0) {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity)
                        / (msmsFactor + massFactor + rtFactor);
                }
                else {
                    return (msmsFactor * msmsSimilarity + massFactor * accurateMassSimilarity + rtFactor * rtSimilarity + isotopeFactor * isotopeSimilarity)
                        / (msmsFactor + massFactor + rtFactor + isotopeFactor);
                }
            }
        }
    }
}

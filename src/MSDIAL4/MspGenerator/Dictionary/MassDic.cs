﻿using System;
using System.Collections.Generic;
using System.Text;

namespace CompMs.MspGenerator
{
    public sealed class MassDictionary
    {
        private MassDictionary() { }

        //public static double C13_C12 = 1.003354838;
        //public static double H2_H1 = 1.006276746;
        //public static double N15_N14 = 0.997034893;
        //public static double O17_O16 = 1.00421708;
        //public static double Si29_Si28 = 0.999568168;
        //public static double S33_S32 = 0.99938776;

        //public static double C13_12_Plus_H2_H1 = 2.009631584;
        //public static double C13_12_Plus_N15_N14 = 2.000389731;
        //public static double C13_12_Plus_O17_O16 = 2.007571918;
        //public static double C13_12_Plus_S33_S32 = 2.002742598;
        //public static double C13_12_Plus_Si29_Si28 = 2.002923006;
        //public static double H2_H1_Plus_N15_N14 = 2.003311639;
        //public static double H2_H1_Plus_O17_O16 = 2.010493826;
        //public static double H2_H1_Plus_S33_S32 = 2.005664506;
        //public static double H2_H1_Plus_Si29_Si28 = 2.005844914;
        //public static double N15_N14_Plus_O17_O16 = 2.001251973;
        //public static double N15_N14_Plus_S33_S32 = 1.996422653;
        //public static double N15_N14_Plus_Si29_Si28 = 1.996603061;
        //public static double O17_O16_Plus_S33_S32 = 2.00360484;
        //public static double O17_O16_Plus_Si29_Si28 = 2.003785248;
        //public static double S33_S32_Plus_Si29_Si28 = 1.998955928;
        //public static double C13_C12_Plus_C13_C12 = 2.006709676;
        //public static double H2_H1_Plus_H2_H1 = 2.012553492;
        //public static double N15_N14_Plus_N15_N14 = 1.994069786;
        //public static double O17_O16_Plus_O17_O16 = 2.00843416;
        //public static double S33_S32_Plus_S33_S32 = 1.99877552;
        //public static double Si29_Si28_Plus_Si29_Si28 = 1.999136336;

        //public static double S34_S32 = 1.9957959;
        //public static double Si30_Si28 = 1.996843638;
        //public static double O18_O16 = 2.00424638;
        //public static double Cl37_Cl35 = 1.99704991;
        //public static double Br81_Br79 = 1.9979535;

        public static double CarbonMass = 12.00000000000;
        public static double HydrogenMass = 1.00782503207;
        public static double NitrogenMass = 14.00307400480;
        public static double OxygenMass = 15.99491461956;
        public static double SulfurMass = 31.97207100000;
        public static double PhosphorusMass = 30.97376163000;
        public static double FluorideMass = 18.99840322000;
        public static double SiliconMass = 27.97692653250;
        public static double ChlorideMass = 34.96885268000;
        public static double BromineMass = 78.91833710000;
        public static double IodineMass = 126.90447300000;
        public static double H2OMass = 18.010564684;
        public static double Proton = 1.00727641974;
        public static double Electron = 0.00054858026;
        public static double NH4Adduct = 18.033825553;
        public static double NaAdduct = 22.9892207;
        public static double KAdduct = 38.96315791f;
        public static double LiAdduct = 7.01545486f;
        public static double HCOOadduct = 44.998202852;
        public static double CH3COOadduct = 59.013852917;
        public static double C6H10O5 = CarbonMass * 6 + HydrogenMass * 10 + OxygenMass * 5;
    }
}

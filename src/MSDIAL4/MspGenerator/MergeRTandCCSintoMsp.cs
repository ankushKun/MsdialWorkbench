﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CompMs.Common.MessagePack;
using CompMs.Common.Parser;
using CompMs.Common.Components;
using CompMs.Common.FormulaGenerator.Parser;
using System.Linq;
using CompMs.Common.Extension;
using System.Collections.Specialized;
using Rfx.Riken.OsakaUniv;

namespace CompMs.MspGenerator
{
    public class MergeRTandCCSintoMsp
    {
        public static void generateDicOfPredict(string predictedFilesDirectry, string dbFileName)
        {
            var predictedList = new List<string>();
            var headerLine = "";

            //if (File.Exists(dbFileName))
            //{
            //    File.Delete(dbFileName);
            //}
            var predictedFileList = new List<string>(Directory.GetFiles(predictedFilesDirectry));

            foreach (var predictedFile in predictedFileList)
            {
                using (var sr = new StreamReader(predictedFile, false))
                {
                    headerLine = sr.ReadLine();
                    while (sr.Peek() > -1)
                    {
                        var line = sr.ReadLine();
                        if (line == null || line.Contains("InChIKey")) { continue; }
                        var lineArray = line.Split('\t');
                        if (lineArray.Length < 14) { continue; }
                        if (lineArray.Contains("")) { continue; }
                        predictedList.Add(line);
                    }
                }
            }
            predictedList = predictedList.Distinct().ToList();

            using (var sw = new StreamWriter(dbFileName, false, Encoding.ASCII))
            {
                sw.WriteLine(headerLine);
                foreach (var item in predictedList)
                {
                    sw.WriteLine(item);
                }
            }

        }

        public static void generateDicOfPredictVs2(string predictedFilesDirectry, string dbFileName)
        {
            var headerLine = "";
            var predictedFileList = new List<string>(Directory.GetFiles(predictedFilesDirectry));

            var resultDic = new Dictionary<string, Dictionary<string, string>>();

            foreach (var predictedFile in predictedFileList)
            {
                using (var sr = new StreamReader(predictedFile, false))
                {
                    headerLine = sr.ReadLine();
                    if (headerLine == ""||headerLine == null) { continue; }
                    var headerLineArray = headerLine.Split('\t');

                    while (sr.Peek() > -1)
                    {
                        var line = sr.ReadLine();
                        if (line == null || line.Contains("InChIKey")) { continue; }
                        var lineArray = line.Split('\t');
                        var lineDic = new Dictionary<string, string>();
                        if (resultDic.ContainsKey(lineArray[0])) { continue; }
                        for (int i = 0; i < lineArray.Length; i++)
                        {
                            lineDic.Add(headerLineArray[i], lineArray[i]);
                        }
                        resultDic.Add(lineArray[0], lineDic);
                    }
                }
            }

            var resultHeaderList = new List<string>() { "InChIKey", "SMILES", "RT" };
            var adductList = adductDic.adductIonDic.Keys;
            foreach (var item in adductList)
            {
                resultHeaderList.Add(item.ToString());
            }
            var resultHeaderLine = string.Join("\t", resultHeaderList);
            using (var sw = new StreamWriter(dbFileName, false, Encoding.ASCII))
            {
                sw.WriteLine(resultHeaderLine);
                foreach (var item in resultDic)
                {
                    var line2 = new List<string>();
                    for (int i = 0; i < item.Value.Count; i++)
                    {
                        line2.Add(item.Value[resultHeaderList[i]]);

                    }
                    sw.WriteLine(string.Join("\t", line2));
                }
            }

        }


        public static void generateInchikeyAndSmilesListFromMsp(string mspFilePath)
        {
            var outputFilePath = Path.GetDirectoryName(mspFilePath) + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_InChIKey-SMILES.txt";
            var mspDB = MspFileParser.MspFileReader(mspFilePath);
            var inchikeyToSmiles = new Dictionary<string, string>();
            foreach (var query in mspDB)
            {
                if (!inchikeyToSmiles.ContainsKey(query.InChIKey))
                {
                    inchikeyToSmiles[query.InChIKey] = query.SMILES;
                }
            }
            using (var sw = new StreamWriter(outputFilePath, false, Encoding.ASCII))
            {
                sw.WriteLine("InChIKey\tSMILES");
                foreach (var item in inchikeyToSmiles)
                {
                    sw.WriteLine(item.Key + "\t" + item.Value);
                }
            }

        }


        public static void generateInchikeyAndSmilesAndChainsListFromMsp(string mspFilePath)
        {
            var outputFilePath = Path.GetDirectoryName(mspFilePath) + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_InChIKey-SMILES-Chains.txt";
            var mspDB = MspFileParser.MspFileReader(mspFilePath);
            var inchikeySmilesAndChains = new Dictionary<string, List<int>>();

            foreach (var query in mspDB)
            {
                var chainString = query.Name.Replace(";", "_").Replace("/", "_").Replace(" ", "_").Replace("(", "_").Replace(")", "").Replace(":", "_");
                var chainStringArray = chainString.Split('_');
                var chainList = new List<int>();
                foreach (var item in chainStringArray)
                {
                    int result = 0;
                    if (int.TryParse(item, out result))
                    {
                        chainList.Add(result);
                    }
                }
                var inchikeySmiles = query.InChIKey + "\t" + query.SMILES;

                if (inchikeySmilesAndChains.ContainsKey(inchikeySmiles))
                {
                    var itemValueNum = inchikeySmilesAndChains[inchikeySmiles].Count;
                    if (itemValueNum < chainList.Count)
                    {
                        inchikeySmilesAndChains.Remove(inchikeySmiles);
                        inchikeySmilesAndChains[inchikeySmiles] = chainList;
                    }
                }
                else
                {
                    inchikeySmilesAndChains[inchikeySmiles] = chainList;
                }
            }
            using (var sw = new StreamWriter(outputFilePath, false, Encoding.ASCII))
            {
                sw.WriteLine("InChIKey\tSMILES\tChain1C\tChain1DB\tChain2C\tChain2DB\tChain3C\tChain3DB\tChain4C\tChain4DB");
                foreach (var quary in inchikeySmilesAndChains)
                {
                    var chain = "\t0\t0\t0\t0\t0\t0\t0\t0";
                    if (quary.Value.Count > 0)
                    {
                        chain = "";
                        for (int i = 0; i < 8; i++)
                        {
                            var item = 0;
                            if (quary.Value.Count > i)
                            {
                                item = quary.Value[i];
                            }
                            chain = chain + "\t" + item;
                        }

                    }
                    sw.WriteLine(quary.Key + chain);
                }
            }

        }


        public static void mergeRTandCCSintoMsp(string mspFilePath, string calculatedFilePath, string outputFolderPath, string outputNameOption)
        {
            var outputFileName = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_converted.lbm2";
            var outputMspFileName = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_insertRTCCS.msp";
            var outputFileNameDev = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_converted_dev.lbm2";

            Console.WriteLine("Loading the msp file.");

            var mspDB = MspFileParser.MspFileReader(mspFilePath);
            //var mspDB2 = MspFileParcer.MspFileReader(mspFilePath);
            var inchikeyToSmiles = new Dictionary<string, string>();
            foreach (var query in mspDB)
            {
                if (!inchikeyToSmiles.ContainsKey(query.InChIKey))
                {
                    inchikeyToSmiles[query.InChIKey] = query.SMILES;
                }
            }

            var inchikeyToPredictedRt = new Dictionary<string, float>();
            using (var sr = new StreamReader(calculatedFilePath, true))
            {
                var line = sr.ReadLine();
                var lineArray = line.Split('\t');
                while (sr.Peek() > -1)
                {
                    line = sr.ReadLine();
                    if (line == string.Empty) continue;
                    lineArray = line.Split('\t');
                    var inchikey = lineArray[0];
                    var predictedRtString = lineArray[2];
                    var predictedRt = -1.0F;
                    if (float.TryParse(predictedRtString, out predictedRt) && !inchikeyToPredictedRt.ContainsKey(inchikey))
                    {
                        inchikeyToPredictedRt[inchikey] = predictedRt;
                    }
                }
            }

            var inchikeyToPredictedCcs = new Dictionary<string, Dictionary<string, string>>();

            using (var sr = new StreamReader(calculatedFilePath, true))
            {
                var header = sr.ReadLine();
                var headerArray = header.Split('\t');
                var adduct = new List<string>();
                foreach (string str in headerArray)
                {
                    adduct.Add(str);
                }

                while (sr.Peek() > -1)
                {
                    var adductAndCcs = new Dictionary<string, string>();
                    var line = sr.ReadLine();
                    if (line == string.Empty) continue;
                    var lineArray = line.Split('\t');
                    var inchikey = lineArray[0];
                    for (int i = 2; i < headerArray.Count(); i++)
                    {
                        if (lineArray.Length == i)
                        {
                            Array.Resize(ref lineArray, lineArray.Length + 1);
                            lineArray[i] = "";
                        }

                        adductAndCcs.Add(adduct[i], lineArray[i]);
                    }

                    if (!inchikeyToPredictedCcs.ContainsKey(lineArray[0]))
                    {
                        inchikeyToPredictedCcs.Add(inchikey, adductAndCcs);
                    }
                }
            }

            var errCount = 0;
            var errList = new List<string>();
            foreach (var query in mspDB)
            {
                if (query.InChIKey == "" || query.InChIKey == null)
                {
                    continue;
                }

                if (inchikeyToPredictedRt.ContainsKey(query.InChIKey))
                {
                    if (inchikeyToPredictedRt[query.InChIKey] == 0)
                    {
                        continue;
                    }
                    else if (query.ChromXs.RT.Value == -1)
                    {
                        query.ChromXs = new ChromXs(inchikeyToPredictedRt[query.InChIKey], ChromXType.RT, ChromXUnit.Min);
                    }
                }
                else
                {
                    errCount = errCount + 1;
                    errList.Add(query.InChIKey + "\t" + query.SMILES);
                    //Console.WriteLine("Error at {0}", query.InChIKey);
                }

                if (inchikeyToPredictedCcs.ContainsKey(query.InChIKey))
                {
                    var CCSs = inchikeyToPredictedCcs[query.InChIKey];
                    if (CCSs.ContainsKey(query.AdductType.AdductIonName))
                    {
                        var adductCCS = CCSs[query.AdductType.AdductIonName];
                        if (adductCCS == "" || adductCCS == "0") { continue; }
                        query.CollisionCrossSection = double.Parse(adductCCS);
                    }
                }
                else
                {
                    errCount = errCount + 1;
                    errList.Add(query.InChIKey + "\t" + query.SMILES);
                    //Console.WriteLine("Error at {0}", query.InChIKey);
                }
            }

            if (errCount > 0)
            {
                var tempCsvFilePath2 = outputFolderPath + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_notfound.txt";
                errList = errList.Distinct().ToList();

                using (var sw = new StreamWriter(tempCsvFilePath2, false, Encoding.ASCII))
                {
                    sw.WriteLine("InChIKey\tSMILES");
                    foreach (var item in errList)
                    {
                        sw.WriteLine(item);
                    }
                }

                Console.WriteLine("empty parameters found...see txt file");
                Console.ReadKey();
            }
            else
            {
                var tempCsvFilePath = outputFolderPath + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
                var counter = 0;
                using (var sw = new StreamWriter(tempCsvFilePath, false, Encoding.ASCII))
                {
                    sw.WriteLine("Name,InChIKey,SMILES");
                    foreach (var pair in inchikeyToSmiles)
                    {
                        sw.WriteLine("ID_" + counter + "," + pair.Key + "," + pair.Value);
                        counter++;
                    }
                }

                var mspDB2 = MspFileParcer.MspFileReader(mspFilePath);
                foreach (var query in mspDB2)
                {
                    if (query.InchiKey == "" || query.InchiKey == null)
                    {
                        continue;
                    }

                    if (inchikeyToPredictedRt.ContainsKey(query.InchiKey))
                    {
                        if (inchikeyToPredictedRt[query.InchiKey] == 0)
                        {
                            continue;
                        }
                        else if (query.RetentionTime == -1)
                        {
                            query.RetentionTime = inchikeyToPredictedRt[query.InchiKey];
                        }
                    }
                    else
                    {
                        errCount = errCount + 1;
                        errList.Add(query.InchiKey + "\t" + query.Smiles);
                        Console.WriteLine("Error at {0}", query.InchiKey);
                    }

                    if (inchikeyToPredictedCcs.ContainsKey(query.InchiKey))
                    {
                        var CCSs = inchikeyToPredictedCcs[query.InchiKey];
                        if (CCSs.ContainsKey(query.AdductIonBean.AdductIonName))
                        {
                            var adductCCS = CCSs[query.AdductIonBean.AdductIonName];
                            if (adductCCS == "" || adductCCS == "0") { continue; }
                            query.CollisionCrossSection = float.Parse(adductCCS);
                        }
                    }
                    else
                    {
                        errCount = errCount + 1;
                        errList.Add(query.InchiKey + "\t" + query.Smiles);
                        Console.WriteLine("Error at {0}", query.InchiKey);
                    }
                }

                Console.WriteLine("Exporting...");

                using (var sw = new StreamWriter(outputMspFileName, false, Encoding.ASCII))
                {
                    foreach (var storage in mspDB2)
                    {
                        writeMspFields(storage, sw);
                    }
                }

                MoleculeMsRefMethods.SaveMspToFile(mspDB, outputFileNameDev);
                MspMethods.SaveMspToFile(mspDB2, outputFileName);
            }
        }

        public static void mergeRTandCCSintoMspVs3(string mspFilePath, List<string[]> calculatedFilePaths, string outputFolderPath)
        {

            Console.WriteLine("Loading the msp file.");

            var mspDB = MspFileParser.MspFileReader(mspFilePath);
            var mspDB2 = MspFileParcer.MspFileReader(mspFilePath);
            var inchikeyToSmiles = new Dictionary<string, string>();
            foreach (var query in mspDB)
            {
                if (!inchikeyToSmiles.ContainsKey(query.InChIKey))
                {
                    inchikeyToSmiles[query.InChIKey] = query.SMILES;
                }
            }

            foreach (var calculatedFile in calculatedFilePaths)
            {
                var calculatedFilePath= calculatedFile[0];
                var outputNameOption = calculatedFile[1];

                var outputFileName = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_converted.lbm2";
                var outputMspFileName = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_insertRTCCS.msp";
                var outputFileNameDev = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_converted_dev.lbm2";
                var inchikeyToPredictedRt = new Dictionary<string, float>();
                using (var sr = new StreamReader(calculatedFilePath, true))
                {
                    var line = sr.ReadLine();
                    var lineArray = line.Split('\t');
                    while (sr.Peek() > -1)
                    {
                        line = sr.ReadLine();
                        if (line == string.Empty) continue;
                        lineArray = line.Split('\t');
                        var inchikey = lineArray[0];
                        var predictedRtString = lineArray[2];
                        var predictedRt = -1.0F;
                        if (float.TryParse(predictedRtString, out predictedRt) && !inchikeyToPredictedRt.ContainsKey(inchikey))
                        {
                            inchikeyToPredictedRt[inchikey] = predictedRt;
                        }
                    }
                }

                var inchikeyToPredictedCcs = new Dictionary<string, Dictionary<string, string>>();

                using (var sr = new StreamReader(calculatedFilePath, true))
                {
                    var header = sr.ReadLine();
                    var headerArray = header.Split('\t');
                    var adduct = new List<string>();
                    foreach (string str in headerArray)
                    {
                        adduct.Add(str);
                    }

                    while (sr.Peek() > -1)
                    {
                        var adductAndCcs = new Dictionary<string, string>();
                        var line = sr.ReadLine();
                        if (line == string.Empty) continue;
                        var lineArray = line.Split('\t');
                        var inchikey = lineArray[0];
                        for (int i = 2; i < headerArray.Count(); i++)
                        {
                            if (lineArray.Length == i)
                            {
                                Array.Resize(ref lineArray, lineArray.Length + 1);
                                lineArray[i] = "";
                            }

                            adductAndCcs.Add(adduct[i], lineArray[i]);
                        }

                        if (!inchikeyToPredictedCcs.ContainsKey(lineArray[0]))
                        {
                            inchikeyToPredictedCcs.Add(inchikey, adductAndCcs);
                        }
                    }
                }

                var errCount = 0;
                var errList = new List<string>();
                foreach (var query in mspDB)
                {
                    if (query.InChIKey == "" || query.InChIKey == null)
                    {
                        continue;
                    }

                    if (inchikeyToPredictedRt.ContainsKey(query.InChIKey))
                    {
                        if (inchikeyToPredictedRt[query.InChIKey] == 0)
                        {
                            continue;
                        }
                        else if (query.ChromXs.RT.Value == -1)
                        {
                            query.ChromXs = new ChromXs(inchikeyToPredictedRt[query.InChIKey], ChromXType.RT, ChromXUnit.Min);
                        }
                    }
                    else
                    {
                        errCount = errCount + 1;
                        errList.Add(query.InChIKey + "\t" + query.SMILES);
                        //Console.WriteLine("Error at {0}", query.InChIKey);
                    }

                    if (inchikeyToPredictedCcs.ContainsKey(query.InChIKey))
                    {
                        var CCSs = inchikeyToPredictedCcs[query.InChIKey];
                        if (CCSs.ContainsKey(query.AdductType.AdductIonName))
                        {
                            var adductCCS = CCSs[query.AdductType.AdductIonName];
                            if (adductCCS == "" || adductCCS == "0") { continue; }
                            query.CollisionCrossSection = double.Parse(adductCCS);
                        }
                    }
                    else
                    {
                        errCount = errCount + 1;
                        errList.Add(query.InChIKey + "\t" + query.SMILES);
                        //Console.WriteLine("Error at {0}", query.InChIKey);
                    }
                }

                if (errCount > 0)
                {
                    var tempCsvFilePath2 = outputFolderPath + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + outputNameOption + "_notfound.txt";
                    errList = errList.Distinct().ToList();

                    using (var sw = new StreamWriter(tempCsvFilePath2, false, Encoding.ASCII))
                    {
                        sw.WriteLine("InChIKey\tSMILES");
                        foreach (var item in errList)
                        {
                            sw.WriteLine(item);
                        }
                    }

                    Console.WriteLine("empty parameters found...see txt file");
                    Console.ReadKey();
                }
                else
                {
                    var tempCsvFilePath = outputFolderPath + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
                    var counter = 0;
                    using (var sw = new StreamWriter(tempCsvFilePath, false, Encoding.ASCII))
                    {
                        sw.WriteLine("Name,InChIKey,SMILES");
                        foreach (var pair in inchikeyToSmiles)
                        {
                            sw.WriteLine("ID_" + counter + "," + pair.Key + "," + pair.Value);
                            counter++;
                        }
                    }

                    //var mspDB2 = MspFileParcer.MspFileReader(mspFilePath);
                    foreach (var query in mspDB2)
                    {
                        if (query.InchiKey == "" || query.InchiKey == null)
                        {
                            continue;
                        }

                        if (inchikeyToPredictedRt.ContainsKey(query.InchiKey))
                        {
                            if (inchikeyToPredictedRt[query.InchiKey] == 0)
                            {
                                continue;
                            }
                            else if (query.RetentionTime == -1)
                            {
                                query.RetentionTime = inchikeyToPredictedRt[query.InchiKey];
                            }
                        }
                        else
                        {
                            errCount = errCount + 1;
                            errList.Add(query.InchiKey + "\t" + query.Smiles);
                            Console.WriteLine("Error at {0}", query.InchiKey);
                        }

                        if (inchikeyToPredictedCcs.ContainsKey(query.InchiKey))
                        {
                            var CCSs = inchikeyToPredictedCcs[query.InchiKey];
                            if (CCSs.ContainsKey(query.AdductIonBean.AdductIonName))
                            {
                                var adductCCS = CCSs[query.AdductIonBean.AdductIonName];
                                if (adductCCS == "" || adductCCS == "0") { continue; }
                                query.CollisionCrossSection = float.Parse(adductCCS);
                            }
                        }
                        else
                        {
                            errCount = errCount + 1;
                            errList.Add(query.InchiKey + "\t" + query.Smiles);
                            Console.WriteLine("Error at {0}", query.InchiKey);
                        }
                    }

                    Console.WriteLine("Exporting...");

                    using (var sw = new StreamWriter(outputMspFileName, false, Encoding.ASCII))
                    {
                        foreach (var storage in mspDB2)
                        {
                            writeMspFields(storage, sw);
                        }
                    }

                    MoleculeMsRefMethods.SaveMspToFile(mspDB, outputFileNameDev);
                    MspMethods.SaveMspToFile(mspDB2, outputFileName);
                }

            }
        }


        public static void mergeRTandCCSintoMspVs2(string mspFilePath, string calculatedFilePath, string outputFolderPath, string outputNameOption)
        {
            var outputFileName = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_converted.lbm2";
            var outputMspFileName = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_insertRTCCS.msp";
            var outputFileNameDev = outputFolderPath + "\\" + Path.GetFileNameWithoutExtension(mspFilePath) + "_" + outputNameOption + "_converted_dev.lbm2";

            Console.WriteLine("Loading the msp file.");

            var mspDB = MspFileParser.MspFileReader(mspFilePath);
            //var mspDB2 = MspFileParcer.MspFileReader(mspFilePath);
            var inchikeyToSmiles = new Dictionary<string, string>();
            foreach (var query in mspDB)
            {
                if (!inchikeyToSmiles.ContainsKey(query.InChIKey))
                {
                    inchikeyToSmiles[query.InChIKey] = query.SMILES;
                }
            }

            var tempCsvFilePath = outputFolderPath + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".csv";
            var counter = 0;
            using (var sw = new StreamWriter(tempCsvFilePath, false, Encoding.ASCII))
            {
                sw.WriteLine("Name,InChIKey,SMILES");
                foreach (var pair in inchikeyToSmiles)
                {
                    sw.WriteLine("ID_" + counter + "," + pair.Key + "," + pair.Value);
                    counter++;
                }
            }

            var inchikeyToPredictedRt = new Dictionary<string, float>();
            using (var sr = new StreamReader(calculatedFilePath, true))
            {
                var line = sr.ReadLine();
                var lineArray = line.Split('\t');
                while (sr.Peek() > -1)
                {
                    line = sr.ReadLine();
                    if (line == string.Empty) continue;
                    lineArray = line.Split('\t');
                    var inchikey = lineArray[0];
                    var predictedRtString = lineArray[2];
                    var predictedRt = -1.0F;
                    if (float.TryParse(predictedRtString, out predictedRt) && !inchikeyToPredictedRt.ContainsKey(inchikey))
                    {
                        inchikeyToPredictedRt[inchikey] = predictedRt;
                    }
                }
            }

            var inchikeyToPredictedCcs = new Dictionary<string, Dictionary<string, string>>();

            using (var sr = new StreamReader(calculatedFilePath, true))
            {
                var header = sr.ReadLine();
                var headerArray = header.Split('\t');
                var adduct = new List<string>();
                foreach (string str in headerArray)
                {
                    adduct.Add(str);
                }

                while (sr.Peek() > -1)
                {
                    var adductAndCcs = new Dictionary<string, string>();
                    var line = sr.ReadLine();
                    if (line == string.Empty) continue;
                    var lineArray = line.Split('\t');
                    var inchikey = lineArray[0];
                    for (int i = 2; i < headerArray.Count(); i++)
                    {
                        if (i >= lineArray.Length)
                        {
                            continue;
                        };
                        adductAndCcs.Add(adduct[i], lineArray[i]);
                    }

                    if (!inchikeyToPredictedCcs.ContainsKey(lineArray[0]))
                    {
                        inchikeyToPredictedCcs.Add(inchikey, adductAndCcs);
                    }
                }
            }

            var errCount = 0;
            var errList = new List<string>();
            foreach (var query in mspDB)
            {
                if (query.InChIKey == "" || query.InChIKey == null)
                {
                    continue;
                }

                if (inchikeyToPredictedRt.ContainsKey(query.InChIKey))
                {
                    if (inchikeyToPredictedRt[query.InChIKey] == 0)
                    {
                        continue;
                    }
                    query.ChromXs = new ChromXs(inchikeyToPredictedRt[query.InChIKey], ChromXType.RT, ChromXUnit.Min);
                }
                else
                {
                    errCount = errCount + 1;
                    errList.Add(query.InChIKey + "\t" + query.SMILES);
                    //Console.WriteLine("Error at {0}", query.InChIKey);
                }

                if (inchikeyToPredictedCcs.ContainsKey(query.InChIKey))
                {
                    var CCSs = inchikeyToPredictedCcs[query.InChIKey];
                    if (CCSs.ContainsKey(query.AdductType.AdductIonName))
                    {
                        var adductCCS = CCSs[query.AdductType.AdductIonName];
                        if (adductCCS == "" || adductCCS == "0")
                        {
                            errCount = errCount + 1;
                            errList.Add(query.InChIKey + "\t" + query.SMILES);
                            continue;
                        }
                        query.CollisionCrossSection = double.Parse(adductCCS);
                    }
                }
                else
                {
                    errCount = errCount + 1;
                    errList.Add(query.InChIKey + "\t" + query.SMILES);
                    //Console.WriteLine("Error at {0}", query.InChIKey);
                }
            }

            if (errCount > 0)
            {
                var tempCsvFilePath2 = outputFolderPath + "\\" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_notfound.txt";
                errList = errList.Distinct().ToList();

                using (var sw = new StreamWriter(tempCsvFilePath2, false, Encoding.ASCII))
                {
                    sw.WriteLine("InChIKey\tSMILES");
                    foreach (var item in errList)
                    {
                        sw.WriteLine(item);
                    }
                }

                Console.WriteLine("empty parameters found...see txt file");
                Console.ReadKey();
            }
            else
            {
                var mspDB2 = MspFileParcer.MspFileReader(mspFilePath);
                foreach (var query in mspDB2)
                {
                    if (query.InchiKey == "" || query.InchiKey == null)
                    {
                        continue;
                    }

                    if (inchikeyToPredictedRt.ContainsKey(query.InchiKey))
                    {
                        if (inchikeyToPredictedRt[query.InchiKey] == 0)
                        {
                            continue;
                        }
                        query.RetentionTime = inchikeyToPredictedRt[query.InchiKey];
                    }
                    else
                    {
                        errCount = errCount + 1;
                        errList.Add(query.InchiKey + "\t" + query.Smiles);
                        Console.WriteLine("Error at {0}", query.InchiKey);
                    }

                    if (inchikeyToPredictedCcs.ContainsKey(query.InchiKey))
                    {
                        var CCSs = inchikeyToPredictedCcs[query.InchiKey];
                        if (CCSs.ContainsKey(query.AdductIonBean.AdductIonName))
                        {
                            var adductCCS = CCSs[query.AdductIonBean.AdductIonName];
                            if (adductCCS == "" || adductCCS == "0") { continue; }
                            query.CollisionCrossSection = float.Parse(adductCCS);
                        }
                    }
                    else
                    {
                        errCount = errCount + 1;
                        errList.Add(query.InchiKey + "\t" + query.Smiles);
                        Console.WriteLine("Error at {0}", query.InchiKey);
                    }
                }

                Console.WriteLine("Exporting...");

                using (var sw = new StreamWriter(outputMspFileName, false, Encoding.ASCII))
                {
                    foreach (var storage in mspDB2)
                    {
                        writeMspFields(storage, sw);
                    }
                }

                MoleculeMsRefMethods.SaveMspToFile(mspDB, outputFileNameDev);
                MspMethods.SaveMspToFile(mspDB2, outputFileName);
            }
        }

        public static void writeMspFields(MspFormatCompoundInformationBean mspStorage, StreamWriter sw)
        {
            sw.WriteLine("NAME: " + mspStorage.Name);
            sw.WriteLine("PRECURSORMZ: " + mspStorage.PrecursorMz);
            sw.WriteLine("PRECURSORTYPE: " + mspStorage.AdductIonBean.AdductIonName);
            sw.WriteLine("IONMODE: " + mspStorage.IonMode);
            sw.WriteLine("FORMULA: " + mspStorage.Formula);
            sw.WriteLine("SMILES: " + mspStorage.Smiles);
            sw.WriteLine("INCHIKEY: " + mspStorage.InchiKey);
            sw.WriteLine("COMPOUNDCLASS: " + mspStorage.CompoundClass);
            sw.WriteLine("RETENTIONTIME: " + mspStorage.RetentionTime);

            sw.WriteLine("CCS: " + mspStorage.CollisionCrossSection);
            sw.WriteLine("COMMENT: " + mspStorage.Comment);

            var peakList = new List<Peak>();
            foreach (var peak in mspStorage.MzIntensityCommentBeanList)
            {
                peakList.Add
                    (new Peak()
                    {
                        Mz = Math.Round(peak.Mz, 3),
                        Intensity = peak.Intensity,
                        Comment = peak.Comment
                    }
                );
            }
            var peaknum = peakList.Count().ToString();

            sw.WriteLine("Num Peaks: " + peaknum);

            peakList = peakList.OrderBy(n => -n.Mz).ToList();

            foreach (var peak in peakList)
            {
                sw.WriteLine(peak.Mz + "\t" + peak.Intensity
                    //+ "\t\"" + peak.Comment + "\""
                    );
            }

            sw.WriteLine();
        }




        public static string getInchikeyFirstHalf(string inchikey)
        {
            var inchikeyHalf = "";
            if (inchikey.Length > 0)
            {
                inchikeyHalf = inchikey.Substring(0, 14);
            }

            return inchikeyHalf;
        }

    }
}

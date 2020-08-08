﻿using System;
using System.Collections.Generic;
using System.Linq;
using AssistantScrapMechanic.Domain.AppFiles;
using AssistantScrapMechanic.Domain.Constant;
using AssistantScrapMechanic.Domain.DataFiles;
using AssistantScrapMechanic.Domain.Enum;
using AssistantScrapMechanic.Domain.IntermediateFiles;
using AssistantScrapMechanic.Integration;
using AssistantScrapMechanic.Logic.Calculator;

namespace AssistantScrapMechanic.GameFilesReader.FileHandlers
{
    public class DataFileHandler
    {
        private readonly FileSystemRepository _appDataSysRepo;
        private readonly FileSystemRepository _inputFileSysRepo;

        public DataFileHandler(FileSystemRepository inputFileSysRepo, FileSystemRepository appDataSysRepo)
        {
            _appDataSysRepo = appDataSysRepo;
            _inputFileSysRepo = inputFileSysRepo;
        }

        public void GenerateDataFiles(List<GameItemLocalised> localisedGameItems)
        {
            LootDataFile lootData = _inputFileSysRepo.LoadJsonFile<LootDataFile>(DataFile.SurvivalLoot);

            List<string> tempAllNames = new List<string>();
            tempAllNames.AddRange(LootCalculator.GetListOfGameNames(lootData.LootTable.RandomLoot));
            tempAllNames.AddRange(LootCalculator.GetListOfGameNames(lootData.LootTable.RandomEpicLoot));
            //tempAllNames.AddRange(LootCalculator.GetListOfGameNames(lootData.LootTable.RandomLootWarehouse));

            tempAllNames.AddRange(LootCalculator.GetListOfGameNames(lootData.LootCrate.SelectOne));
            tempAllNames.AddRange(LootCalculator.GetListOfGameNames(lootData.LootCrateEpic.SelectOne));
            //tempAllNames.AddRange(LootCalculator.GetListOfGameNames(lootData.LootCrateEpicWareHouse.SelectOne));

            HashSet<string> allNamesHashSet = new HashSet<string>();
            foreach (string tempAllName in tempAllNames)
            {
                allNamesHashSet.Add(tempAllName);
            }

            List<string> allNames = allNamesHashSet.ToList();
            Dictionary<string, string> gameNameToAppIdLookup = new Dictionary<string, string>();

            foreach (string gameName in allNames)
            {
                if (gameNameToAppIdLookup.ContainsKey(gameName)) continue;
                bool found = false;
                foreach (GameItemLocalised gameItemLoc in localisedGameItems)
                {
                    if (!gameName.Equals(gameItemLoc.GameName)) continue;
                    if (gameNameToAppIdLookup.ContainsKey(gameName)) continue;

                    gameNameToAppIdLookup.Add(gameName, gameItemLoc.AppId);
                    found = true;
                    break;
                }

                if (!found) throw new Exception("Not found");
            }

            Dictionary<string, LootQuantitiesLookup> quantitiesLookup = new Dictionary<string, LootQuantitiesLookup>();
            foreach (LootQuantitiesLookup quantityLookup in lootData.LootQuantitiesLookup)
            {
                if (quantitiesLookup.ContainsKey(quantityLookup.Name)) continue;
                quantitiesLookup.Add(quantityLookup.Name, quantityLookup);
            }

            Dictionary<string, AppLoot> appLootDict = new Dictionary<string, AppLoot>();
            AddToAppLootDictionary(appLootDict, lootData.LootCrate.SelectOne, AppLootContainerType.CommonChest, gameNameToAppIdLookup, quantitiesLookup);
            AddToAppLootDictionary(appLootDict, lootData.LootTable.RandomLoot, AppLootContainerType.CommonChest, gameNameToAppIdLookup, quantitiesLookup);
            // TODO derive random loot from LootContainer

            AddToAppLootDictionary(appLootDict, lootData.LootCrateEpic.SelectOne, AppLootContainerType.RareChest, gameNameToAppIdLookup, quantitiesLookup);
            AddToAppLootDictionary(appLootDict, lootData.LootTable.RandomEpicLoot, AppLootContainerType.RareChest, gameNameToAppIdLookup, quantitiesLookup);

            List<AppLoot> appLoots = new List<AppLoot>();
            foreach (string dictKey in appLootDict.Keys)
            {
                AppLoot appLoot = appLootDict[dictKey];
                Dictionary<string, AppLootChance> uniqueChances = new Dictionary<string, AppLootChance>();
                foreach (AppLootChance chance in appLoot.Chances)
                {
                    string key = $"{chance.Chance}{chance.Max}{chance.Min}{chance.Type}";
                    if (uniqueChances.ContainsKey(key)) continue;

                    uniqueChances.Add(key, chance);
                }
                List<AppLootChance> newAppChances = new List<AppLootChance>();
                foreach (string uniqueChanceKey in uniqueChances.Keys)
                {
                    newAppChances.Add(uniqueChances[uniqueChanceKey]);
                }
                appLoots.Add(new AppLoot
                {
                    AppId = appLoot.AppId,
                    Chances = newAppChances,
                });
            }

            _appDataSysRepo.WriteBackToJsonFile(appLoots, AppFile.Loot);
        }

        private static void AddToAppLootDictionary(Dictionary<string, AppLoot> appLoopDict, List<LootChance> selectOne, AppLootContainerType containerType, Dictionary<string, string> gameNameToAppIdLookup, Dictionary<string, LootQuantitiesLookup> quantitiesLookup)
        {
            int normalTotalChance = LootCalculator.TotalChanceValue(selectOne);
            foreach (LootChance normalSelectOneLootChance in selectOne)
            {
                if (!gameNameToAppIdLookup.ContainsKey(normalSelectOneLootChance.GameName)) continue;
                string appId = gameNameToAppIdLookup[normalSelectOneLootChance.GameName];

                double percentChance = (normalSelectOneLootChance.Chance / (normalTotalChance * 1.0)) * 100;
                LootQuantitiesLookup quantityKey = quantitiesLookup[normalSelectOneLootChance.QuantityKey];
                AppLootChance newChance = new AppLootChance
                {
                    Chance = (int)Math.Round(percentChance, 0),
                    Min = quantityKey.Min,
                    Max = quantityKey.Max,
                    Type = containerType,
                };
                if (appLoopDict.ContainsKey(appId))
                {
                    AppLoot current = appLoopDict[appId];
                    //if (current.Chances.Any(c => c.Type == containerType)) continue;
                    current.Chances.Add(newChance);
                    appLoopDict[appId] = current;
                }
                else
                {
                    appLoopDict.Add(appId, new AppLoot
                    {
                        AppId = appId,
                        Chances = new List<AppLootChance>
                            { newChance }
                    });
                }
            }
        }
    }
}
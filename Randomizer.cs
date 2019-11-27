using MMRando.Constants;
using MMRando.LogicMigrator;
using MMRando.Models;
using MMRando.Models.Rom;
using MMRando.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MMRando
{

    public class Randomizer
    {
        private Random _random { get; set; }
        public Random Random
        {
            get => _random;
            set => _random = value;
        }

        public List<ItemObject> ItemList { get; set; }

        List<Gossip> GossipList { get; set; }

        #region Dependence and Conditions
        List<int> ConditionsChecked { get; set; }
        Dictionary<int, Dependence> DependenceChecked { get; set; }
        List<int[]> ConditionRemoves { get; set; }

        private class Dependence
        {
            public int[] ItemIds { get; set; }
            public DependenceType Type { get; set; }

            public static Dependence Dependent => new Dependence { Type = DependenceType.Dependent };
            public static Dependence NotDependent => new Dependence { Type = DependenceType.NotDependent };
            public static Dependence Circular(params int[] itemIds) => new Dependence { ItemIds = itemIds, Type = DependenceType.Circular };
        }

        private enum DependenceType
        {
            Dependent,
            NotDependent,
            Circular
        }

        Dictionary<int, List<int>> ForbiddenReplacedBy = new Dictionary<int, List<int>>
        {
            // Deku_Mask should not be replaced by trade items, or items that can be downgraded.
            {
                Items.MaskDeku, new List<int>
                {
                    Items.UpgradeGildedSword,
                    Items.UpgradeMirrorShield,
                    Items.UpgradeBiggestQuiver,
                    Items.UpgradeBigBombBag,
                    Items.UpgradeBiggestBombBag,
                    Items.UpgradeGiantWallet
                }
                .Concat(Enumerable.Range(Items.TradeItemMoonTear, Items.TradeItemMamaLetter - Items.TradeItemMoonTear + 1))
                .Concat(Enumerable.Range(Items.ItemBottleWitch, Items.ItemBottleMadameAroma - Items.ItemBottleWitch + 1))
                .ToList()
            },

            // Keaton_Mask and Mama_Letter are obtained one directly after another
            // Keaton_Mask cannot be replaced by items that may be overwritten by item obtained at Mama_Letter
            {
                Items.MaskKeaton,
                new List<int> {
                    Items.UpgradeGiantWallet,
                    Items.UpgradeGildedSword,
                    Items.UpgradeMirrorShield,
                    Items.UpgradeBiggestQuiver,
                    Items.UpgradeBigBombBag,
                    Items.UpgradeBiggestBombBag,
                    Items.TradeItemMoonTear,
                    Items.TradeItemLandDeed,
                    Items.TradeItemSwampDeed,
                    Items.TradeItemMountainDeed,
                    Items.TradeItemOceanDeed,
                    Items.TradeItemRoomKey,
                    Items.TradeItemMamaLetter,
                    Items.TradeItemKafeiLetter,
                    Items.TradeItemPendant
                }
            },
        };

        Dictionary<int, List<int>> ForbiddenPlacedAt = new Dictionary<int, List<int>>
        {
        };

        #endregion

        private Settings _settings;
        private RandomizedResult _randomized;

        public Randomizer(Settings settings)
        {
            _settings = settings;
        }

        //rando functions

        #region Gossip quotes

        private void MakeGossipQuotes()
        {
            var gossipQuotes = new List<string>();
            ReadAndPopulateGossipList();

            for (int itemIndex = 0; itemIndex < ItemList.Count; itemIndex++)
            {
                if (!ItemList[itemIndex].ReplacesAnotherItem)
                {
                    continue;
                }

                // Skip hints for vanilla bottle content
                if ((!_settings.RandomizeBottleCatchContents)
                    && ItemUtils.IsBottleCatchContent(itemIndex))
                {
                    continue;
                }

                // Skip hints for vanilla shop items
                if ((!_settings.AddShopItems)
                    && ItemUtils.IsShopItem(itemIndex))
                {
                    continue;
                }

                // Skip hints for vanilla dungeon items
                if (!_settings.AddDungeonItems
                    && ItemUtils.IsDungeonItem(itemIndex))
                {
                    continue;
                }

                // Skip hint for song of soaring
                if (_settings.ExcludeSongOfSoaring && itemIndex == Items.SongSoaring)
                {
                    continue;
                }

                // Skip hints for moon items
                if (!_settings.AddMoonItems
                    && ItemUtils.IsMoonItem(itemIndex))
                {
                    continue;
                }

                // Skip hints for other items
                if (!_settings.AddOther
                    && ItemUtils.IsOtherItem(itemIndex))
                {
                    continue;
                }

                int sourceItemId = ItemList[itemIndex].ReplacesItemId;
                sourceItemId = ItemUtils.SubtractItemOffset(sourceItemId);

                int toItemId = itemIndex;
                toItemId = ItemUtils.SubtractItemOffset(toItemId);

                // 5% chance of being fake
                bool isFake = (Random.Next(100) < 5);
                if (isFake)
                {
                    sourceItemId = Random.Next(GossipList.Count);
                }

                int sourceMessageLength = GossipList[sourceItemId]
                    .SourceMessage
                    .Length;

                int destinationMessageLength = GossipList[toItemId]
                    .DestinationMessage
                    .Length;

                // Randomize messages
                string sourceMessage = GossipList[sourceItemId]
                    .SourceMessage[Random.Next(sourceMessageLength)];

                string destinationMessage = GossipList[toItemId]
                    .DestinationMessage[Random.Next(destinationMessageLength)];

                // Sound differs if hint is fake
                ushort soundEffectId = (ushort)(isFake ? 0x690A : 0x690C);

                var quote = BuildGossipQuote(soundEffectId, sourceMessage, destinationMessage);

                gossipQuotes.Add(quote);
            }

            for (int i = 0; i < Gossip.JunkMessages.Count; i++)
            {
                gossipQuotes.Add(Gossip.JunkMessages[i]);
            }

            _randomized.GossipQuotes = gossipQuotes;
        }

        private void ReadAndPopulateGossipList()
        {
            GossipList = new List<Gossip>();

            string[] gossipLines = Properties.Resources.GOSSIP
                .Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < gossipLines.Length; i += 2)
            {
                var sourceMessage = gossipLines[i].Split(';');
                var destinationMessage = gossipLines[i + 1].Split(';');
                var nextGossip = new Gossip
                {
                    SourceMessage = sourceMessage,
                    DestinationMessage = destinationMessage
                };

                GossipList.Add(nextGossip);
            }
        }

        public string BuildGossipQuote(ushort soundEffectId, string sourceMessage, string destinationMessage)
        {
            int startIndex = Random.Next(Gossip.MessageStartSentences.Count);
            int midIndex = Random.Next(Gossip.MessageMidSentences.Count);
            string start = Gossip.MessageStartSentences[startIndex];
            string mid = Gossip.MessageMidSentences[midIndex];

            string sfx = $"{(char)((soundEffectId >> 8) & 0xFF)}{(char)(soundEffectId & 0xFF)}";

            return $"\x1E{sfx}{start} \x01{sourceMessage}\x00\x11{mid} \x06{destinationMessage}\x00" + "...\xBF";
        }

        #endregion

        private void EntranceShuffle()
        {
            var newDCFlags = new int[] { -1, -1, -1, -1 };
            var newDCMasks = new int[] { -1, -1, -1, -1 };
            var newEntranceIndices = new int[] { -1, -1, -1, -1 };
            var newExitIndices = new int[] { -1, -1, -1, -1 };

            for (int i = 0; i < 4; i++)
            {
                int n;
                do
                {
                    n = Random.Next(4);
                } while (newEntranceIndices.Contains(n));

                newEntranceIndices[i] = n;
                newExitIndices[n] = i;
            }

            var areaAccessObjects = new ItemObject[] {
                ItemList[Items.AreaWoodFallTempleAccess],
                ItemList[Items.AreaSnowheadTempleAccess],
                ItemList[Items.AreaInvertedStoneTowerTempleAccess],
                ItemList[Items.AreaGreatBayTempleAccess]
            };

            var areaAccessObjectIndexes = new int[] {
                Items.AreaWoodFallTempleAccess,
                Items.AreaSnowheadTempleAccess,
                Items.AreaInvertedStoneTowerTempleAccess,
                Items.AreaGreatBayTempleAccess
            };

            for (int i = 0; i < 4; i++)
            {
                Debug.WriteLine($"Entrance {Items.ITEM_NAMES[areaAccessObjectIndexes[newEntranceIndices[i]]]} placed at {Items.ITEM_NAMES[areaAccessObjects[i].ID]}.");
                ItemList[areaAccessObjectIndexes[newEntranceIndices[i]]] = areaAccessObjects[i];
            }

            var areaClearObjects = new ItemObject[] {
                ItemList[Items.AreaWoodFallTempleClear],
                ItemList[Items.AreaSnowheadTempleClear],
                ItemList[Items.AreaStoneTowerClear],
                ItemList[Items.AreaGreatBayTempleClear]
            };

            var areaClearObjectIndexes = new int[] {
                Items.AreaWoodFallTempleClear,
                Items.AreaSnowheadTempleClear,
                Items.AreaStoneTowerClear,
                Items.AreaGreatBayTempleClear
            };

            for (int i = 0; i < 4; i++)
            {
                ItemList[areaClearObjectIndexes[i]] = areaClearObjects[newEntranceIndices[i]];
            }

            var newEntrances = new int[] { -1, -1, -1, -1 };
            var newExits = new int[] { -1, -1, -1, -1 };

            for (int i = 0; i < 4; i++)
            {
                newEntrances[i] = Values.OldEntrances[newEntranceIndices[i]];
                newExits[i] = Values.OldExits[newExitIndices[i]];
                newDCFlags[i] = Values.OldDCFlags[newExitIndices[i]];
                newDCMasks[i] = Values.OldMaskFlags[newExitIndices[i]];
            }

            _randomized.NewEntrances = newEntrances;
            _randomized.NewDestinationIndices = newEntranceIndices;
            _randomized.NewExits = newExits;
            _randomized.NewExitIndices = newExitIndices;
            _randomized.NewDCFlags = newDCFlags;
            _randomized.NewDCMasks = newDCMasks;
        }

        #region Sequences and BGM

        private void BGMShuffle()
        {
            while (RomData.TargetSequences.Count > 0)
            {
                List<SequenceInfo> Unassigned = RomData.SequenceList.FindAll(u => u.Replaces == -1);

                int targetIndex = Random.Next(RomData.TargetSequences.Count);
                var targetSequence = RomData.TargetSequences[targetIndex];

                while (true)
                {
                    int unassignedIndex = Random.Next(Unassigned.Count);

                    if (Unassigned[unassignedIndex].Name.StartsWith("mm")
                        & (Random.Next(100) < 50))
                    {
                        continue;
                    }

                    for (int i = 0; i < Unassigned[unassignedIndex].Type.Count; i++)
                    {
                        if (targetSequence.Type.Contains(Unassigned[unassignedIndex].Type[i]))
                        {
                            Unassigned[unassignedIndex].Replaces = targetSequence.Replaces;
                            Debug.WriteLine(Unassigned[unassignedIndex].Name + " -> " + targetSequence.Name);
                            RomData.TargetSequences.RemoveAt(targetIndex);
                            break;
                        }
                        else if (i + 1 == Unassigned[unassignedIndex].Type.Count)
                        {
                            if ((Random.Next(30) == 0)
                                && ((Unassigned[unassignedIndex].Type[0] & 8) == (targetSequence.Type[0] & 8))
                                && (Unassigned[unassignedIndex].Type.Contains(10) == targetSequence.Type.Contains(10))
                                && (!Unassigned[unassignedIndex].Type.Contains(16)))
                            {
                                Unassigned[unassignedIndex].Replaces = targetSequence.Replaces;
                                Debug.WriteLine(Unassigned[unassignedIndex].Name + " -> " + targetSequence.Name);
                                RomData.TargetSequences.RemoveAt(targetIndex);
                                break;
                            }
                        }
                    }

                    if (Unassigned[unassignedIndex].Replaces != -1)
                    {
                        break;
                    }
                }
            }

            RomData.SequenceList.RemoveAll(u => u.Replaces == -1);
        }

        private void SortBGM()
        {
            if (!_settings.RandomizeBGM)
            {
                return;
            }

            SequenceUtils.ReadSequenceInfo();
            BGMShuffle();
        }

        #endregion

        private void SetTatlColour()
        {
            if (_settings.TatlColorSchema == TatlColorSchema.Rainbow)
            {
                for (int i = 0; i < 10; i++)
                {
                    byte[] c = new byte[4];
                    Random.NextBytes(c);

                    if ((i % 2) == 0)
                    {
                        c[0] = 0xFF;
                    }
                    else
                    {
                        c[0] = 0;
                    }

                    Values.TatlColours[4, i] = BitConverter.ToUInt32(c, 0);
                };
            };
        }

        private void PrepareRulesetItemData()
        {
            ItemList = new List<ItemObject>();

            if (_settings.LogicMode == LogicMode.Casual
                || _settings.LogicMode == LogicMode.Glitched
                || _settings.LogicMode == LogicMode.UserLogic)
            {
                string[] data = ReadRulesetFromResources();
                PopulateItemListFromLogicData(data);
            }
            else
            {
                PopulateItemListWithoutLogic();
            }
        }

        /// <summary>
        /// Populates item list without logic. Default TimeAvailable = 63
        /// </summary>
        private void PopulateItemListWithoutLogic()
        {
            for (var i = 0; i < Items.TotalNumberOfItems; i++)
            {
                var currentItem = new ItemObject
                {
                    ID = i,
                    TimeAvailable = 63
                };

                ItemList.Add(currentItem);
            }
        }

        /// <summary>
        /// Populates the item list using the lines from a logic file, processes them 4 lines per item. 
        /// </summary>
        /// <param name="data">The lines from a logic file</param>
        private void PopulateItemListFromLogicData(string[] data)
        {
            if (Migrator.GetVersion(data.ToList()) != Migrator.CurrentVersion)
            {
                throw new InvalidDataException("Logic file is out of date. Open it in the Logic Editor to bring it up to date.");
            }

            int itemId = 0;
            int lineNumber = 0;

            var currentItem = new ItemObject();

            // Process lines in groups of 4
            foreach (string line in data)
            {
                if (line.Contains("-"))
                {
                    continue;
                }

                switch (lineNumber)
                {
                    case 0:
                        //dependence
                        ProcessDependenciesForItem(currentItem, line);
                        break;
                    case 1:
                        //conditionals
                        ProcessConditionalsForItem(currentItem, line);
                        break;
                    case 2:
                        //time needed
                        currentItem.TimeNeeded = Convert.ToInt32(line);
                        break;
                    case 3:
                        //time available
                        currentItem.TimeAvailable = Convert.ToInt32(line);
                        if (currentItem.TimeAvailable == 0)
                        {
                            currentItem.TimeAvailable = 63;
                        }
                        break;
                }

                lineNumber++;

                if (lineNumber == 4)
                {
                    currentItem.ID = itemId;
                    ItemList.Add(currentItem);

                    currentItem = new ItemObject();

                    itemId++;
                    lineNumber = 0;
                }
            }
        }

        private void ProcessConditionalsForItem(ItemObject currentItem, string line)
        {
            List<List<int>> conditional = new List<List<int>>();

            if (line == "")
            {
                currentItem.Conditionals = null;
            }
            else
            {
                foreach (string conditions in line.Split(';'))
                {
                    int[] conditionaloption = Array.ConvertAll(conditions.Split(','), int.Parse);
                    conditional.Add(conditionaloption.ToList());
                }
                currentItem.Conditionals = conditional;
            }
        }

        private void ProcessDependenciesForItem(ItemObject currentItem, string line)
        {
            List<int> dependencies = new List<int>();

            if (line == "")
            {
                currentItem.DependsOnItems = null;
            }
            else
            {
                foreach (string dependency in line.Split(','))
                {
                    dependencies.Add(Convert.ToInt32(dependency));
                }
                currentItem.DependsOnItems = dependencies;
            }
        }

        public void SeedRNG()
        {
            Random = new Random(_settings.Seed);
        }

        private string[] ReadRulesetFromResources()
        {
            string[] lines = null;
            var mode = _settings.LogicMode;

            if (mode == LogicMode.Casual)
            {
                lines = Properties.Resources.REQ_CASUAL.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
            else if (mode == LogicMode.Glitched)
            {
                lines = Properties.Resources.REQ_GLITCH.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            }
            else if (mode == LogicMode.UserLogic)
            {
                using (StreamReader Req = new StreamReader(File.Open(_settings.UserLogicFileName, FileMode.Open)))
                {
                    lines = Req.ReadToEnd().Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                }
            }

            return lines;
        }

        private Dependence CheckDependence(int CurrentItem, int Target, List<int> dependencyPath)
        {
            Debug.WriteLine($"CheckDependence({CurrentItem}, {Target})");
            if (ItemList[CurrentItem].TimeNeeded == 0
                && !ItemList.Any(io => (io.Conditionals?.Any(c => c.Contains(CurrentItem)) ?? false) || (io.DependsOnItems?.Contains(CurrentItem) ?? false)))
            {
                return Dependence.NotDependent;
            }

            // permanent items ignore dependencies of Blast Mask check
            if (Target == Items.MaskBlast && !ItemUtils.IsTemporaryItem(CurrentItem))
            {
                return Dependence.NotDependent;
            }

            //check timing
            if (ItemList[CurrentItem].TimeNeeded != 0 && dependencyPath.Skip(1).All(p => ItemUtils.IsFakeItem(p) || ItemUtils.IsTemporaryItem(ItemList.Single(i => i.ReplacesItemId == p).ID)))
            {
                if ((ItemList[CurrentItem].TimeNeeded & ItemList[Target].TimeAvailable) == 0)
                {
                    Debug.WriteLine($"{CurrentItem} is needed at {ItemList[CurrentItem].TimeNeeded} but {Target} is only available at {ItemList[Target].TimeAvailable}");
                    return Dependence.Dependent;
                }
            }

            if (ItemList[Target].HasConditionals)
            {
                if (ItemList[Target].Conditionals
                    .FindAll(u => u.Contains(CurrentItem)).Count == ItemList[Target].Conditionals.Count)
                {
                    Debug.WriteLine($"All conditionals of {Target} contains {CurrentItem}");
                    return Dependence.Dependent;
                }

                if (ItemList[CurrentItem].HasCannotRequireItems)
                {
                    for (int i = 0; i < ItemList[CurrentItem].CannotRequireItems.Count; i++)
                    {
                        if (ItemList[Target].Conditionals
                            .FindAll(u => u.Contains(ItemList[CurrentItem].CannotRequireItems[i])
                            || u.Contains(CurrentItem)).Count == ItemList[Target].Conditionals.Count)
                        {
                            Debug.WriteLine($"All conditionals of {Target} cannot be required by {CurrentItem}");
                            return Dependence.Dependent;
                        }
                    }
                }

                int k = 0;
                var circularDependencies = new List<int>();
                for (int i = 0; i < ItemList[Target].Conditionals.Count; i++)
                {
                    bool match = false;
                    for (int j = 0; j < ItemList[Target].Conditionals[i].Count; j++)
                    {
                        int d = ItemList[Target].Conditionals[i][j];
                        if (!ItemUtils.IsFakeItem(d) && !ItemList[d].ReplacesAnotherItem && d != CurrentItem)
                        {
                            continue;
                        }

                        int[] check = new int[] { Target, i, j };

                        if (ItemList[d].ReplacesAnotherItem)
                        {
                            d = ItemList[d].ReplacesItemId;
                        }
                        if (d == CurrentItem)
                        {
                            DependenceChecked[d] = Dependence.Dependent;
                        }
                        else
                        {
                            if (dependencyPath.Contains(d))
                            {
                                DependenceChecked[d] = Dependence.Circular(d);
                            }
                            if (!DependenceChecked.ContainsKey(d) || (DependenceChecked[d].Type == DependenceType.Circular && !DependenceChecked[d].ItemIds.All(id => dependencyPath.Contains(id))))
                            {
                                var childPath = dependencyPath.ToList();
                                childPath.Add(d);
                                DependenceChecked[d] = CheckDependence(CurrentItem, d, childPath);
                            }
                        }

                        if (DependenceChecked[d].Type != DependenceType.NotDependent)
                        {
                            if (!dependencyPath.Contains(d) && DependenceChecked[d].Type == DependenceType.Circular && DependenceChecked[d].ItemIds.All(id => id == d))
                            {
                                DependenceChecked[d] = Dependence.Dependent;
                            }
                            if (DependenceChecked[d].Type == DependenceType.Dependent)
                            {
                                if (!ConditionRemoves.Any(c => c.SequenceEqual(check)))
                                {
                                    ConditionRemoves.Add(check);
                                }
                            }
                            else
                            {
                                circularDependencies = circularDependencies.Union(DependenceChecked[d].ItemIds).ToList();
                            }
                            if (!match)
                            {
                                k++;
                                match = true;
                            }
                        }
                    }
                }

                if (k == ItemList[Target].Conditionals.Count)
                {
                    if (circularDependencies.Any())
                    {
                        return Dependence.Circular(circularDependencies.ToArray());
                    }
                    Debug.WriteLine($"All conditionals of {Target} failed dependency check for {CurrentItem}.");
                    return Dependence.Dependent;
                }
            }

            if (ItemList[Target].DependsOnItems == null)
            {
                return Dependence.NotDependent;
            }

            //cycle through all things
            for (int i = 0; i < ItemList[Target].DependsOnItems.Count; i++)
            {
                int dependency = ItemList[Target].DependsOnItems[i];
                if (dependency == CurrentItem)
                {
                    Debug.WriteLine($"{Target} has direct dependence on {CurrentItem}");
                    return Dependence.Dependent;
                }

                if (ItemList[CurrentItem].HasCannotRequireItems)
                {
                    for (int j = 0; j < ItemList[CurrentItem].CannotRequireItems.Count; j++)
                    {
                        if (ItemList[Target].DependsOnItems.Contains(ItemList[CurrentItem].CannotRequireItems[j]))
                        {
                            Debug.WriteLine($"Dependence {ItemList[CurrentItem].CannotRequireItems[j]} of {Target} cannot be required by {CurrentItem}");
                            return Dependence.Dependent;
                        }
                    }
                }

                if (ItemUtils.IsFakeItem(dependency)
                    || ItemList[dependency].ReplacesAnotherItem)
                {
                    if (ItemList[dependency].ReplacesAnotherItem)
                    {
                        dependency = ItemList[dependency].ReplacesItemId;
                    }

                    if (dependencyPath.Contains(dependency))
                    {
                        DependenceChecked[dependency] = Dependence.Circular(dependency);
                        return DependenceChecked[dependency];
                    }
                    if (!DependenceChecked.ContainsKey(dependency) || (DependenceChecked[dependency].Type == DependenceType.Circular && !DependenceChecked[dependency].ItemIds.All(id => dependencyPath.Contains(id))))
                    {
                        var childPath = dependencyPath.ToList();
                        childPath.Add(dependency);
                        DependenceChecked[dependency] = CheckDependence(CurrentItem, dependency, childPath);
                    }
                    if (DependenceChecked[dependency].Type != DependenceType.NotDependent)
                    {
                        if (DependenceChecked[dependency].Type == DependenceType.Circular && DependenceChecked[dependency].ItemIds.All(id => id == dependency))
                        {
                            DependenceChecked[dependency] = Dependence.Dependent;
                        }
                        Debug.WriteLine($"{CurrentItem} is dependent on {dependency}");
                        return DependenceChecked[dependency];
                    }
                }
            }

            return Dependence.NotDependent;
        }

        private void RemoveConditionals(int CurrentItem)
        {
            for (int i = 0; i < ConditionRemoves.Count; i++)
            {
                int x = ConditionRemoves[i][0];
                int y = ConditionRemoves[i][1];
                int z = ConditionRemoves[i][2];
                ItemList[x].Conditionals[y] = null;
            }

            for (int i = 0; i < ConditionRemoves.Count; i++)
            {
                int x = ConditionRemoves[i][0];
                int y = ConditionRemoves[i][1];
                int z = ConditionRemoves[i][2];

                for (int j = 0; j < ItemList[x].Conditionals.Count; j++)
                {
                    if (ItemList[x].Conditionals[j] != null)
                    {
                        for (int k = 0; k < ItemList[x].Conditionals[j].Count; k++)
                        {
                            int d = ItemList[x].Conditionals[j][k];

                            if (!ItemList[x].HasCannotRequireItems)
                            {
                                ItemList[x].CannotRequireItems = new List<int>();
                            }
                            if (!ItemList[d].CannotRequireItems.Contains(CurrentItem))
                            {
                                ItemList[d].CannotRequireItems.Add(CurrentItem);
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < ItemList.Count; i++)
            {
                if (ItemList[i].Conditionals != null)
                {
                    ItemList[i].Conditionals.RemoveAll(u => u == null);
                }
            }
        }

        private void UpdateConditionals(int CurrentItem, int Target)
        {
            if (!ItemList[Target].HasConditionals)
            {
                return;
            }

            if (ItemList[Target].Conditionals.Count == 1)
            {
                for (int i = 0; i < ItemList[Target].Conditionals[0].Count; i++)
                {
                    if (!ItemList[Target].HasDependencies)
                    {
                        ItemList[Target].DependsOnItems = new List<int>();
                    }

                    int j = ItemList[Target].Conditionals[0][i];
                    if (!ItemList[Target].DependsOnItems.Contains(j))
                    {
                        ItemList[Target].DependsOnItems.Add(j);
                    }
                    if (!ItemList[j].HasCannotRequireItems)
                    {
                        ItemList[j].CannotRequireItems = new List<int>();
                    }
                    if (!ItemList[j].CannotRequireItems.Contains(CurrentItem))
                    {
                        ItemList[j].CannotRequireItems.Add(CurrentItem);
                    }
                }
                ItemList[Target].Conditionals.RemoveAt(0);
            }
            else
            {
                //check if all conditions have a common item
                for (int i = 0; i < ItemList[Target].Conditionals[0].Count; i++)
                {
                    int testitem = ItemList[Target].Conditionals[0][i];
                    if (ItemList[Target].Conditionals.FindAll(u => u.Contains(testitem)).Count == ItemList[Target].Conditionals.Count)
                    {
                        // require this item and remove from conditions
                        if (!ItemList[Target].HasDependencies)
                        {
                            ItemList[Target].DependsOnItems = new List<int>();
                        }
                        if (!ItemList[Target].DependsOnItems.Contains(testitem))
                        {
                            ItemList[Target].DependsOnItems.Add(testitem);
                        }
                        for (int j = 0; j < ItemList[Target].Conditionals.Count; j++)
                        {
                            ItemList[Target].Conditionals[j].Remove(testitem);
                        }

                        break;
                    }
                }
                //for (int i = 0; i < ItemList[Target].Conditional.Count; i++)
                //{
                //    for (int j = 0; j < ItemList[Target].Conditional[i].Count; j++)
                //    {
                //        int k = ItemList[Target].Conditional[i][j];
                //        if (ItemList[k].Cannot_Require == null)
                //        {
                //            ItemList[k].Cannot_Require = new List<int>();
                //        };
                //        ItemList[k].Cannot_Require.Add(CurrentItem);
                //    };
                //};
            };
        }

        private void AddConditionals(int target, int currentItem, int d)
        {
            List<List<int>> baseConditionals = ItemList[target].Conditionals;

            if (baseConditionals == null)
            {
                baseConditionals = new List<List<int>>();
            }

            ItemList[target].Conditionals = new List<List<int>>();
            foreach (List<int> conditions in ItemList[d].Conditionals)
            {
                if (!conditions.Contains(currentItem))
                {
                    List<List<int>> newConditional = new List<List<int>>();
                    if (baseConditionals.Count == 0)
                    {
                        newConditional.Add(conditions);
                    }
                    else
                    {
                        foreach (List<int> baseConditions in baseConditionals)
                        {
                            newConditional.Add(baseConditions.Concat(conditions).ToList());
                        }
                    }

                    ItemList[target].Conditionals.AddRange(newConditional);
                }
            }
        }

        private void CheckConditionals(int currentItem, int target, List<int> dependencyPath)
        {
            if (target == Items.MaskBlast)
            {
                if (!ItemUtils.IsTemporaryItem(currentItem))
                {
                    ItemList[target].DependsOnItems = null;
                }
            }

            ConditionsChecked.Add(target);
            UpdateConditionals(currentItem, target);

            if (!ItemList[target].HasDependencies)
            {
                return;
            }

            for (int i = 0; i < ItemList[target].DependsOnItems.Count; i++)
            {
                int dependency = ItemList[target].DependsOnItems[i];
                if (!ItemList[dependency].HasCannotRequireItems)
                {
                    ItemList[dependency].CannotRequireItems = new List<int>();
                }
                if (!ItemList[dependency].CannotRequireItems.Contains(currentItem))
                {
                    ItemList[dependency].CannotRequireItems.Add(currentItem);
                }
                if (ItemUtils.IsFakeItem(dependency) || ItemList[dependency].ReplacesAnotherItem)
                {
                    if (ItemList[dependency].ReplacesAnotherItem)
                    {
                        dependency = ItemList[dependency].ReplacesItemId;
                    }

                    if (!ConditionsChecked.Contains(dependency))
                    {
                        var childPath = dependencyPath.ToList();
                        childPath.Add(dependency);
                        CheckConditionals(currentItem, dependency, childPath);
                    }
                }
                else if (ItemList[currentItem].TimeNeeded != 0 && ItemUtils.IsTemporaryItem(dependency) && dependencyPath.Skip(1).All(p => ItemUtils.IsFakeItem(p) || ItemUtils.IsTemporaryItem(ItemList.Single(j => j.ReplacesItemId == p).ID)))
                {
                    ItemList[dependency].TimeNeeded &= ItemList[currentItem].TimeNeeded;
                }
            }

            ItemList[target].DependsOnItems.RemoveAll(u => u == -1);
        }

        private bool CheckMatch(int currentItem, int target)
        {
            if (ForbiddenPlacedAt.ContainsKey(currentItem)
                && ForbiddenPlacedAt[currentItem].Contains(target))
            {
                Debug.WriteLine($"{currentItem} forbidden from being placed at {target}");
                return false;
            }

            if (ForbiddenReplacedBy.ContainsKey(target) && ForbiddenReplacedBy[target].Contains(currentItem))
            {
                Debug.WriteLine($"{target} forbids being replaced by {currentItem}");
                return false;
            }

            if (ItemUtils.IsTemporaryItem(currentItem) && ItemUtils.IsMoonItem(target))
            {
                Debug.WriteLine($"{currentItem} cannot be placed on the moon.");
                return false;
            }

            //check direct dependence
            ConditionRemoves = new List<int[]>();
            DependenceChecked = new Dictionary<int, Dependence> { { target, new Dependence { Type = DependenceType.Dependent } } };
            var dependencyPath = new List<int> { target };

            if (CheckDependence(currentItem, target, dependencyPath).Type != DependenceType.NotDependent)
            {
                return false;
            }

            //check conditional dependence
            RemoveConditionals(currentItem);
            ConditionsChecked = new List<int>();
            CheckConditionals(currentItem, target, dependencyPath);
            return true;
        }

        private void PlaceItem(int currentItem, List<int> targets)
        {
            if (ItemList[currentItem].ReplacesAnotherItem)
            {
                return;
            }

            var availableItems = targets.ToList();

            while (true)
            {
                if (availableItems.Count == 0)
                {
                    throw new Exception($"Unable to place {Items.ITEM_NAMES[currentItem]} anywhere.");
                }

                int targetItem = 0;
                if (currentItem > Items.SongOath && availableItems.Contains(0))
                {
                    targetItem = Random.Next(1, availableItems.Count);
                }
                else
                {
                    targetItem = Random.Next(availableItems.Count);
                }

                Debug.WriteLine($"----Attempting to place {Items.ITEM_NAMES[currentItem]} at {Items.ITEM_NAMES[availableItems[targetItem]]}.---");

                if (CheckMatch(currentItem, availableItems[targetItem]))
                {
                    ItemList[currentItem].ReplacesItemId = availableItems[targetItem];

                    Debug.WriteLine($"----Placed {Items.ITEM_NAMES[currentItem]} at {Items.ITEM_NAMES[ItemList[currentItem].ReplacesItemId]}----");

                    targets.Remove(availableItems[targetItem]);
                    return;
                }
                else
                {
                    Debug.WriteLine($"----Failed to place {Items.ITEM_NAMES[currentItem]} at {Items.ITEM_NAMES[availableItems[targetItem]]}----");
                    availableItems.RemoveAt(targetItem);
                }
            }
        }

        private void RandomizeItems()
        {
            if (_settings.UseCustomItemList)
            {
                SetupCustomItems();
            }
            else
            {
                Setup();
            }

            var itemPool = new List<int>();

            AddAllItems(itemPool);
            bool puzzle_output = true;
            if(puzzle_output)
            {
ItemList[Items.ShopItemGoronArrow10].ReplacesItemId = Items.HeartPieceNotebookHand;
ItemList[Items.ItemGreatBayCompass].ReplacesItemId = Items.ItemBottleAliens;
ItemList[Items.UpgradeBiggestBombBag].ReplacesItemId = Items.MaskAllNight;
ItemList[Items.ItemGreatBayMap].ReplacesItemId = Items.HeartPieceBank;
ItemList[Items.TradeItemLandDeed].ReplacesItemId = Items.ChestBeanGrottoRedRupee;
ItemList[Items.ItemBottleBeavers].ReplacesItemId = Items.ItemBottleBeavers;
ItemList[Items.MaskGiant].ReplacesItemId = Items.HeartPieceBeaverRace;
ItemList[Items.MaskBremen].ReplacesItemId = Items.HeartPieceZoraGrotto;
ItemList[Items.MaskTruth].ReplacesItemId = Items.MaskBlast;
ItemList[Items.ItemTingleMapTown].ReplacesItemId = Items.HeartPieceBoatArchery;
ItemList[Items.ItemTingleMapGreatBay].ReplacesItemId = Items.ItemBombBag;
ItemList[Items.ItemTingleMapRanch].ReplacesItemId = Items.ShopItemBombsBomb10;
ItemList[Items.ItemTingleMapSnowhead].ReplacesItemId = Items.ShopItemBombsBombchu10;
ItemList[Items.ItemTingleMapStoneTower].ReplacesItemId = Items.ChestBomberHideoutSilverRupee;
ItemList[Items.ItemTingleMapWoodfall].ReplacesItemId = Items.ItemNotebook;
ItemList[Items.BottleCatchBigPoe].ReplacesItemId = Items.BottleCatchBigPoe;
ItemList[Items.BottleCatchBug].ReplacesItemId = Items.BottleCatchBug;
ItemList[Items.BottleCatchEgg].ReplacesItemId = Items.BottleCatchPrincess;
ItemList[Items.BottleCatchFairy].ReplacesItemId = Items.BottleCatchFairy;
ItemList[Items.BottleCatchFish].ReplacesItemId = Items.BottleCatchFish;
ItemList[Items.BottleCatchHotSpringWater].ReplacesItemId = Items.BottleCatchHotSpringWater;
ItemList[Items.BottleCatchPoe].ReplacesItemId = Items.BottleCatchPoe;
ItemList[Items.BottleCatchSpringWater].ReplacesItemId = Items.BottleCatchSpringWater;
ItemList[Items.BottleCatchMushroom].ReplacesItemId = Items.BottleCatchEgg;
ItemList[Items.MaskCircusLeader].ReplacesItemId = Items.MaskBremen;
ItemList[Items.TradeItemKafeiLetter].ReplacesItemId = Items.MaskBunnyHood;
ItemList[Items.MaskKamaro].ReplacesItemId = Items.MaskCaptainHat;
ItemList[Items.MaskBunnyHood].ReplacesItemId = Items.ItemBottleMadameAroma;
ItemList[Items.ItemNotebook].ReplacesItemId = Items.MaskCircusLeader;
ItemList[Items.ItemIceArrow].ReplacesItemId = Items.MaskCouple;
ItemList[Items.SongEpona].ReplacesItemId = Items.ItemBottleDampe;
ItemList[Items.HeartPieceNotebookHand].ReplacesItemId = Items.MaskDeku;
ItemList[Items.TradeItemMamaLetter].ReplacesItemId = Items.HeartPieceDekuPalace;
ItemList[Items.ItemMagicBean].ReplacesItemId = Items.HeartPieceDekuPlayground;
ItemList[Items.ItemBombBag].ReplacesItemId = Items.HeartPieceDekuTrial;
ItemList[Items.TradeItemMoonTear].ReplacesItemId = Items.HeartPieceDodong;
ItemList[Items.TradeItemMountainDeed].ReplacesItemId = Items.HeartPieceDogRace;
ItemList[Items.TradeItemOceanDeed].ReplacesItemId = Items.ChestDogRacePurpleRupee;
ItemList[Items.PreClocktownDekuNuts10].ReplacesItemId = Items.MaskDonGero;
ItemList[Items.ItemSnowheadCompass].ReplacesItemId = Items.ChestEastClockTownSilverRupee;
ItemList[Items.SongNewWaveBossaNova].ReplacesItemId = Items.SongElegy;
ItemList[Items.ItemPictobox].ReplacesItemId = Items.SongEpona;
ItemList[Items.MaskGoron].ReplacesItemId = Items.HeartPieceEvan;
ItemList[Items.ShopItemGoronBomb10].ReplacesItemId = Items.MaskFierceDeity;
ItemList[Items.MaskStone].ReplacesItemId = Items.ItemFireArrow;
ItemList[Items.UpgradeBigBombBag].ReplacesItemId = Items.HeartPieceFishermanGame;
ItemList[Items.SongOath].ReplacesItemId = Items.HeartPieceChoir;
ItemList[Items.ItemSnowheadMap].ReplacesItemId = Items.MaskGaro;
ItemList[Items.ItemGoldDust].ReplacesItemId = Items.MaskGiant;
ItemList[Items.TradeItemRoomKey].ReplacesItemId = Items.MaskGibdo;
ItemList[Items.SongStorms].ReplacesItemId = Items.UpgradeGildedSword;
ItemList[Items.ItemLens].ReplacesItemId = Items.HeartContainerSnowhead;
ItemList[Items.MaskRomani].ReplacesItemId = Items.ItemGoldDust;
ItemList[Items.ItemStoneTowerBossKey].ReplacesItemId = Items.SongLullaby;
ItemList[Items.SongElegy].ReplacesItemId = Items.MaskGoron;
ItemList[Items.ItemStoneTowerCompass].ReplacesItemId = Items.ChestToGoronRaceGrotto;
ItemList[Items.ChestInvertedStoneTowerBean].ReplacesItemId = Items.ShopItemGoronArrow10;
ItemList[Items.ItemStoneTowerMap].ReplacesItemId = Items.ShopItemGoronBomb10;
ItemList[Items.TradeItemSwampDeed].ReplacesItemId = Items.ShopItemGoronRedPotion;
ItemList[Items.ShopItemTradingPostShield].ReplacesItemId = Items.HeartPieceGoronTrial;
ItemList[Items.ShopItemTradingPostArrow30].ReplacesItemId = Items.HeartPieceGoronVillageScrub;
ItemList[Items.SongLullaby].ReplacesItemId = Items.HeartPieceNotebookGran2;
ItemList[Items.ShopItemZoraShield].ReplacesItemId = Items.HeartPieceNotebookGran1;
ItemList[Items.ShopItemTradingPostArrow50].ReplacesItemId = Items.ChestBadBatsGrottoPurpleRupee;
ItemList[Items.ShopItemTradingPostNut10].ReplacesItemId = Items.ChestGraveyardGrotto;
ItemList[Items.ShopItemTradingPostStick].ReplacesItemId = Items.HeartPieceKnuckle;
ItemList[Items.MaskCouple].ReplacesItemId = Items.ItemGreatBayBossKey;
ItemList[Items.TwinmoldTrialArrows30].ReplacesItemId = Items.ChestGreatBayCapeGrotto;
ItemList[Items.ItemWoodfallCompass].ReplacesItemId = Items.ChestGreatBayCapeLedge1;
ItemList[Items.ItemWoodfallMap].ReplacesItemId = Items.ChestGreatBayCapeLedge2;
ItemList[Items.ItemBottleAliens].ReplacesItemId = Items.ChestGreatBayCapeUnderwater;
ItemList[Items.ShopItemZoraArrow10].ReplacesItemId = Items.ChestGreatBayCoastGrotto;
ItemList[Items.ChestBeanGrottoRedRupee].ReplacesItemId = Items.HeartPieceGreatBayCoast;
ItemList[Items.ShopItemWitchBluePotion].ReplacesItemId = Items.ItemGreatBayCompass;
ItemList[Items.ShopItemTradingPostRedPotion].ReplacesItemId = Items.ItemGreatBayKey1;
ItemList[Items.ShopItemTradingPostFairy].ReplacesItemId = Items.HeartPieceGreatBayCapeLikeLike;
ItemList[Items.ItemBottleMadameAroma].ReplacesItemId = Items.ItemGreatBayMap;
ItemList[Items.HeartPieceBank].ReplacesItemId = Items.MaskGreatFairy;
ItemList[Items.ShopItemWitchRedPotion].ReplacesItemId = Items.ItemFairySword;
ItemList[Items.ShopItemZoraRedPotion].ReplacesItemId = Items.HeartContainerGreatBay;
ItemList[Items.MaskCaptainHat].ReplacesItemId = Items.ItemBow;
ItemList[Items.ItemSnowheadBossKey].ReplacesItemId = Items.HeartPieceHoneyAndDarling;
ItemList[Items.HeartPieceBeaverRace].ReplacesItemId = Items.ItemHookshot;
ItemList[Items.HeartPieceZoraGrotto].ReplacesItemId = Items.ChestHotSpringGrottoRedRupee;
ItemList[Items.ItemPowderKeg].ReplacesItemId = Items.ItemIceArrow;
ItemList[Items.ItemStoneTowerKey1].ReplacesItemId = Items.HeartPieceCastle;
ItemList[Items.ItemStoneTowerKey2].ReplacesItemId = Items.ChestIkanaGrottoRecoveryHeart;
ItemList[Items.ItemStoneTowerKey3].ReplacesItemId = Items.HeartPieceIkana;
ItemList[Items.ItemStoneTowerKey4].ReplacesItemId = Items.ChestInnGuestRoom;
ItemList[Items.ItemSnowheadKey1].ReplacesItemId = Items.ChestInnStaffRoom;
ItemList[Items.MaskKafei].ReplacesItemId = Items.MaskKafei;
ItemList[Items.ItemSnowheadKey2].ReplacesItemId = Items.MaskKamaro;
ItemList[Items.DekuPrincess].ReplacesItemId = Items.MaskKeaton;
ItemList[Items.ItemBottleWitch].ReplacesItemId = Items.HeartPieceKeatonQuiz;
ItemList[Items.ItemSnowheadKey3].ReplacesItemId = Items.HeartPieceLabFish;
ItemList[Items.HeartPieceBoatArchery].ReplacesItemId = Items.TradeItemLandDeed;
ItemList[Items.HeartPieceDekuPalace].ReplacesItemId = Items.ChestLensCaveRedRupee;
ItemList[Items.MaskBlast].ReplacesItemId = Items.ChestLensCavePurpleRupee;
ItemList[Items.HeartPieceDekuPlayground].ReplacesItemId = Items.ItemLens;
ItemList[Items.TradeItemPendant].ReplacesItemId = Items.TradeItemKafeiLetter;
ItemList[Items.HeartPieceDekuTrial].ReplacesItemId = Items.TradeItemMamaLetter;
ItemList[Items.HeartPieceDodong].ReplacesItemId = Items.ItemLightArrow;
ItemList[Items.ItemBow].ReplacesItemId = Items.HeartPieceLinkTrial;
ItemList[Items.HeartPieceDogRace].ReplacesItemId = Items.ItemMagicBean;
ItemList[Items.HeartPieceEvan].ReplacesItemId = Items.ItemTingleMapTown;
ItemList[Items.HeartPieceFishermanGame].ReplacesItemId = Items.ItemTingleMapGreatBay;
ItemList[Items.HeartPieceChoir].ReplacesItemId = Items.ItemTingleMapRanch;
ItemList[Items.HeartContainerSnowhead].ReplacesItemId = Items.ItemTingleMapSnowhead;
ItemList[Items.HeartPieceGoronTrial].ReplacesItemId = Items.ItemTingleMapStoneTower;
ItemList[Items.HeartPieceGoronVillageScrub].ReplacesItemId = Items.ItemTingleMapWoodfall;
ItemList[Items.MaskDeku].ReplacesItemId = Items.MaskScents;
ItemList[Items.HeartPieceNotebookGran2].ReplacesItemId = Items.MaskTruth;
ItemList[Items.HeartPieceNotebookGran1].ReplacesItemId = Items.HeartPieceNotebookMayor;
ItemList[Items.MaskScents].ReplacesItemId = Items.UpgradeMirrorShield;
ItemList[Items.HeartPieceKnuckle].ReplacesItemId = Items.TradeItemMoonTear;
ItemList[Items.ItemWoodfallKey1].ReplacesItemId = Items.UpgradeBiggestBombBag;
ItemList[Items.HeartPieceGreatBayCoast].ReplacesItemId = Items.TradeItemMountainDeed;
ItemList[Items.ItemGreatBayKey1].ReplacesItemId = Items.ChestMountainVillage;
ItemList[Items.MaskGaro].ReplacesItemId = Items.ChestMountainVillageGrottoBottle;
ItemList[Items.HeartPieceGreatBayCapeLikeLike].ReplacesItemId = Items.ChestWoodsGrotto;
ItemList[Items.SongSonata].ReplacesItemId = Items.SongNewWaveBossaNova;
ItemList[Items.HeartContainerGreatBay].ReplacesItemId = Items.HeartPieceNorthClockTown;
ItemList[Items.ShopItemGoronRedPotion].ReplacesItemId = Items.SongOath;
ItemList[Items.HeartPieceHoneyAndDarling].ReplacesItemId = Items.HeartPieceOceanSpiderHouse;
ItemList[Items.HeartPieceCastle].ReplacesItemId = Items.TradeItemOceanDeed;
ItemList[Items.HeartPieceIkana].ReplacesItemId = Items.UpgradeGiantWallet;
ItemList[Items.ItemLightArrow].ReplacesItemId = Items.HeartContainerWoodfall;
ItemList[Items.HeartPieceKeatonQuiz].ReplacesItemId = Items.ChestToIkanaRedRupee;
ItemList[Items.HeartPieceLabFish].ReplacesItemId = Items.ChestToIkanaGrotto;
ItemList[Items.HeartPieceLinkTrial].ReplacesItemId = Items.ChestToSnowheadGrotto;
ItemList[Items.HeartPieceNotebookMayor].ReplacesItemId = Items.HeartPieceToSnowhead;
ItemList[Items.HeartPieceNorthClockTown].ReplacesItemId = Items.ChestToSwampGrotto;
ItemList[Items.HeartPieceOceanSpiderHouse].ReplacesItemId = Items.HeartPieceToSwamp;
ItemList[Items.HeartContainerWoodfall].ReplacesItemId = Items.HeartPiecePeahat;
ItemList[Items.HeartPieceToSnowhead].ReplacesItemId = Items.TradeItemPendant;
ItemList[Items.HeartPieceToSwamp].ReplacesItemId = Items.ChestPiratesFortressRedRupee1;
ItemList[Items.HeartPiecePeahat].ReplacesItemId = Items.ChestPiratesFortressRedRupee2;
ItemList[Items.HeartPiecePictobox].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee1;
ItemList[Items.HeartPiecePiratesFortress].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee2;
ItemList[Items.HeartPiecePoeHut].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee3;
ItemList[Items.HeartPiecePostBox].ReplacesItemId = Items.ChestInsidePiratesFortressGuardSilverRupee;
ItemList[Items.HeartPieceNotebookPostman].ReplacesItemId = Items.ChestInsidePiratesFortressHeartPieceRoomRedRupee;
ItemList[Items.HeartPieceNotebookRosa].ReplacesItemId = Items.ChestInsidePiratesFortressHeartPieceRoomBlueRupee;
ItemList[Items.HeartPieceSeaHorse].ReplacesItemId = Items.ChestInsidePiratesFortressMazeRedRupee;
ItemList[Items.ChestSecretShrineHeartPiece].ReplacesItemId = Items.ChestInsidePiratesFortressTankRedRupee;
ItemList[Items.HeartPieceSwampArchery].ReplacesItemId = Items.ItemPictobox;
ItemList[Items.HeartPieceSwampScrub].ReplacesItemId = Items.HeartPiecePictobox;
ItemList[Items.HeartPieceSwordsmanSchool].ReplacesItemId = Items.HeartPiecePiratesFortress;
ItemList[Items.HeartPieceTerminaBusinessScrub].ReplacesItemId = Items.HeartPiecePoeHut;
ItemList[Items.HeartPieceTerminaGossipStones].ReplacesItemId = Items.HeartPiecePostBox;
ItemList[Items.HeartPieceTownArchery].ReplacesItemId = Items.HeartPieceNotebookPostman;
ItemList[Items.HeartPieceTreasureChestGame].ReplacesItemId = Items.MaskPostmanHat;
ItemList[Items.HeartPieceTwinIslandsChest].ReplacesItemId = Items.ItemPowderKeg;
ItemList[Items.HeartContainerStoneTower].ReplacesItemId = Items.ChestPinacleRockRedRupee1;
ItemList[Items.HeartPieceWoodFallChest].ReplacesItemId = Items.ChestPinacleRockRedRupee2;
ItemList[Items.MaskKeaton].ReplacesItemId = Items.PreClocktownDekuNuts10;
ItemList[Items.UpgradeAdultWallet].ReplacesItemId = Items.UpgradeRazorSword;
ItemList[Items.HeartPieceZoraHallScrub].ReplacesItemId = Items.MaskRomani;
ItemList[Items.HeartPieceZoraTrial].ReplacesItemId = Items.TradeItemRoomKey;
ItemList[Items.ChestBomberHideoutSilverRupee].ReplacesItemId = Items.HeartPieceNotebookRosa;
ItemList[Items.ChestDogRacePurpleRupee].ReplacesItemId = Items.HeartPieceSeaHorse;
ItemList[Items.ChestTerminaGrottoBombchu].ReplacesItemId = Items.ChestSecretShrineDinoGrotto;
ItemList[Items.ChestEastClockTownSilverRupee].ReplacesItemId = Items.ChestSecretShrineGaroGrotto;
ItemList[Items.TwinmoldTrialBombchu10].ReplacesItemId = Items.ChestSecretShrineHeartPiece;
ItemList[Items.ChestBadBatsGrottoPurpleRupee].ReplacesItemId = Items.ChestSecretShrineWartGrotto;
ItemList[Items.MaskGreatFairy].ReplacesItemId = Items.ChestSecretShrineWizzGrotto;
ItemList[Items.ItemGreatBayBossKey].ReplacesItemId = Items.ItemSnowheadBossKey;
ItemList[Items.ChestHotSpringGrottoRedRupee].ReplacesItemId = Items.ItemSnowheadCompass;
ItemList[Items.ChestInnGuestRoom].ReplacesItemId = Items.ItemSnowheadKey1;
ItemList[Items.ChestInnStaffRoom].ReplacesItemId = Items.ItemSnowheadKey2;
ItemList[Items.ChestLensCaveRedRupee].ReplacesItemId = Items.ItemSnowheadKey3;
ItemList[Items.ChestLensCavePurpleRupee].ReplacesItemId = Items.ItemSnowheadMap;
ItemList[Items.ChestMountainVillage].ReplacesItemId = Items.SongSonata;
ItemList[Items.ChestWoodsGrotto].ReplacesItemId = Items.SongSoaring;
ItemList[Items.ChestToIkanaRedRupee].ReplacesItemId = Items.SongStorms;
ItemList[Items.ChestToIkanaGrotto].ReplacesItemId = Items.ChestSouthClockTownRedRupee;
ItemList[Items.ChestToSnowheadGrotto].ReplacesItemId = Items.ChestSouthClockTownPurpleRupee;
ItemList[Items.ChestToSwampGrotto].ReplacesItemId = Items.HeartPieceSouthClockTown;
ItemList[Items.MaskFierceDeity].ReplacesItemId = Items.MaskStone;
ItemList[Items.ShopItemWitchGreenPotion].ReplacesItemId = Items.ChestInvertedStoneTowerSilverRupee;
ItemList[Items.SongSoaring].ReplacesItemId = Items.ChestInvertedStoneTowerBombchu10;
ItemList[Items.MaskDonGero].ReplacesItemId = Items.ItemStoneTowerBossKey;
ItemList[Items.ShopItemBombsBombchu10].ReplacesItemId = Items.ItemStoneTowerCompass;
ItemList[Items.ChestToGoronRaceGrotto].ReplacesItemId = Items.ItemStoneTowerKey1;
ItemList[Items.ChestGraveyardGrotto].ReplacesItemId = Items.ItemStoneTowerKey2;
ItemList[Items.ChestGreatBayCapeGrotto].ReplacesItemId = Items.ItemStoneTowerKey3;
ItemList[Items.ChestIkanaGrottoRecoveryHeart].ReplacesItemId = Items.ItemStoneTowerKey4;
ItemList[Items.ShopItemTradingPostGreenPotion].ReplacesItemId = Items.ChestInvertedStoneTowerBean;
ItemList[Items.ChestInvertedStoneTowerBombchu10].ReplacesItemId = Items.ItemStoneTowerMap;
ItemList[Items.UpgradeBiggestQuiver].ReplacesItemId = Items.HeartPieceSwampArchery;
ItemList[Items.UpgradeBigQuiver].ReplacesItemId = Items.UpgradeBiggestQuiver;
ItemList[Items.ChestSecretShrineGaroGrotto].ReplacesItemId = Items.ChestSwampGrotto;
ItemList[Items.MaskPostmanHat].ReplacesItemId = Items.HeartPieceSwampScrub;
ItemList[Items.ChestSecretShrineWizzGrotto].ReplacesItemId = Items.TradeItemSwampDeed;
ItemList[Items.ChestSouthClockTownRedRupee].ReplacesItemId = Items.HeartPieceSwordsmanSchool;
ItemList[Items.ChestSouthClockTownPurpleRupee].ReplacesItemId = Items.ChestTerminaGrottoRedRupee;
ItemList[Items.HeartPieceSouthClockTown].ReplacesItemId = Items.ChestTerminaGrottoBombchu;
ItemList[Items.ChestInvertedStoneTowerSilverRupee].ReplacesItemId = Items.HeartPieceTerminaBusinessScrub;
ItemList[Items.ChestSwampGrotto].ReplacesItemId = Items.HeartPieceTerminaGossipStones;
ItemList[Items.ChestTerminaGrottoRedRupee].ReplacesItemId = Items.ChestTerminaGrassRedRupee;
ItemList[Items.ChestTerminaGrassRedRupee].ReplacesItemId = Items.ChestTerminaStumpRedRupee;
ItemList[Items.MaskZora].ReplacesItemId = Items.ChestTerminaUnderwaterRedRupee;
ItemList[Items.ItemHookshot].ReplacesItemId = Items.HeartPieceTownArchery;
ItemList[Items.ItemFairySword].ReplacesItemId = Items.UpgradeBigQuiver;
ItemList[Items.ChestWellLeftPurpleRupee].ReplacesItemId = Items.UpgradeBigBombBag;
ItemList[Items.ChestWellRightPurpleRupee].ReplacesItemId = Items.UpgradeAdultWallet;
ItemList[Items.ChestWoodfallRedRupee].ReplacesItemId = Items.ShopItemTradingPostArrow30;
ItemList[Items.ChestWoodfallBlueRupee].ReplacesItemId = Items.ShopItemTradingPostArrow50;
ItemList[Items.ChestInsidePiratesFortressMazeRedRupee].ReplacesItemId = Items.ShopItemTradingPostFairy;
ItemList[Items.ChestTerminaStumpRedRupee].ReplacesItemId = Items.ShopItemTradingPostGreenPotion;
ItemList[Items.ChestTerminaUnderwaterRedRupee].ReplacesItemId = Items.ShopItemTradingPostNut10;
ItemList[Items.ChestToGoronVillageRedRupee].ReplacesItemId = Items.ShopItemTradingPostRedPotion;
ItemList[Items.ChestPinacleRockRedRupee1].ReplacesItemId = Items.ShopItemTradingPostShield;
ItemList[Items.ChestPinacleRockRedRupee2].ReplacesItemId = Items.ShopItemTradingPostStick;
ItemList[Items.ChestSecretShrineDinoGrotto].ReplacesItemId = Items.HeartPieceTreasureChestGame;
ItemList[Items.ShopItemBombsBomb10].ReplacesItemId = Items.ChestToGoronVillageRedRupee;
ItemList[Items.ItemFireArrow].ReplacesItemId = Items.HeartPieceTwinIslandsChest;
ItemList[Items.UpgradeMirrorShield].ReplacesItemId = Items.HeartContainerStoneTower;
ItemList[Items.UpgradeGiantWallet].ReplacesItemId = Items.TwinmoldTrialArrows30;
ItemList[Items.MaskGibdo].ReplacesItemId = Items.TwinmoldTrialBombchu10;
ItemList[Items.UpgradeGildedSword].ReplacesItemId = Items.ChestWellLeftPurpleRupee;
ItemList[Items.UpgradeRazorSword].ReplacesItemId = Items.ChestWellRightPurpleRupee;
ItemList[Items.ChestPiratesFortressRedRupee1].ReplacesItemId = Items.ItemBottleWitch;
ItemList[Items.ChestPiratesFortressRedRupee2].ReplacesItemId = Items.ShopItemWitchBluePotion;
ItemList[Items.ChestPiratesFortressEntranceRedRupee1].ReplacesItemId = Items.ShopItemWitchGreenPotion;
ItemList[Items.ChestPiratesFortressEntranceRedRupee2].ReplacesItemId = Items.ShopItemWitchRedPotion;
ItemList[Items.ChestPiratesFortressEntranceRedRupee3].ReplacesItemId = Items.ChestWoodfallRedRupee;
ItemList[Items.ChestInsidePiratesFortressGuardSilverRupee].ReplacesItemId = Items.ChestWoodfallBlueRupee;
ItemList[Items.ChestInsidePiratesFortressHeartPieceRoomRedRupee].ReplacesItemId = Items.ItemWoodfallBossKey;
ItemList[Items.ChestInsidePiratesFortressHeartPieceRoomBlueRupee].ReplacesItemId = Items.HeartPieceWoodFallChest;
ItemList[Items.ItemWoodfallBossKey].ReplacesItemId = Items.ItemWoodfallCompass;
ItemList[Items.ChestGreatBayCoastGrotto].ReplacesItemId = Items.ItemWoodfallKey1;
ItemList[Items.ItemBottleDampe].ReplacesItemId = Items.ItemWoodfallMap;
ItemList[Items.ChestInsidePiratesFortressTankRedRupee].ReplacesItemId = Items.HeartPieceZoraHallScrub;
ItemList[Items.ChestSecretShrineWartGrotto].ReplacesItemId = Items.MaskZora;
ItemList[Items.ChestGreatBayCapeLedge1].ReplacesItemId = Items.ShopItemZoraArrow10;
ItemList[Items.ChestGreatBayCapeLedge2].ReplacesItemId = Items.ShopItemZoraRedPotion;
ItemList[Items.ChestGreatBayCapeUnderwater].ReplacesItemId = Items.ShopItemZoraShield;
ItemList[Items.MaskAllNight].ReplacesItemId = Items.HeartPieceZoraTrial;
            }
            // test mapping
            else
            {
ItemList[Items.HeartPieceNotebookHand].ReplacesItemId = Items.HeartPieceNotebookHand;
ItemList[Items.ItemBottleAliens].ReplacesItemId = Items.ItemBottleAliens;
ItemList[Items.MaskAllNight].ReplacesItemId = Items.MaskAllNight;
ItemList[Items.HeartPieceBank].ReplacesItemId = Items.HeartPieceBank;
ItemList[Items.ChestBeanGrottoRedRupee].ReplacesItemId = Items.ChestBeanGrottoRedRupee;
ItemList[Items.ItemBottleBeavers].ReplacesItemId = Items.ItemBottleBeavers;
ItemList[Items.HeartPieceBeaverRace].ReplacesItemId = Items.HeartPieceBeaverRace;
ItemList[Items.HeartPieceZoraGrotto].ReplacesItemId = Items.HeartPieceZoraGrotto;
ItemList[Items.MaskBlast].ReplacesItemId = Items.MaskBlast;
ItemList[Items.HeartPieceBoatArchery].ReplacesItemId = Items.HeartPieceBoatArchery;
ItemList[Items.ItemBombBag].ReplacesItemId = Items.ItemBombBag;
ItemList[Items.ShopItemBombsBomb10].ReplacesItemId = Items.ShopItemBombsBomb10;
ItemList[Items.ShopItemBombsBombchu10].ReplacesItemId = Items.ShopItemBombsBombchu10;
ItemList[Items.ChestBomberHideoutSilverRupee].ReplacesItemId = Items.ChestBomberHideoutSilverRupee;
ItemList[Items.ItemNotebook].ReplacesItemId = Items.ItemNotebook;
ItemList[Items.BottleCatchBigPoe].ReplacesItemId = Items.BottleCatchBigPoe;
ItemList[Items.BottleCatchBug].ReplacesItemId = Items.BottleCatchBug;
//ItemList[Items.BottleCatchPrincess].ReplacesItemId = Items.BottleCatchPrincess;
ItemList[Items.BottleCatchFairy].ReplacesItemId = Items.BottleCatchFairy;
ItemList[Items.BottleCatchFish].ReplacesItemId = Items.BottleCatchFish;
ItemList[Items.BottleCatchHotSpringWater].ReplacesItemId = Items.BottleCatchHotSpringWater;
ItemList[Items.BottleCatchMushroom].ReplacesItemId = Items.BottleCatchMushroom;
ItemList[Items.BottleCatchPoe].ReplacesItemId = Items.BottleCatchPoe;
ItemList[Items.BottleCatchSpringWater].ReplacesItemId = Items.BottleCatchSpringWater;
ItemList[Items.BottleCatchEgg].ReplacesItemId = Items.BottleCatchEgg;
ItemList[Items.MaskBremen].ReplacesItemId = Items.MaskBremen;
ItemList[Items.MaskBunnyHood].ReplacesItemId = Items.MaskBunnyHood;
ItemList[Items.MaskCaptainHat].ReplacesItemId = Items.MaskCaptainHat;
ItemList[Items.ItemBottleMadameAroma].ReplacesItemId = Items.ItemBottleMadameAroma;
ItemList[Items.MaskCircusLeader].ReplacesItemId = Items.MaskCircusLeader;
ItemList[Items.MaskCouple].ReplacesItemId = Items.MaskCouple;
ItemList[Items.ItemBottleDampe].ReplacesItemId = Items.ItemBottleDampe;
ItemList[Items.MaskDeku].ReplacesItemId = Items.MaskDeku;
ItemList[Items.HeartPieceDekuPalace].ReplacesItemId = Items.HeartPieceDekuPalace;
ItemList[Items.HeartPieceDekuPlayground].ReplacesItemId = Items.HeartPieceDekuPlayground;
ItemList[Items.HeartPieceDodong].ReplacesItemId = Items.HeartPieceDodong;
ItemList[Items.HeartPieceDogRace].ReplacesItemId = Items.HeartPieceDogRace;
ItemList[Items.ChestDogRacePurpleRupee].ReplacesItemId = Items.ChestDogRacePurpleRupee;
ItemList[Items.MaskDonGero].ReplacesItemId = Items.MaskDonGero;
ItemList[Items.ChestEastClockTownSilverRupee].ReplacesItemId = Items.ChestEastClockTownSilverRupee;
ItemList[Items.SongElegy].ReplacesItemId = Items.SongElegy;
ItemList[Items.SongEpona].ReplacesItemId = Items.SongEpona;
ItemList[Items.HeartPieceEvan].ReplacesItemId = Items.HeartPieceEvan;
ItemList[Items.ChestTerminaUnderwaterRedRupee].ReplacesItemId = Items.ItemFireArrow;
ItemList[Items.HeartPieceFishermanGame].ReplacesItemId = Items.HeartPieceFishermanGame;
ItemList[Items.HeartPieceChoir].ReplacesItemId = Items.HeartPieceChoir;
ItemList[Items.MaskGaro].ReplacesItemId = Items.MaskGaro;
ItemList[Items.MaskGiant].ReplacesItemId = Items.MaskGiant;
ItemList[Items.MaskGibdo].ReplacesItemId = Items.MaskGibdo;
ItemList[Items.UpgradeGildedSword].ReplacesItemId = Items.UpgradeGildedSword;
ItemList[Items.HeartContainerSnowhead].ReplacesItemId = Items.HeartContainerSnowhead;
ItemList[Items.ItemGoldDust].ReplacesItemId = Items.ItemGoldDust;
ItemList[Items.SongLullaby].ReplacesItemId = Items.SongLullaby;
ItemList[Items.ChestTerminaStumpRedRupee].ReplacesItemId = Items.MaskGoron;
ItemList[Items.ChestToGoronRaceGrotto].ReplacesItemId = Items.ChestToGoronRaceGrotto;
ItemList[Items.ShopItemGoronArrow10].ReplacesItemId = Items.ShopItemGoronArrow10;
ItemList[Items.ShopItemGoronBomb10].ReplacesItemId = Items.ShopItemGoronBomb10;
ItemList[Items.ShopItemGoronRedPotion].ReplacesItemId = Items.ShopItemGoronRedPotion;
ItemList[Items.HeartPieceGoronVillageScrub].ReplacesItemId = Items.HeartPieceGoronVillageScrub;
ItemList[Items.HeartPieceNotebookGran2].ReplacesItemId = Items.HeartPieceNotebookGran2;
ItemList[Items.HeartPieceNotebookGran1].ReplacesItemId = Items.HeartPieceNotebookGran1;
ItemList[Items.ChestBadBatsGrottoPurpleRupee].ReplacesItemId = Items.ChestBadBatsGrottoPurpleRupee;
ItemList[Items.ChestGraveyardGrotto].ReplacesItemId = Items.ChestGraveyardGrotto;
ItemList[Items.HeartPieceKnuckle].ReplacesItemId = Items.HeartPieceKnuckle;
ItemList[Items.ItemGreatBayBossKey].ReplacesItemId = Items.ItemGreatBayBossKey;
ItemList[Items.ChestGreatBayCapeGrotto].ReplacesItemId = Items.ChestGreatBayCapeGrotto;
ItemList[Items.ChestGreatBayCapeLedge1].ReplacesItemId = Items.ChestGreatBayCapeLedge1;
ItemList[Items.ChestGreatBayCapeLedge2].ReplacesItemId = Items.ChestGreatBayCapeLedge2;
ItemList[Items.ChestGreatBayCapeUnderwater].ReplacesItemId = Items.ChestGreatBayCapeUnderwater;
ItemList[Items.ChestGreatBayCoastGrotto].ReplacesItemId = Items.ChestGreatBayCoastGrotto;
ItemList[Items.HeartPieceGreatBayCoast].ReplacesItemId = Items.HeartPieceGreatBayCoast;
ItemList[Items.ItemGreatBayCompass].ReplacesItemId = Items.ItemGreatBayCompass;
ItemList[Items.ItemGreatBayKey1].ReplacesItemId = Items.ItemGreatBayKey1;
ItemList[Items.HeartPieceGreatBayCapeLikeLike].ReplacesItemId = Items.HeartPieceGreatBayCapeLikeLike;
ItemList[Items.ItemGreatBayMap].ReplacesItemId = Items.ItemGreatBayMap;
ItemList[Items.MaskGreatFairy].ReplacesItemId = Items.MaskGreatFairy;
ItemList[Items.ItemFairySword].ReplacesItemId = Items.ItemFairySword;
ItemList[Items.HeartContainerGreatBay].ReplacesItemId = Items.HeartContainerGreatBay;
ItemList[Items.ChestTerminaGrassRedRupee].ReplacesItemId = Items.ItemBow;
ItemList[Items.HeartPieceHoneyAndDarling].ReplacesItemId = Items.HeartPieceHoneyAndDarling;
ItemList[Items.ChestTerminaGrottoBombchu].ReplacesItemId = Items.ItemHookshot;
ItemList[Items.ChestHotSpringGrottoRedRupee].ReplacesItemId = Items.ChestHotSpringGrottoRedRupee;
ItemList[Items.ChestTerminaGrottoRedRupee].ReplacesItemId = Items.ItemIceArrow;
ItemList[Items.HeartPieceCastle].ReplacesItemId = Items.HeartPieceCastle;
ItemList[Items.ChestIkanaGrottoRecoveryHeart].ReplacesItemId = Items.ChestIkanaGrottoRecoveryHeart;
ItemList[Items.HeartPieceIkana].ReplacesItemId = Items.HeartPieceIkana;
ItemList[Items.ChestInnGuestRoom].ReplacesItemId = Items.ChestInnGuestRoom;
ItemList[Items.ChestInnStaffRoom].ReplacesItemId = Items.ChestInnStaffRoom;
ItemList[Items.ItemLightArrow].ReplacesItemId = Items.MaskKafei;
ItemList[Items.MaskKamaro].ReplacesItemId = Items.MaskKamaro;
ItemList[Items.MaskKeaton].ReplacesItemId = Items.MaskKeaton;
ItemList[Items.HeartPieceKeatonQuiz].ReplacesItemId = Items.HeartPieceKeatonQuiz;
ItemList[Items.HeartPieceLabFish].ReplacesItemId = Items.HeartPieceLabFish;
ItemList[Items.TradeItemLandDeed].ReplacesItemId = Items.TradeItemLandDeed;
ItemList[Items.ChestLensCaveRedRupee].ReplacesItemId = Items.ChestLensCaveRedRupee;
ItemList[Items.ChestLensCavePurpleRupee].ReplacesItemId = Items.ChestLensCavePurpleRupee;
ItemList[Items.ItemLens].ReplacesItemId = Items.ItemLens;
ItemList[Items.TradeItemKafeiLetter].ReplacesItemId = Items.TradeItemKafeiLetter;
ItemList[Items.TradeItemMamaLetter].ReplacesItemId = Items.TradeItemMamaLetter;
ItemList[Items.MaskKafei].ReplacesItemId = Items.ItemLightArrow;
ItemList[Items.ItemMagicBean].ReplacesItemId = Items.ItemMagicBean;
ItemList[Items.ItemTingleMapTown].ReplacesItemId = Items.ItemTingleMapTown;
ItemList[Items.ItemTingleMapGreatBay].ReplacesItemId = Items.ItemTingleMapGreatBay;
ItemList[Items.ItemTingleMapRanch].ReplacesItemId = Items.ItemTingleMapRanch;
ItemList[Items.ItemTingleMapSnowhead].ReplacesItemId = Items.ItemTingleMapSnowhead;
ItemList[Items.ItemTingleMapStoneTower].ReplacesItemId = Items.ItemTingleMapStoneTower;
ItemList[Items.ItemTingleMapWoodfall].ReplacesItemId = Items.ItemTingleMapWoodfall;
ItemList[Items.MaskScents].ReplacesItemId = Items.MaskScents;
ItemList[Items.MaskTruth].ReplacesItemId = Items.MaskTruth;
ItemList[Items.HeartPieceNotebookMayor].ReplacesItemId = Items.HeartPieceNotebookMayor;
ItemList[Items.UpgradeMirrorShield].ReplacesItemId = Items.UpgradeMirrorShield;
ItemList[Items.TradeItemMoonTear].ReplacesItemId = Items.TradeItemMoonTear;
ItemList[Items.UpgradeBiggestBombBag].ReplacesItemId = Items.UpgradeBiggestBombBag;
ItemList[Items.TradeItemMountainDeed].ReplacesItemId = Items.TradeItemMountainDeed;
ItemList[Items.ChestMountainVillage].ReplacesItemId = Items.ChestMountainVillage;
ItemList[Items.ChestMountainVillageGrottoBottle].ReplacesItemId = Items.ChestMountainVillageGrottoBottle;
ItemList[Items.ChestWoodsGrotto].ReplacesItemId = Items.ChestWoodsGrotto;
ItemList[Items.SongNewWaveBossaNova].ReplacesItemId = Items.SongNewWaveBossaNova;
ItemList[Items.HeartPieceNorthClockTown].ReplacesItemId = Items.HeartPieceNorthClockTown;
ItemList[Items.SongOath].ReplacesItemId = Items.SongOath;
ItemList[Items.HeartPieceOceanSpiderHouse].ReplacesItemId = Items.HeartPieceOceanSpiderHouse;
ItemList[Items.TradeItemOceanDeed].ReplacesItemId = Items.TradeItemOceanDeed;
ItemList[Items.UpgradeGiantWallet].ReplacesItemId = Items.UpgradeGiantWallet;
ItemList[Items.HeartContainerWoodfall].ReplacesItemId = Items.HeartContainerWoodfall;
ItemList[Items.ChestToIkanaRedRupee].ReplacesItemId = Items.ChestToIkanaRedRupee;
ItemList[Items.ChestToIkanaGrotto].ReplacesItemId = Items.ChestToIkanaGrotto;
ItemList[Items.ChestToSnowheadGrotto].ReplacesItemId = Items.ChestToSnowheadGrotto;
ItemList[Items.HeartPieceToSnowhead].ReplacesItemId = Items.HeartPieceToSnowhead;
ItemList[Items.ChestToSwampGrotto].ReplacesItemId = Items.ChestToSwampGrotto;
ItemList[Items.HeartPieceToSwamp].ReplacesItemId = Items.HeartPieceToSwamp;
ItemList[Items.HeartPiecePeahat].ReplacesItemId = Items.HeartPiecePeahat;
ItemList[Items.TradeItemPendant].ReplacesItemId = Items.TradeItemPendant;
ItemList[Items.ChestPiratesFortressRedRupee1].ReplacesItemId = Items.ChestPiratesFortressRedRupee1;
ItemList[Items.ChestPiratesFortressRedRupee2].ReplacesItemId = Items.ChestPiratesFortressRedRupee2;
ItemList[Items.ChestPiratesFortressEntranceRedRupee1].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee1;
ItemList[Items.ChestPiratesFortressEntranceRedRupee2].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee2;
ItemList[Items.ChestPiratesFortressEntranceRedRupee3].ReplacesItemId = Items.ChestPiratesFortressEntranceRedRupee3;
ItemList[Items.ChestInsidePiratesFortressGuardSilverRupee].ReplacesItemId = Items.ChestInsidePiratesFortressGuardSilverRupee;
ItemList[Items.ChestInsidePiratesFortressHeartPieceRoomRedRupee].ReplacesItemId = Items.ChestInsidePiratesFortressHeartPieceRoomRedRupee;
ItemList[Items.ChestInsidePiratesFortressHeartPieceRoomBlueRupee].ReplacesItemId = Items.ChestInsidePiratesFortressHeartPieceRoomBlueRupee;
ItemList[Items.ChestInsidePiratesFortressMazeRedRupee].ReplacesItemId = Items.ChestInsidePiratesFortressMazeRedRupee;
ItemList[Items.ChestInsidePiratesFortressTankRedRupee].ReplacesItemId = Items.ChestInsidePiratesFortressTankRedRupee;
ItemList[Items.ItemPictobox].ReplacesItemId = Items.ItemPictobox;
ItemList[Items.HeartPiecePictobox].ReplacesItemId = Items.HeartPiecePictobox;
ItemList[Items.HeartPiecePiratesFortress].ReplacesItemId = Items.HeartPiecePiratesFortress;
ItemList[Items.HeartPiecePoeHut].ReplacesItemId = Items.HeartPiecePoeHut;
ItemList[Items.HeartPiecePostBox].ReplacesItemId = Items.HeartPiecePostBox;
ItemList[Items.HeartPieceNotebookPostman].ReplacesItemId = Items.HeartPieceNotebookPostman;
ItemList[Items.MaskPostmanHat].ReplacesItemId = Items.MaskPostmanHat;
ItemList[Items.ItemPowderKeg].ReplacesItemId = Items.ItemPowderKeg;
ItemList[Items.ChestPinacleRockRedRupee1].ReplacesItemId = Items.ChestPinacleRockRedRupee1;
ItemList[Items.ChestPinacleRockRedRupee2].ReplacesItemId = Items.ChestPinacleRockRedRupee2;
ItemList[Items.HeartPieceSouthClockTown].ReplacesItemId = Items.UpgradeRazorSword;
ItemList[Items.MaskRomani].ReplacesItemId = Items.MaskRomani;
ItemList[Items.TradeItemRoomKey].ReplacesItemId = Items.TradeItemRoomKey;
ItemList[Items.HeartPieceNotebookRosa].ReplacesItemId = Items.HeartPieceNotebookRosa;
ItemList[Items.HeartPieceSeaHorse].ReplacesItemId = Items.HeartPieceSeaHorse;
ItemList[Items.ChestSecretShrineDinoGrotto].ReplacesItemId = Items.ChestSecretShrineDinoGrotto;
ItemList[Items.ChestSecretShrineGaroGrotto].ReplacesItemId = Items.ChestSecretShrineGaroGrotto;
ItemList[Items.ChestSecretShrineHeartPiece].ReplacesItemId = Items.ChestSecretShrineHeartPiece;
ItemList[Items.ChestSecretShrineWartGrotto].ReplacesItemId = Items.ChestSecretShrineWartGrotto;
ItemList[Items.ChestSecretShrineWizzGrotto].ReplacesItemId = Items.ChestSecretShrineWizzGrotto;
ItemList[Items.ItemSnowheadBossKey].ReplacesItemId = Items.ItemSnowheadBossKey;
ItemList[Items.ItemSnowheadCompass].ReplacesItemId = Items.ItemSnowheadCompass;
ItemList[Items.ItemSnowheadKey1].ReplacesItemId = Items.ItemSnowheadKey1;
ItemList[Items.ItemSnowheadKey2].ReplacesItemId = Items.ItemSnowheadKey2;
ItemList[Items.ItemSnowheadKey3].ReplacesItemId = Items.ItemSnowheadKey3;
ItemList[Items.ItemSnowheadMap].ReplacesItemId = Items.ItemSnowheadMap;
ItemList[Items.SongSonata].ReplacesItemId = Items.SongSonata;
ItemList[Items.SongSoaring].ReplacesItemId = Items.SongSoaring;
ItemList[Items.SongStorms].ReplacesItemId = Items.SongStorms;
ItemList[Items.MaskZora].ReplacesItemId = Items.ChestSouthClockTownRedRupee;
ItemList[Items.ChestSouthClockTownPurpleRupee].ReplacesItemId = Items.ChestSouthClockTownPurpleRupee;
ItemList[Items.MaskStone].ReplacesItemId = Items.MaskStone;
ItemList[Items.ChestInvertedStoneTowerSilverRupee].ReplacesItemId = Items.ChestInvertedStoneTowerSilverRupee;
ItemList[Items.ChestInvertedStoneTowerBombchu10].ReplacesItemId = Items.ChestInvertedStoneTowerBombchu10;
ItemList[Items.ItemStoneTowerBossKey].ReplacesItemId = Items.ItemStoneTowerBossKey;
ItemList[Items.ItemStoneTowerCompass].ReplacesItemId = Items.ItemStoneTowerCompass;
ItemList[Items.ItemStoneTowerKey1].ReplacesItemId = Items.ItemStoneTowerKey1;
ItemList[Items.ItemStoneTowerKey2].ReplacesItemId = Items.ItemStoneTowerKey2;
ItemList[Items.ItemStoneTowerKey3].ReplacesItemId = Items.ItemStoneTowerKey3;
ItemList[Items.ItemStoneTowerKey4].ReplacesItemId = Items.ItemStoneTowerKey4;
ItemList[Items.ChestInvertedStoneTowerBean].ReplacesItemId = Items.ChestInvertedStoneTowerBean;
ItemList[Items.ItemStoneTowerMap].ReplacesItemId = Items.ItemStoneTowerMap;
ItemList[Items.HeartPieceSwampArchery].ReplacesItemId = Items.HeartPieceSwampArchery;
ItemList[Items.UpgradeBiggestQuiver].ReplacesItemId = Items.UpgradeBiggestQuiver;
ItemList[Items.ChestSwampGrotto].ReplacesItemId = Items.ChestSwampGrotto;
ItemList[Items.HeartPieceSwampScrub].ReplacesItemId = Items.HeartPieceSwampScrub;
ItemList[Items.TradeItemSwampDeed].ReplacesItemId = Items.TradeItemSwampDeed;
ItemList[Items.HeartPieceSwordsmanSchool].ReplacesItemId = Items.HeartPieceSwordsmanSchool;
ItemList[Items.ItemFireArrow].ReplacesItemId = Items.ChestTerminaGrottoRedRupee;
ItemList[Items.MaskGoron].ReplacesItemId = Items.ChestTerminaGrottoBombchu;
ItemList[Items.HeartPieceTerminaBusinessScrub].ReplacesItemId = Items.HeartPieceTerminaBusinessScrub;
ItemList[Items.HeartPieceTerminaGossipStones].ReplacesItemId = Items.HeartPieceTerminaGossipStones;
ItemList[Items.ItemBow].ReplacesItemId = Items.ChestTerminaGrassRedRupee;
ItemList[Items.ItemHookshot].ReplacesItemId = Items.ChestTerminaStumpRedRupee;
ItemList[Items.ItemIceArrow].ReplacesItemId = Items.ChestTerminaUnderwaterRedRupee;
ItemList[Items.HeartPieceTownArchery].ReplacesItemId = Items.HeartPieceTownArchery;
ItemList[Items.UpgradeBigQuiver].ReplacesItemId = Items.UpgradeBigQuiver;
ItemList[Items.UpgradeBigBombBag].ReplacesItemId = Items.UpgradeBigBombBag;
ItemList[Items.UpgradeAdultWallet].ReplacesItemId = Items.UpgradeAdultWallet;
ItemList[Items.ShopItemTradingPostArrow30].ReplacesItemId = Items.ShopItemTradingPostArrow30;
ItemList[Items.ShopItemTradingPostArrow50].ReplacesItemId = Items.ShopItemTradingPostArrow50;
ItemList[Items.PreClocktownDekuNuts10].ReplacesItemId = Items.ShopItemTradingPostFairy;
ItemList[Items.TwinmoldTrialBombchu10].ReplacesItemId = Items.ShopItemTradingPostGreenPotion;
ItemList[Items.TwinmoldTrialArrows30].ReplacesItemId = Items.ShopItemTradingPostNut10;
ItemList[Items.ShopItemTradingPostRedPotion].ReplacesItemId = Items.ShopItemTradingPostRedPotion;
ItemList[Items.ShopItemTradingPostShield].ReplacesItemId = Items.ShopItemTradingPostShield;
ItemList[Items.ShopItemTradingPostStick].ReplacesItemId = Items.ShopItemTradingPostStick;
ItemList[Items.HeartPieceTreasureChestGame].ReplacesItemId = Items.HeartPieceTreasureChestGame;
ItemList[Items.ChestToGoronVillageRedRupee].ReplacesItemId = Items.ChestToGoronVillageRedRupee;
ItemList[Items.HeartPieceTwinIslandsChest].ReplacesItemId = Items.HeartPieceTwinIslandsChest;
ItemList[Items.HeartContainerStoneTower].ReplacesItemId = Items.HeartContainerStoneTower;
ItemList[Items.ChestWellLeftPurpleRupee].ReplacesItemId = Items.ChestWellLeftPurpleRupee;
ItemList[Items.ChestWellRightPurpleRupee].ReplacesItemId = Items.ChestWellRightPurpleRupee;
ItemList[Items.ItemBottleWitch].ReplacesItemId = Items.ItemBottleWitch;
ItemList[Items.ShopItemWitchBluePotion].ReplacesItemId = Items.ShopItemWitchBluePotion;
ItemList[Items.ShopItemWitchGreenPotion].ReplacesItemId = Items.ShopItemWitchGreenPotion;
ItemList[Items.ShopItemWitchRedPotion].ReplacesItemId = Items.ShopItemWitchRedPotion;
ItemList[Items.ChestWoodfallRedRupee].ReplacesItemId = Items.ChestWoodfallRedRupee;
ItemList[Items.ChestWoodfallBlueRupee].ReplacesItemId = Items.ChestWoodfallBlueRupee;
ItemList[Items.ItemWoodfallBossKey].ReplacesItemId = Items.ItemWoodfallBossKey;
ItemList[Items.HeartPieceWoodFallChest].ReplacesItemId = Items.HeartPieceWoodFallChest;
ItemList[Items.ItemWoodfallCompass].ReplacesItemId = Items.ItemWoodfallCompass;
ItemList[Items.ItemWoodfallKey1].ReplacesItemId = Items.ItemWoodfallKey1;
ItemList[Items.ItemWoodfallMap].ReplacesItemId = Items.ItemWoodfallMap;
ItemList[Items.HeartPieceZoraHallScrub].ReplacesItemId = Items.HeartPieceZoraHallScrub;
ItemList[Items.ChestSouthClockTownRedRupee].ReplacesItemId = Items.MaskZora;
ItemList[Items.ShopItemTradingPostFairy].ReplacesItemId = Items.ShopItemZoraArrow10;
ItemList[Items.ShopItemTradingPostGreenPotion].ReplacesItemId = Items.ShopItemZoraRedPotion;
ItemList[Items.ShopItemTradingPostNut10].ReplacesItemId = Items.ShopItemZoraShield;
ItemList[Items.HeartPieceDekuTrial].ReplacesItemId = Items.HeartPieceDekuTrial;
ItemList[Items.HeartPieceGoronTrial].ReplacesItemId = Items.HeartPieceGoronTrial;
ItemList[Items.HeartPieceZoraTrial].ReplacesItemId = Items.HeartPieceZoraTrial;
ItemList[Items.HeartPieceLinkTrial].ReplacesItemId = Items.HeartPieceLinkTrial;
ItemList[Items.MaskFierceDeity].ReplacesItemId = Items.MaskFierceDeity;
ItemList[Items.ShopItemZoraArrow10].ReplacesItemId = Items.PreClocktownDekuNuts10;
ItemList[Items.ShopItemZoraRedPotion].ReplacesItemId = Items.TwinmoldTrialBombchu10;
ItemList[Items.ShopItemZoraShield].ReplacesItemId = Items.TwinmoldTrialArrows30;
ItemList[Items.DekuPrincess].ReplacesItemId = Items.HeartPieceSouthClockTown;
//ItemList[Items.UpgradeRazorSword].ReplacesItemId = 
            }
            /*
            // Generate the random mapping
            PlaceQuestItems(itemPool);
            PlaceTradeItems(itemPool);
            PlaceDungeonItems(itemPool);
            PlaceFreeItem(itemPool);
            PlaceUpgrades(itemPool);
            PlaceSongs(itemPool);
            PlaceMasks(itemPool);
            PlaceRegularItems(itemPool);
            PlaceShopItems(itemPool);
            PlaceMoonItems(itemPool);
            PlaceHeartpieces(itemPool);
            PlaceOther(itemPool);
            PlaceTingleMaps(itemPool);
            */
            _randomized.ItemList = ItemList;
        }

        /// <summary>
        /// Places moon items in the randomization pool.
        /// </summary>
        private void PlaceMoonItems(List<int> itemPool)
        {
            for (int i = Items.HeartPieceDekuTrial; i <= Items.DekuPrincess; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places tingle maps in the randomization pool.
        /// </summary>
        private void PlaceTingleMaps(List<int> itemPool)
        {
            for (int i = Items.ItemTingleMapTown; i <= Items.ItemTingleMapStoneTower; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places other chests and grottos in the randomization pool.
        /// </summary>
        /// <param name="itemPool"></param>
        private void PlaceOther(List<int> itemPool)
        {
            for (int i = Items.ChestLensCaveRedRupee; i <= Items.ChestSouthClockTownPurpleRupee; i++)
            {
                PlaceItem(i, itemPool);
            }

            PlaceItem(Items.ChestToGoronRaceGrotto, itemPool);
        }

        /// <summary>
        /// Places heart pieces in the randomization pool. Includes rewards/chests, as well as standing heart pieces.
        /// </summary>
        private void PlaceHeartpieces(List<int> itemPool)
        {
            // Rewards/chests
            for (int i = Items.HeartPieceNotebookMayor; i <= Items.HeartPieceKnuckle; i++)
            {
                PlaceItem(i, itemPool);
            }

            // Bank reward
            PlaceItem(Items.HeartPieceBank, itemPool);

            // Standing heart pieces
            for (int i = Items.HeartPieceSouthClockTown; i <= Items.HeartContainerStoneTower; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places shop items in the randomization pool
        /// </summary>
        private void PlaceShopItems(List<int> itemPool)
        {
            for (int i = Items.ShopItemTradingPostRedPotion; i <= Items.ShopItemZoraRedPotion; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places dungeon items in the randomization pool
        /// </summary>
        private void PlaceDungeonItems(List<int> itemPool)
        {
            for (int i = Items.ItemWoodfallMap; i <= Items.ItemStoneTowerKey4; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places songs in the randomization pool
        /// </summary>
        private void PlaceSongs(List<int> itemPool)
        {
            for (int i = Items.SongSoaring; i <= Items.SongOath; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places masks in the randomization pool
        /// </summary>
        private void PlaceMasks(List<int> itemPool)
        {
            for (int i = Items.MaskPostmanHat; i <= Items.MaskZora; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places upgrade items in the randomization pool
        /// </summary>
        private void PlaceUpgrades(List<int> itemPool)
        {
            for (int i = Items.UpgradeRazorSword; i <= Items.UpgradeGiantWallet; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places regular items in the randomization pool
        /// </summary>
        private void PlaceRegularItems(List<int> itemPool)
        {
            for (int i = Items.MaskDeku; i <= Items.ItemNotebook; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Replace starting deku mask with free item if not already replaced.
        /// </summary>
        private void PlaceFreeItem(List<int> itemPool)
        {
            if (ItemList.FindIndex(item => item.ReplacesItemId == Items.MaskDeku) != -1)
            {
                return;
            }

            int freeItem = Random.Next(Items.SongOath + 1);
            if (ForbiddenReplacedBy.ContainsKey(Items.MaskDeku))
            {
                while (ItemList[freeItem].ReplacesItemId != -1
                    || ForbiddenReplacedBy[Items.MaskDeku].Contains(freeItem))
                {
                    freeItem = Random.Next(Items.SongOath + 1);
                }
            }
            ItemList[freeItem].ReplacesItemId = Items.MaskDeku;
            itemPool.Remove(Items.MaskDeku);
        }

        /// <summary>
        /// Adds all items into the randomization pool (excludes area/other and items that already have placement)
        /// </summary>
        private void AddAllItems(List<int> itemPool)
        {
            for (int i = 0; i < ItemList.Count; i++)
            {
                // Skip item if its in area and other, is out of range or has placement
                if ((ItemUtils.IsAreaOrOther(i)
                    || ItemUtils.IsOutOfRange(i))
                    || (ItemList[i].ReplacesAnotherItem))
                {
                    continue;
                }

                itemPool.Add(i);
            }
        }

        /// <summary>
        /// Places quest items in the randomization pool
        /// </summary>
        private void PlaceQuestItems(List<int> itemPool)
        {
            for (int i = Items.TradeItemRoomKey; i <= Items.TradeItemMamaLetter; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Places trade items in the randomization pool
        /// </summary>
        private void PlaceTradeItems(List<int> itemPool)
        {
            for (int i = Items.TradeItemMoonTear; i <= Items.TradeItemOceanDeed; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Adds items to randomization pool based on settings.
        /// </summary>
        private void Setup()
        {
            if (_settings.ExcludeSongOfSoaring)
            {
                ItemList[Items.SongSoaring].ReplacesItemId = Items.SongSoaring;
            }

            if (!_settings.AddSongs)
            {
                ShuffleSongs();
            }

            if (!_settings.AddDungeonItems)
            {
                PreserveDungeonItems();
            }

            if (!_settings.AddShopItems)
            {
                PreserveShopItems();
            }

            if (!_settings.AddOther)
            {
                PreserveOther();
            }

            if (_settings.RandomizeBottleCatchContents)
            {
                AddBottleCatchContents();
            }
            else
            {
                PreserveBottleCatchContents();
            }

            if (!_settings.AddMoonItems)
            {
                PreserveMoonItems();
            }
        }

        /// <summary>
        /// Keeps bottle catch contents vanilla
        /// </summary>
        private void PreserveBottleCatchContents()
        {
            for (int i = Items.BottleCatchFairy; i <= Items.BottleCatchMushroom; i++)
            {
                ItemList[i].ReplacesItemId = i;
            }
        }

        /// <summary>
        /// Randomizes bottle catch contents
        /// </summary>
        private void AddBottleCatchContents()
        {
            var itemPool = new List<int>();
            for (int i = Items.BottleCatchFairy; i <= Items.BottleCatchMushroom; i++)
            {
                if (ItemList[i].ReplacesAnotherItem)
                {
                    continue;
                }
                itemPool.Add(i);
            }

            for (int i = Items.BottleCatchFairy; i <= Items.BottleCatchMushroom; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Keeps other vanilla
        /// </summary>
        private void PreserveOther()
        {
            for (int i = Items.ChestLensCaveRedRupee; i <= Items.ChestToGoronRaceGrotto; i++)
            {
                ItemList[i].ReplacesItemId = i;
            }
        }

        /// <summary>
        /// Keeps shop items vanilla
        /// </summary>
        private void PreserveShopItems()
        {
            for (int i = Items.ShopItemTradingPostRedPotion; i <= Items.ShopItemZoraRedPotion; i++)
            {
                ItemList[i].ReplacesItemId = i;
            }

            ItemList[Items.ItemBombBag].ReplacesItemId = Items.ItemBombBag;
            ItemList[Items.UpgradeBigBombBag].ReplacesItemId = Items.UpgradeBigBombBag;
            ItemList[Items.MaskAllNight].ReplacesItemId = Items.MaskAllNight;
        }

        /// <summary>
        /// Keeps dungeon items vanilla
        /// </summary>
        private void PreserveDungeonItems()
        {
            for (int i = Items.ItemWoodfallMap; i <= Items.ItemStoneTowerKey4; i++)
            {
                ItemList[i].ReplacesItemId = i;
            };
        }

        /// <summary>
        /// Keeps moon items vanilla
        /// </summary>
        private void PreserveMoonItems()
        {
            for (int i = Items.HeartPieceDekuTrial; i <= Items.DekuPrincess; i++)
            {
                ItemList[i].ReplacesItemId = i;
            }
        }

        /// <summary>
        /// Randomizes songs with other songs
        /// </summary>
        private void ShuffleSongs()
        {
            var itemPool = new List<int>();
            for (int i = Items.SongSoaring; i <= Items.SongOath; i++)
            {
                if (ItemList[i].ReplacesAnotherItem)
                {
                    continue;
                }
                itemPool.Add(i);
            }

            for (int i = Items.SongSoaring; i <= Items.SongOath; i++)
            {
                PlaceItem(i, itemPool);
            }
        }

        /// <summary>
        /// Adds custom item list to randomization. NOTE: keeps area and other vanilla, randomizes bottle catch contents
        /// </summary>
        private void SetupCustomItems()
        {
            // Keep shop items vanilla, unless custom item list contains a shop item
            _settings.AddShopItems = false;

            // Make all items vanilla, and override using custom item list
            MakeAllItemsVanilla();

            // Should these be vanilla by default? Why not check settings.
            ApplyCustomItemList();

            // Should these be randomized by default? Why not check settings.
            AddBottleCatchContents();

            if (!_settings.AddSongs)
            {
                ShuffleSongs();
            }
        }

        /// <summary>
        /// Mark all items as replacing themselves (i.e. vanilla)
        /// </summary>
        private void MakeAllItemsVanilla()
        {
            for (int item = 0; item < ItemList.Count; item++)
            {
                if (ItemUtils.IsAreaOrOther(item)
                    || ItemUtils.IsOutOfRange(item))
                {
                    continue;
                }

                ItemList[item].ReplacesItemId = item;
            }
        }

        /// <summary>
        /// Adds items specified from the Custom Item List to the randomizer pool, while keeping the rest vanilla
        /// </summary>
        private void ApplyCustomItemList()
        {
            for (int i = 0; i < _settings.CustomItemList.Count; i++)
            {
                int selectedItem = _settings.CustomItemList[i];

                selectedItem = ItemUtils.AddItemOffset(selectedItem);

                int selectedItemIndex = ItemList.FindIndex(u => u.ID == selectedItem);

                if (selectedItemIndex != -1)
                {
                    ItemList[selectedItemIndex].ReplacesItemId = -1;
                }

                if (ItemUtils.IsShopItem(selectedItem))
                {
                    _settings.AddShopItems = true;
                }
            }
        }

        /// <summary>
        /// Randomizes the ROM with respect to the configured ruleset.
        /// </summary>
        public RandomizedResult Randomize(BackgroundWorker worker, DoWorkEventArgs e)
        {
            SeedRNG();

            _randomized = new RandomizedResult(_settings, Random);

            if (_settings.LogicMode != LogicMode.Vanilla)
            {
                worker.ReportProgress(5, "Preparing ruleset...");
                PrepareRulesetItemData();

                if (_settings.RandomizeDungeonEntrances)
                {
                    worker.ReportProgress(10, "Shuffling entrances...");
                    EntranceShuffle();
                }

                _randomized.Logic = ItemList.Select(io => new ItemLogic(io)).ToList();

                worker.ReportProgress(30, "Shuffling items...");
                RandomizeItems();


                if (_settings.EnableGossipHints)
                {
                    worker.ReportProgress(35, "Making gossip quotes...");
                    //gossip
                    SeedRNG();
                    MakeGossipQuotes();
                }
            }

            worker.ReportProgress(40, "Coloring Tatl...");

            //Randomize tatl colour
            SeedRNG();
            SetTatlColour();

            worker.ReportProgress(45, "Randomizing Music...");

            //Sort BGM
            SeedRNG();
            SortBGM();

            return _randomized;
        }
    }

}

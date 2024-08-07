using Dalamud.Game;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Utility;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Services {
    //internal service for managing duties and duty results
    internal class DutyManager : IDisposable {

        private delegate IntPtr TreasureDungeonDirectorCtorDelegate(IntPtr p1, IntPtr p2, IntPtr p3, byte p4);
        [Signature("48 89 5C 24 ?? 57 48 83 EC ?? 41 0F B6 D9 48 8B F9 E8", DetourName = nameof(TreasureDungeonDirectorCtorDetour))]
        private readonly Hook<TreasureDungeonDirectorCtorDelegate> _treasureDungeonDirectorCtorHook;

        private Plugin _plugin;
        private bool _firstTerritoryChange;
        //this is because there might be no checkpoints yet -_-
        //private List<LootResult> _firstLootResults = new();
        internal DutyResults? CurrentDutyResults { get; private set; }
        internal Duty? CurrentDuty {
            get {
                return IsDutyInProgress() ? Duties[CurrentDutyResults!.DutyId] : null;
            }
        }

        internal DutyResultsRaw? CurrentDutyResultsRaw { get; private set; }

        //internal CheckpointResults? LastCheckpoint => CurrentDutyResults?.CheckpointResults.LastOrDefault();

        internal readonly Dictionary<int, Duty> Duties = new Dictionary<int, Duty>() {
            { 179 , new Duty(179, "the aquapolis", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", EmptyCagesRegex),
                new Checkpoint("Open 2nd chamber", SecondChamberRegex),
                new Checkpoint("Clear 2nd chamber", EmptyCagesRegex),
                new Checkpoint("Open 3rd chamber", ThirdChamberRegex),
                new Checkpoint("Clear 3rd chamber", EmptyCagesRegex),
                new Checkpoint("Open 4th chamber", FourthChamberRegex),
                new Checkpoint("Clear 4th chamber", EmptyCagesRegex),
                new Checkpoint("Open 5th chamber", FifthChamberRegex),
                new Checkpoint("Clear 5th chamber", EmptyCagesRegex),
                new Checkpoint("Open 6th chamber", SixthChamberRegex),
                new Checkpoint("Clear 6th chamber", EmptyCagesRegex),
                new Checkpoint("Open final chamber", FinalChamberRegex),
                new Checkpoint("Clear final chamber", EmptyCagesRegex)
            }, new Checkpoint("Failure", "The Aquapolis has ended")) },
            { 268, new Duty(268, "the lost canals of uznair", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", EmptyCagesRegex),
                new Checkpoint("Open 2nd chamber", SecondChamberRegex),
                new Checkpoint("Clear 2nd chamber", EmptyCagesRegex),
                new Checkpoint("Open 3rd chamber", ThirdChamberRegex),
                new Checkpoint("Clear 3rd chamber", EmptyCagesRegex),
                new Checkpoint("Open 4th chamber", FourthChamberRegex),
                new Checkpoint("Clear 4th chamber", EmptyCagesRegex),
                new Checkpoint("Open 5th chamber", FifthChamberRegex),
                new Checkpoint("Clear 5th chamber", EmptyCagesRegex),
                new Checkpoint("Open 6th chamber", SixthChamberRegex),
                new Checkpoint("Clear 6th chamber", EmptyCagesRegex),
                new Checkpoint("Open final chamber", FinalChamberRegex),
                new Checkpoint("Clear final chamber", EmptyCagesRegex)
            }, new Checkpoint("Failure", "The Lost Canals of Uznair has ended")) },
            { 276 , new Duty(276, "the hidden canals of uznair", DutyStructure.Doors, 7, new() {
                new Checkpoint("Clear 1st chamber", EmptyCagesRegex),
                new Checkpoint("Open 2nd chamber", SecondChamberRegex),
                new Checkpoint("Clear 2nd chamber", EmptyCagesRegex),
                new Checkpoint("Open 3rd chamber", ThirdChamberRegex),
                new Checkpoint("Clear 3rd chamber", EmptyCagesRegex),
                new Checkpoint("Open 4th chamber", FourthChamberRegex),
                new Checkpoint("Clear 4th chamber", EmptyCagesRegex),
                new Checkpoint("Open 5th chamber", FifthChamberRegex),
                new Checkpoint("Clear 5th chamber", EmptyCagesRegex),
                new Checkpoint("Open 6th chamber", SixthChamberRegex),
                new Checkpoint("Clear 6th chamber", EmptyCagesRegex),
                new Checkpoint("Open final chamber", FinalChamberRegex),
                new Checkpoint("Clear final chamber", EmptyCagesRegex)
            }, new Checkpoint("Failure", "The Hidden Canals of Uznair has ended")) },
            { 586, new Duty(586, "the shifting altars of uznair", DutyStructure.Roulette, 5, new() {
                new Checkpoint("Complete 1st Summon"),
                new Checkpoint("Defeat 1st Summon"),
                new Checkpoint("Complete 2nd Summon"),
                new Checkpoint("Defeat 2nd Summon"),
                new Checkpoint("Complete 3rd Summon"),
                new Checkpoint("Defeat 3rd Summon"),
                new Checkpoint("Complete 4th Summon"),
                new Checkpoint("Defeat 4th Summon"),
                new Checkpoint("Complete final Summon"),
                new Checkpoint("Defeat final Summon")
            }, new Checkpoint("Failure", "The Shifting Altars of Uznair has ended"),
                new string[] {"altar beast", "altar chimera", "altar dullahan", "altar skatene", "altar totem", "hati" },
                new string[] {"altar arachne", "altar kelpie", "the older one", "the winged" },
                new string[] {"altar airavata", "altar mandragora", "the great gold whisker" },
                new string[] {"altar apanda", "altar diresaur", "altar manticore" }) },
            { 688 , new Duty(688, "the dungeons of lyhe ghiah", DutyStructure.Doors, 5, new() {
                new Checkpoint("Clear 1st chamber", EmptyCagesRegex),
                new Checkpoint("Open 2nd chamber", SecondChamberRegex),
                new Checkpoint("Clear 2nd chamber", EmptyCagesRegex),
                new Checkpoint("Open 3rd chamber", ThirdChamberRegex),
                new Checkpoint("Clear 3rd chamber", EmptyCagesRegex),
                new Checkpoint("Open 4th chamber", FourthChamberRegex),
                new Checkpoint("Clear 4th chamber", EmptyCagesRegex),
                new Checkpoint("Open final chamber", FinalChamberRegex),
                new Checkpoint("Clear final chamber", EmptyCagesRegex)
            }, new Checkpoint("Failure", "The Dungeons of Lyhe Ghiah has ended")) },
            { 745 , new Duty(745, "the shifting oubliettes of lyhe ghiah", DutyStructure.Roulette, 5, new() {
                new Checkpoint("Complete 1st Summon"),
                new Checkpoint("Defeat 1st Summon"),
                new Checkpoint("Complete 2nd Summon"),
                new Checkpoint("Defeat 2nd Summon"),
                new Checkpoint("Complete 3rd Summon"),
                new Checkpoint("Defeat 3rd Summon"),
                new Checkpoint("Complete 4th Summon"),
                new Checkpoint("Defeat 4th Summon"),
                new Checkpoint("Complete final Summon"),
                new Checkpoint("Defeat final Summon")
            }, new Checkpoint("Failure", "The Shifting Oubliettes of Lyhe Ghiah has ended."),
                new string[] {"secret undine", "secret djinn", "secret swallow", "secret serpent", "secret cladoselache", "secret worm" },
                new string[] {"greedy pixie", "secret basket", "secret pegasus", "secret porxie" },
                new string[] {"secret korrigan", "secret keeper", "fuath troublemaker" },
                new string[] {"daen ose the avaricious" }) },
            { 819 , new Duty(819, "the excitatron 6000", DutyStructure.Doors, 5, new() {
                new Checkpoint("Clear 1st chamber", EmptyCagesRegex),
                new Checkpoint("Open 2nd chamber", SecondChamberRegex),
                new Checkpoint("Clear 2nd chamber", EmptyCagesRegex),
                new Checkpoint("Open 3rd chamber", ThirdChamberRegex),
                new Checkpoint("Clear 3rd chamber", EmptyCagesRegex),
                new Checkpoint("Open 4th chamber", FourthChamberRegex),
                new Checkpoint("Clear 4th chamber", EmptyCagesRegex),
                new Checkpoint("Open final chamber", FinalChamberRegex),
                new Checkpoint("Clear final chamber", EmptyCagesRegex)
            }, new Checkpoint("Failure", "The Excitatron 6000 has ended")) },
            { 909 , new Duty(909, "the shifting gymnasion agonon", DutyStructure.Roulette, 5, new() {
                new Checkpoint("Complete 1st Summon"),
                new Checkpoint("Defeat 1st Summon"),
                new Checkpoint("Complete 2nd Summon"),
                new Checkpoint("Defeat 2nd Summon"),
                new Checkpoint("Complete 3rd Summon"),
                new Checkpoint("Defeat 3rd Summon"),
                new Checkpoint("Complete 4th Summon"),
                new Checkpoint("Defeat 4th Summon"),
                new Checkpoint("Complete final Summon"),
                new Checkpoint("Defeat final Summon")
            }, new Checkpoint("Failure", "The Shifting Gymnasion Agonon has ended"),
                new string[] {"gymnasiou megakantha", "gymnasiou triton", "gymnasiou satyros", "gymnasiou leon", "gymnasiou pithekos", "gymnasiou tigris" },
                new string[] {"gymnasiou styphnolobion", "gymnasiou meganereis", "gymnasiou sphinx", "gymnasiou acheloios" },
                new string[] {"lyssa chrysine", "lampas chrysine", "gymnasiou mandragoras" },
                new string[] {"hippomenes", "phaethon", "narkissos" }) },
            { 993 , new Duty(993, "cenote ja ja gural", DutyStructure.Doors, 5, new() {
                new Checkpoint("Clear 1st chamber", EmptyCagesRegex),
                new Checkpoint("Open 2nd chamber", SecondChamberRegex),
                new Checkpoint("Clear 2nd chamber", EmptyCagesRegex),
                new Checkpoint("Open 3rd chamber", ThirdChamberRegex),
                new Checkpoint("Clear 3rd chamber", EmptyCagesRegex),
                new Checkpoint("Open 4th chamber", FourthChamberRegex),
                new Checkpoint("Clear 4th chamber", EmptyCagesRegex),
                new Checkpoint("Open final chamber", FinalChamberRegex),
                new Checkpoint("Clear final chamber", EmptyCagesRegex)
            }, new Checkpoint("Failure", "Cenote Ja Ja Gural has ended")) },
        };

        internal static readonly Dictionary<ClientLanguage, Regex> GilObtainedRegex = new() {
            { ClientLanguage.English, new Regex(@"(?<=You obtain )[\d,\.]+(?= gil)", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(?<=Vous obtenez )[\d,\.\W]+(?= gils)", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(?<=Du hast )[\d,\.\W]+(?= Gil erhalten)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"[\d,\.\W]+(?=ギルを手に入れた)", RegexOptions.IgnoreCase) }
        };

        internal static readonly Dictionary<ClientLanguage, Regex> LootListRegex = new() {
            { ClientLanguage.English, new Regex(@"(the|an|a|[\.,\d]+)\b(?=.* been added to the loot list)", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(le|la|l'|un|une|[\.,\d]+)\b(?=.* a été ajoutée au butin)", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(?<=Ihr habt Beutegut \(.?)(ein|eine|einen|der|die|den|dem|des|[\.,\d]+)\b", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"([\.,\d]*)(?=戦利品に追加されました)", RegexOptions.IgnoreCase) }
        };

        internal static readonly Dictionary<ClientLanguage, Regex> SelfObtainedQuantityRegex = new() {
            { ClientLanguage.English, new Regex(@"(?<=You obtain .?)(the|an|a|[\.,\d]+)\b", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(?<=Vous obtenez .?)(le|la|l'|un|une|[\.,\d]+)\b", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(?<=Du hast .?)(ein|eine|einen|der|die|den|dem|des|[\.,\d]+)\b", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"[\.,\d]*(?=(個|を)手に入れた。)", RegexOptions.IgnoreCase) }
        };

        //EN note: does not work with items beginning with no indefinite
        //JP note: specifies allagan tomestones
        //may need this for party members...
        internal static readonly Dictionary<ClientLanguage, Regex> SelfObtainedItemRegex = new() {
            { ClientLanguage.English, new Regex(@"(?<=You obtain .?(an|a|[\.,\d])+\s)[\w\s]*", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(?<=Vous obtenez .?(un|une|[\.,\d])+\s)[\w\s]*", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(?<=Du hast .?(ein|eine|einen|[\.,\d]+)\s)[\w\s]*(?= erhalten)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@".*(?=を[\d]*個手に入れた。)", RegexOptions.IgnoreCase) }
        };

        internal static readonly Dictionary<ClientLanguage, Regex> PartyMemberObtainedRegex = new() {
            { ClientLanguage.English, new Regex(@"(?<=obtains .?)(the|an|a|[\.,\d]+)\b", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(?<=obtient .?)(le|la|l'|un|une|[\.,\d]+)\b", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(?<=hat .?)(ein|eine|einen|der|die|den|dem|des|[\.,\d]+)\b", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"[\.,\d]*(?=を手に入れた。)", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 3777, 3800
        internal static readonly Dictionary<ClientLanguage, Regex> EmptyCagesRegex = new() {
            { ClientLanguage.English, new Regex(@"The cages are empty", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez vaincu tous les monstres!", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(Die Gegner sind besiegt!|Du hast alle Gegner besiegt)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"すべての敵を倒した！", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 6998, 9365
        internal static readonly Dictionary<ClientLanguage, Regex> SecondChamberRegex = new() {
            { ClientLanguage.English, new Regex(@"The gate to the 2nd chamber opens", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez ouvert la porte menant (vers|à) la deuxième salle", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"Das Tor zur zweiten Kammer (öffnet sich|steht offen!)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"「第二区画」への扉が開いた！", RegexOptions.IgnoreCase) }
        };

        internal static readonly Dictionary<ClientLanguage, Regex> ThirdChamberRegex = new() {
            { ClientLanguage.English, new Regex(@"The gate to the 3rd chamber opens", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez ouvert la porte menant (vers|à) la troisième salle", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"Das Tor zur dritten Kammer (öffnet sich|steht offen!)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"「第三区画」への扉が開いた！", RegexOptions.IgnoreCase) }
        };

        internal static readonly Dictionary<ClientLanguage, Regex> FourthChamberRegex = new() {
            { ClientLanguage.English, new Regex(@"The gate to the 4th chamber opens", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez ouvert la porte menant (vers|à) la quatrième salle", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"Das Tor zur vierten Kammer (öffnet sich|steht offen!)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"「第四区画」への扉が開いた！", RegexOptions.IgnoreCase) }
        };

        internal static readonly Dictionary<ClientLanguage, Regex> FifthChamberRegex = new() {
            { ClientLanguage.English, new Regex(@"The gate to the 5th chamber opens", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez ouvert la porte menant (vers|à) la cinquième salle", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"Das Tor zur fünften Kammer (öffnet sich|steht offen!)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"「第五区画」への扉が開いた！", RegexOptions.IgnoreCase) }
        };

        internal static readonly Dictionary<ClientLanguage, Regex> SixthChamberRegex = new() {
            { ClientLanguage.English, new Regex(@"The gate to the 6th chamber opens", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez ouvert la porte menant (vers|à) la sixième salle", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"Das Tor zur sechsten Kammer (öffnet sich|steht offen!)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"「第六区画」への扉が開いた！", RegexOptions.IgnoreCase) }
        };

        internal static readonly Dictionary<ClientLanguage, Regex> FinalChamberRegex = new() {
            { ClientLanguage.English, new Regex(@"(The gate to Condemnation( is)? open(s)?|The gate to the final chamber opens)", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez ouvert la porte menant (vers|à) la dernière salle", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"Das Tor zur (letzten Kammer|Verdammnis) (öffnet sich|steht offen!)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"「最終区画」への扉が開いた！", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 9352
        internal static readonly Dictionary<ClientLanguage, Regex> IsSavedRegex = new() {
            { ClientLanguage.English, new Regex(@"^An unknown force", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"n'est plus.*apparaît\!$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"^Als .* fällt.*erscheint!$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"が消滅したことで、.*が現れた！$", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 9360, 9366
        internal static readonly Dictionary<ClientLanguage, Regex> AbominationRegex = new() {
            { ClientLanguage.English, new Regex(@"^The .* retreats into the shadows", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(Les ennemis se sont enfuis|L'avatar de l'observateur est parti)", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(Ihr konntet nicht alle Wächter bezwingen|Ihr konntet nicht alle Beobachter bezwingen)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"(魔物は立ち去ったようだ|観察者の幻体は去ったようだ)", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 9360, 9366
        internal static readonly Dictionary<ClientLanguage, Regex> SummonDefeatedRegex = new() {
            { ClientLanguage.English, new Regex(@"^(The summon is dispelled|The trial is passed)", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"Vous avez terrassé tous les ennemis", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(Alle Wächter sind besiegt|Alle Beobachter sind besiegt)", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"すべての魔物を倒した", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 6997
        internal static readonly Dictionary<ClientLanguage, Regex> HandOnGateRegex = new() {
            { ClientLanguage.English, new Regex(@"(hand on the gate\.)$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(actionné la porte\.)$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(das Tor zu öffnen\.)$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"(は、扉に手を掛けた。)$", RegexOptions.IgnoreCase) }
        };

        //LogMessage: 9350
        internal static readonly Dictionary<ClientLanguage, Regex> BeginInvocationRegex = new() {
            { ClientLanguage.English, new Regex(@"(begins the invocation\.)$", RegexOptions.IgnoreCase) },
            { ClientLanguage.French, new Regex(@"(a activé le cercle d'invocation!)$", RegexOptions.IgnoreCase) },
            { ClientLanguage.German, new Regex(@"(startet die Anbetung!)$", RegexOptions.IgnoreCase) },
            { ClientLanguage.Japanese, new Regex(@"(は、召喚魔法陣に手をかざした！)$", RegexOptions.IgnoreCase) }
        };

        public DutyManager(Plugin plugin) {
            _plugin = plugin;

            //setup regexes
            foreach(var duty in Duties) {
                duty.Value.FailureCheckpoint = new Checkpoint("Failure", GetFailureRegex(duty.Value.DutyId));

                if(duty.Value.Structure == DutyStructure.Roulette) {
                    duty.Value.LesserSummonRegex = GetTranslatedSummonRegex(duty.Key, Summon.Lesser);
                    duty.Value.GreaterSummonRegex = GetTranslatedSummonRegex(duty.Key, Summon.Greater);
                    duty.Value.ElderSummonRegex = GetTranslatedSummonRegex(duty.Key, Summon.Elder);
                    duty.Value.CircleShiftsRegex = GetTranslatedSummonRegex(duty.Key, Summon.Gold);
                }
            }

            _plugin.DutyState.DutyStarted += OnDutyStart;
            _plugin.DutyState.DutyCompleted += OnDutyCompleted;
            _plugin.DutyState.DutyWiped += OnDutyWiped;
            _plugin.DutyState.DutyCompleted += OnDutyRecommenced;
            _plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            _plugin.ChatGui.CheckMessageHandled += OnChatMessage;

#if DEBUG
            _plugin.InteropProvider.InitializeFromAttributes(this);
            _plugin.Log.Debug($"thd director .ctor address: 0x{_treasureDungeonDirectorCtorHook!.Address.ToString("X2")}");
            _treasureDungeonDirectorCtorHook.Enable();
#endif

            //attempt to pickup
            if(_plugin.ClientState.IsLoggedIn && _plugin.IsLanguageSupported() && !IsDutyInProgress()) {
                _plugin.DataQueue.QueueDataOperation(() => {
                    PickupLastDuty();
                });
            }
        }

        public void Dispose() {
            _plugin.DutyState.DutyStarted -= OnDutyStart;
            _plugin.DutyState.DutyCompleted -= OnDutyCompleted;
            _plugin.DutyState.DutyWiped -= OnDutyWiped;
            _plugin.DutyState.DutyCompleted -= OnDutyRecommenced;
            _plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
            _plugin.ChatGui.CheckMessageHandled -= OnChatMessage;
#if DEBUG
            _treasureDungeonDirectorCtorHook.Dispose();
#endif
        }

        private IntPtr TreasureDungeonDirectorCtorDetour(IntPtr p1, IntPtr p2, IntPtr p3, byte p4) {
            _plugin.Log.Debug("Treasure Hunt Dungeon Director .ctor occurred!");
            try {
                var currentDuty = _plugin.Functions.GetCurrentDutyId();
                _plugin.Log.Debug($"Current duty: {currentDuty}");
                if(!Duties.ContainsKey(currentDuty)) {
                    _plugin.Log.Information($"Unknown duty: {currentDuty}, starting message logging.");
                }

            } catch(Exception e) {
                //suppress all exceptions so game doesn't crash if something fails here
                _plugin.Log.Error(e, $"Error in THD director ctor");
            }
            return _treasureDungeonDirectorCtorHook.Original(p1, p2, p3, p4);
        }

        //attempt to start new duty results
        //returns true if succesfully started
        private bool StartNewDuty(int dutyId) {

            //abort if not in English-language client
            //if(!_plugin.IsEnglishClient()) {
            //    return false;
            //}

            if(Duties.ContainsKey(dutyId) && Duties[dutyId].Checkpoints != null) {
                //var lastMap = _plugin.StorageManager.GetMaps().Query().Where(m => !m.IsDeleted).OrderBy(m => m.Time).ToList().LastOrDefault();
                _plugin.Log.Information($"Starting new duty results for duty id: {dutyId}");
                //_currentDutyResults = new DutyResults(dutyId, Duties[dutyId].Name, _plugin.CurrentPartyList, "");
                CurrentDutyResults = new DutyResults {
                    DutyId = dutyId,
                    DutyName = Duties[dutyId].Name,
                    Players = _plugin.GameStateManager.CurrentPartyList.Keys.ToArray(),
                    Owner = "",
                };
                //_firstLootResults = new();

                var lastMap = _plugin.MapManager.GetLastMap();
                //check last map, 10 min fallback for linking to most recent map
                if(lastMap != null && (DateTime.Now - lastMap.Time).TotalMinutes < 10) {
                    CurrentDutyResults.Map = lastMap;
                    CurrentDutyResults.Owner = lastMap.Owner;
                    lastMap.IsPortal = true;
                    lastMap.DutyName = Duties[dutyId].GetDisplayName();
                    lastMap.DutyId = dutyId;
                    _plugin.StorageManager.UpdateMap(lastMap);
                } else {
                    _plugin.Log.Warning("Unknown map owner for current duty.");
                    CurrentDutyResults.Map = null;
                    CurrentDutyResults.Owner = "";
                }

                _plugin.StorageManager.AddDutyResults(CurrentDutyResults);
                //_plugin.Save();
                return true;
            }
            return false;
        }

        //attempt to pickup duty that did not complete
        //returns true if duty results was succesfully picked up
        private bool PickupLastDuty(bool toSave = true) {
            int dutyId = _plugin.Functions.GetCurrentDutyId();
            var duty = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            var lastDutyResults = _plugin.StorageManager.GetDutyResults().Query().OrderBy(dr => dr.Time).ToList().LastOrDefault();
            if(lastDutyResults != null) {
                TimeSpan lastTimeDiff = DateTime.Now - lastDutyResults.Time;
                //pickup if duty is valid, and matches the last duty which was not completed and not more than an hour has elapsed (fallback)
                if(Duties.ContainsKey(dutyId) && Duties[dutyId].Checkpoints != null && lastDutyResults.DutyId == dutyId && !lastDutyResults.IsComplete && !_firstTerritoryChange && lastTimeDiff.TotalHours < 1) {
                    _plugin.Log.Information($"re-picking up last duty results id:{lastDutyResults.Id.ToString()}");
                    CurrentDutyResults = lastDutyResults;
                    CurrentDutyResults.IsPickup = true;

                    _plugin.StorageManager.UpdateDutyResults(CurrentDutyResults);
                    //if(toSave) {
                    //    Plugin.StorageManager.UpdateDutyResults(_currentDutyResults);
                    //    Plugin.Save();
                    //}

                    return true;
                } else {
                    return false;
                }
            }
            return false;
        }


        private void StartNewUnknownDuty(int dutyId) {

            _plugin.Log.Information($"Starting new raw duty results for duty id: {dutyId}");

            CurrentDutyResultsRaw = new() {
                Language = _plugin.ClientState.ClientLanguage,
                DutyId = dutyId,
                Players = _plugin.GameStateManager.CurrentPartyList.Keys.ToArray(),
            };
            var lastMap = _plugin.MapManager.GetLastMap();
            if(lastMap != null && (DateTime.Now - lastMap.Time).TotalMinutes < 10) {
                CurrentDutyResultsRaw.Map = lastMap;
                CurrentDutyResultsRaw.Owner = lastMap.Owner;
                lastMap.IsPortal = true;
                lastMap.DutyId = dutyId;
                _plugin.StorageManager.UpdateMap(lastMap);
            } else {
                CurrentDutyResultsRaw.Map = null;
                CurrentDutyResultsRaw.Owner = "";
            }
        }

        //validate duty results and fill in missing data if possible
        private bool ValidateUpdateDutyResults(DutyResults dutyResults) {
            //check for no players
            if(dutyResults.Players == null || dutyResults.Players.Length <= 0) {
                _plugin.Log.Warning($"No players on duty results {dutyResults.Id.ToString()}");
                if(dutyResults.Owner.IsNullOrEmpty()) {
                    _plugin.Log.Warning($"No owner on duty results {dutyResults.Id.ToString()}");
                } else {
                    //dutyResults.Players = new[] { dutyResults.Owner };
                }
                dutyResults.Players = _plugin.GameStateManager.CurrentPartyList.Keys.ToArray();
                return false;
            }
            return true;
        }

        private void OnDutyStart(object? sender, ushort territoryId) {
            _plugin.Log.Debug($"Duty has started with territory id: {territoryId} name: {_plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryId)?.PlaceName.Value?.Name} ");
            var dutyId = _plugin.Functions.GetCurrentDutyId();
            _plugin.Log.Debug($"Current duty ID: {dutyId}");
            var duty = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            _plugin.Log.Debug($"Duty Name: {duty?.Name}");

            //check if duty is ongoing to attempt to pickup...
            _plugin.Log.Debug($"Current duty ongoing? {CurrentDutyResults != null}");
        }

        private void OnDutyCompleted(object? sender, ushort param1) {
            _plugin.Log.Verbose("Duty completed!");
            //EndDuty();
        }

        private void OnDutyWiped(object? sender, ushort param1) {
            _plugin.Log.Verbose("Duty wiped!");
            //EndDuty();
        }

        private void OnDutyRecommenced(object? sender, ushort param1) {
            _plugin.Log.Verbose("Duty recommenced!");
            //EndDuty();
        }

        private void OnTerritoryChanged(ushort territoryId) {
            var dutyId = _plugin.Functions.GetCurrentDutyId();
            _plugin.DataQueue.QueueDataOperation(() => {
                var duty = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
                _plugin.Log.Debug($"Territory changed: {territoryId}, Current duty: {_plugin.Functions.GetCurrentDutyId()}, Content Type: {_plugin.Functions.GetInstanceContentType().ToString() ?? ""}");

                if(!Duties.ContainsKey(dutyId) && _plugin.Functions.GetInstanceContentType() == InstanceContentType.TreasureHuntDungeon) {
                    _plugin.Log.Information($"Unknown treasure hunt duty: {dutyId}");
                }

                if(IsDutyInProgress()) {
                    //clear current duty if it was completed successfully or clear as a fallback. attempt to pickup otherwise on disconnect
                    if(CurrentDutyResults!.IsComplete || dutyId != CurrentDutyResults.DutyId) {
                        EndCurrentDuty();
                    }
                } else if(IsUnknownDutyInProgress()) {
                    if(CurrentDutyResultsRaw!.IsComplete || dutyId != CurrentDutyResultsRaw.DutyId) {
                        EndCurrentDuty();
                    }
                } else if(duty != null) {
                    if(Duties.ContainsKey(dutyId)) {
                        //attempt to pickup if game closed without completing properly
                        if(!PickupLastDuty(true)) {
                            StartNewDuty(dutyId);
                        }
                    } else if(_plugin.Functions.GetInstanceContentType() == InstanceContentType.TreasureHuntDungeon) {
                        //StartNewUnknownDuty(dutyId);
                    }
                }
                _firstTerritoryChange = true;
            });
        }

        private void OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {

            //refuse to process if not a supported language
            if(!_plugin.IsLanguageSupported()) {
                return;
            }

            switch((int)type) {
                case 62:
                case 2105:
                case 2110:
                case 2233:
                case 4158:
                case 8254:
                case (int)XivChatType.SystemMessage:
                    string messageText = message.ToString();
                    var item = (ItemPayload?)message.Payloads.FirstOrDefault(m => m is ItemPayload);
                    uint? itemId = item?.ItemId;
                    bool isHq = item is not null ? item.IsHQ : false;
                    var player = (PlayerPayload?)message.Payloads.FirstOrDefault(m => m is PlayerPayload);
                    string? playerKey = player is not null ? $"{player.PlayerName} {player.World.Name}" : null;
                    Message record = new(DateTime.Now, (int)type, messageText, itemId, isHq, playerKey);
                    _plugin.DataQueue.QueueDataOperation(() => {
                        if(IsUnknownDutyInProgress()) {
                            CurrentDutyResultsRaw!.Messages.Add(record);
                        } else if(IsDutyInProgress()) {
                            if(ProcessChatMessage(CurrentDutyResults!, record)) {
                                _plugin.StorageManager.UpdateDutyResults(CurrentDutyResults!);
                            }
                        }
                    });
                    break;
                default:
                    break;
            }
        }

        //return true if change occured
        private bool ProcessChatMessage(DutyResults results, Message message) {
            bool isChange = false;
            var duty = Duties[results.DutyId];

            //check for gil obtained
            if(message.Channel == 62) {
                Match m = GilObtainedRegex[_plugin.ClientState.ClientLanguage].Match(message.Text);
                if(m.Success) {
                    string parsedGilString = m.Value.Replace(",", "").Replace(".", "").Replace(" ", "");
                    int gil = int.Parse(parsedGilString);
                    results.TotalGil += gil;
                    AddLootResults(results, 1, false, gil, _plugin.GameStateManager.GetCurrentPlayer());
                    isChange = true;
                }
                //self loot obtained
            } else if(message.Channel == 2110) {
                Match quantityMatch = SelfObtainedQuantityRegex[_plugin.ClientState.ClientLanguage].Match(message.Text);
                Match itemMatch = SelfObtainedItemRegex[_plugin.ClientState.ClientLanguage].Match(message.Text);
                if(quantityMatch.Success) {
                    bool isNumber = Regex.IsMatch(quantityMatch.Value, @"\d+");
                    int quantity = isNumber ? int.Parse(quantityMatch.Value.Replace(",", "").Replace(".", "")) : 1;
                    var currentPlayer = _plugin.GameStateManager.GetCurrentPlayer();
                    if(message.ItemId is not null) {
                        AddLootResults(results, (uint)message.ItemId, (bool)message.IsHq, quantity, currentPlayer);
                        isChange = true;
#if DEBUG
                        _plugin.Log.Debug(string.Format("itemId: {0, -40} isHQ: {1, -6} quantity: {2, -5} recipient: {3}", message.ItemId, message.IsHq, quantity, currentPlayer));
#endif
                    } else if(itemMatch.Success) {
                        //tomestones
                        //Japanese has no plural...
                        var rowId = quantity != 1 && _plugin.ClientState.ClientLanguage != ClientLanguage.Japanese ? _plugin.GetRowId<Item>(itemMatch.Value, "Plural", GrammarCase.Accusative) : _plugin.GetRowId<Item>(itemMatch.Value, "Singular", GrammarCase.Accusative);
                        if(rowId is not null) {
                            AddLootResults(results, (uint)rowId, false, quantity, currentPlayer);
                            isChange = true;
                        } else {
                            _plugin.Log.Warning($"Cannot find rowId for {itemMatch.Value}");
                        }
                    }
                }
                //party member loot obtained
            } else if(message.Channel == 8254 || message.Channel == 4158) {
                Match m = PartyMemberObtainedRegex[_plugin.ClientState.ClientLanguage].Match(message.Text);
                if(m.Success) {
                    //todo make this work for all languages...
                    bool isNumber = Regex.IsMatch(m.Value, @"\d+");
                    int quantity = isNumber ? int.Parse(m.Value.Replace(",", "").Replace(".", "")) : 1;
                    if(message.ItemId is not null && !message.PlayerKey.IsNullOrEmpty()) {
                        AddLootResults(results, (uint)message.ItemId, (bool)message.IsHq, quantity, message.PlayerKey);
                        isChange = true;
#if DEBUG
                        _plugin.Log.Debug(string.Format("itemId: {0, -40} isHQ: {1, -6} quantity: {2, -5} recipient: {3}", message.ItemId, message.IsHq, quantity, message.PlayerKey));
#endif
                    }
                }

                //check for loot list
            } else if((XivChatType)message.Channel == XivChatType.SystemMessage) {
                Match m = LootListRegex[_plugin.ClientState.ClientLanguage].Match(message.Text);
                if(m.Success) {
                    //todo make this work for all languages...
                    bool isNumber = Regex.IsMatch(m.Value, @"\d+");
                    int quantity = isNumber ? int.Parse(m.Value.Replace(",", "").Replace(".", "")) : 1;
                    if(message.ItemId is not null) {
                        AddLootResults(results, (uint)message.ItemId, (bool)message.IsHq, quantity);
                        isChange = true;
#if DEBUG
                        _plugin.Log.Debug(string.Format("itemId: {0, -40} isHQ: {1, -6} quantity: {2, -5}", message.ItemId, message.IsHq, quantity));
                        _plugin.Log.Debug($"value: {m.Value} isNumber: {isNumber} quantity: {quantity}");
#endif
                    }
                }

                //check for failure
            } else if((message.Channel == 2233 || message.Channel == 2105) && duty.FailureCheckpoint!.LocalizedRegex![_plugin.ClientState.ClientLanguage].IsMatch(message.Text)) {
                results!.IsComplete = true;
                results!.CompletionTime = DateTime.Now;
                isChange = true;
            } else {
                switch(CurrentDuty!.Structure) {
                    case DutyStructure.Doors:
                        isChange = ProcessCheckpointsDoors(results, message);
                        break;
                    case DutyStructure.Roulette:
                        isChange = ProcessCheckpointsRoulette(results, message);
                        break;
                    default:
                        break;
                }
            }
            ////save if changes discovered
            //if(isChange) {
            //    _plugin.StorageManager.UpdateDutyResults(CurrentDutyResults!);
            //    //_plugin.Save();
            //}
            return isChange;
        }

        //return true if updates made
        private bool ProcessCheckpointsDoors(DutyResults results, Message message) {
            var duty = Duties[results.DutyId];
            if(results.CheckpointResults.Count < duty.Checkpoints!.Count) {
                var nextCheckpoint = duty.Checkpoints![results.CheckpointResults.Count];
                if((message.Channel == 2233 || message.Channel == 2105) && nextCheckpoint.LocalizedRegex![_plugin.ClientState.ClientLanguage].IsMatch(message.Text)) {
                    _plugin.Log.Information($"Adding new checkpoint: {nextCheckpoint.Name}");
                    results.CheckpointResults.Add(new() {
                        Checkpoint = nextCheckpoint,
                        IsReached = true,
                        LootResults = new(),
                    });
                    if(results.CheckpointResults.Count > 0 && results.FirstLootResults.Count > 0) {
                        results.LastCheckpoint!.LootResults = results.FirstLootResults;
                        results.FirstLootResults = new();
                    }

                    //if all checkpoints reached, set to duty complete
                    if(results.CheckpointResults.Where(cr => cr.IsReached).Count() == duty.Checkpoints!.Count) {
                        results.IsComplete = true;
                        results.CompletionTime = DateTime.Now;
                    }
                    return true;
                }
            }
            return false;
        }

        private bool ProcessCheckpointsRoulette(DutyResults results, Message message) {
            var duty = Duties[results.DutyId];
            if(duty.Structure != DutyStructure.Roulette) {
                throw new ArgumentException("Incorrect duty type.");
            }

            if(message.Channel == 2105 || message.Channel == 2233) {
                //check for save
                bool isSave = IsSavedRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.Text);
                //check for circles shift
                Match shiftMatch = duty!.CircleShiftsRegex![_plugin.ClientState.ClientLanguage].Match(message.Text);
                if(shiftMatch.Success) {
                    AddRouletteCheckpointResults(results, Summon.Gold, _plugin.TranslateBNpcName(shiftMatch.Value, ClientLanguage.English), isSave);
                    return true;
                }
                //check for abomination
                Match specialMatch = AbominationRegex[_plugin.ClientState.ClientLanguage].Match(message.Text);
                if(specialMatch.Success) {
                    AddRouletteCheckpointResults(results, Summon.Silver, null, isSave);
                    //add next checkpoint as well
                    AddRouletteCheckpointResults(results, null);
                    if(results!.CheckpointResults.Where(cr => cr.IsReached).Count() == duty.Checkpoints!.Count) {
                        results.IsComplete = true;
                        results.CompletionTime = DateTime.Now;
                    }
                    return true;
                }
                //check for lesser summon
                Match lesserMatch = duty.LesserSummonRegex![_plugin.ClientState.ClientLanguage]!.Match(message.Text);
                if(lesserMatch.Success) {
                    AddRouletteCheckpointResults(results, Summon.Lesser, _plugin.TranslateBNpcName(lesserMatch.Value, ClientLanguage.English), isSave);
                    return true;
                }
                //check for greater summon
                Match greaterMatch = duty.GreaterSummonRegex![_plugin.ClientState.ClientLanguage]!.Match(message.Text);
                if(greaterMatch.Success) {
                    AddRouletteCheckpointResults(results, Summon.Greater, _plugin.TranslateBNpcName(greaterMatch.Value, ClientLanguage.English), isSave);
                    return true;
                }
                //check for elder summon
                Match elderMatch = duty.ElderSummonRegex![_plugin.ClientState.ClientLanguage]!.Match(message.Text);
                if(elderMatch.Success) {
                    AddRouletteCheckpointResults(results, Summon.Elder, _plugin.TranslateBNpcName(elderMatch.Value, ClientLanguage.English), isSave);
                    return true;
                }
                //enemy defeated
                if(SummonDefeatedRegex[_plugin.ClientState.ClientLanguage].IsMatch(message.Text)) {
                    AddRouletteCheckpointResults(results, null);
                    if(results.CheckpointResults.Where(cr => cr.IsReached).Count() == duty.Checkpoints!.Count) {
                        results.IsComplete = true;
                        results.CompletionTime = DateTime.Now;
                    }
                    return true;
                }

                //check for unknown enemy
                //Match unknownMatch = Regex.Match(message.ToString(), ".*(?=,? appears?)", RegexOptions.IgnoreCase);
                //(?<=\ban?\b ).*(?=,? appears\.*\!*$)
            }
            return false;
        }

        private void AddRouletteCheckpointResults(DutyResults results, Summon? summon, string? monsterName = null, bool isSaved = false) {
            int size = results.CheckpointResults.Count;
            _plugin.Log.Information($"Adding new checkpoint: {CurrentDuty!.Checkpoints![size].Name}");
            results.CheckpointResults.Add(new RouletteCheckpointResults {
                Checkpoint = CurrentDuty!.Checkpoints![size],
                SummonType = summon,
                MonsterName = monsterName,
                IsSaved = isSaved,
                IsReached = true,
                LootResults = new(),
            });
            if(results.CheckpointResults.Count > 0 && results.FirstLootResults.Count > 0) {
                results.LastCheckpoint!.LootResults = results.FirstLootResults;
                results.FirstLootResults = new();
            }

            //(CheckpointResults[size].Checkpoint as RouletteCheckpoint).SummonType = summon;
            //(CheckpointResults[size].Checkpoint as RouletteCheckpoint).Enemy = enemy;
        }

        private void AddLootResults(DutyResults results, uint itemId, bool isHQ, int quantity, string? recipient = null) {
            var matchingLootResults = results.GetMatchingLootResult(itemId, isHQ, quantity);
            if(matchingLootResults is null) {
                LootResult lootResult = new() {
                    Time = DateTime.Now,
                    ItemId = itemId,
                    IsHQ = isHQ,
                    Quantity = quantity,
                    Recipient = recipient,
                };
                if(results.LastCheckpoint is null) {
                    results.FirstLootResults.Add(lootResult);
                } else if(results.LastCheckpoint.LootResults != null) {
                    results.LastCheckpoint.LootResults.Add(lootResult);
                }
            } else {
                matchingLootResults.Recipient = recipient;
            }
        }

        private void EndCurrentDuty() {
            if(IsDutyInProgress()) {
                _plugin.Log.Information($"Ending duty results id: {CurrentDutyResults!.Id}");
                //CurrentDutyResults!.IsComplete = true;
                //if(CurrentDutyResults.CompletionTime.Ticks == 0) {
                //    CurrentDutyResults.CompletionTime = DateTime.Now;
                //}
                //check for malformed/missing data
                ValidateUpdateDutyResults(CurrentDutyResults);
                _plugin.StorageManager.UpdateDutyResults(CurrentDutyResults);
                //_firstLootResults = new();
            } else if(IsUnknownDutyInProgress()) {
                _plugin.Log.Information($"Ending raw duty results id: {CurrentDutyResultsRaw!.Id}");
                CurrentDutyResultsRaw.IsComplete = true;
                _plugin.StorageManager.UpdateDutyResultsRaw(CurrentDutyResultsRaw);
            }
            CurrentDutyResults = null;
            CurrentDutyResultsRaw = null;
        }

        internal bool IsDutyInProgress() {
            return CurrentDutyResults != null;
        }

        internal bool IsUnknownDutyInProgress() {
            return CurrentDutyResultsRaw != null;
        }

        private Dictionary<ClientLanguage, Regex> GetFailureRegex(int dutyId) {
            string? dutyNameEnglish = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>(ClientLanguage.English)?.Where(r => r.RowId == dutyId).FirstOrDefault()?.Name.ToString();
            string? dutyNameFrench = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>(ClientLanguage.French)?.Where(r => r.RowId == dutyId).FirstOrDefault()?.Name.ToString();
            string? dutyNameGerman = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>(ClientLanguage.German)?.Where(r => r.RowId == dutyId).FirstOrDefault()?.Name.ToString();
            string? dutyNameJapanese = _plugin.DataManager.GetExcelSheet<ContentFinderCondition>(ClientLanguage.Japanese)?.Where(r => r.RowId == dutyId).FirstOrDefault()?.Name.ToString();

            return new Dictionary<ClientLanguage, Regex>() {
                { ClientLanguage.English, new Regex($"{dutyNameEnglish} has ended", RegexOptions.IgnoreCase) },
                { ClientLanguage.French, new Regex($"La mission “{dutyNameFrench}” prend fin", RegexOptions.IgnoreCase) },
                { ClientLanguage.German, new Regex($"„{dutyNameGerman}“ wurde beendet", RegexOptions.IgnoreCase) },
                { ClientLanguage.Japanese, new Regex($"「{dutyNameJapanese}」の攻略を終了した。", RegexOptions.IgnoreCase) }
            };
        }

        private Dictionary<ClientLanguage, Regex> GetTranslatedSummonRegex(int dutyId, Summon summonType) {
            var duty = Duties[dutyId];
            if(duty == null || duty.Structure != DutyStructure.Roulette) {
                throw new InvalidOperationException("cannot build summon regex for null/non-roulette duty!");
            }

            Dictionary<ClientLanguage, string> patterns = new() {
                {ClientLanguage.French, "(" },
                {ClientLanguage.German, "(" },
                {ClientLanguage.Japanese, "(" }
            };

            string[] toIterate;
            switch(summonType) {
                case Summon.Lesser:
                    toIterate = duty.LesserSummons!; break;
                case Summon.Greater:
                    toIterate = duty.GreaterSummons!; break;
                case Summon.Elder:
                case Summon.Gold:
                    toIterate = duty.ElderSummons!.ToList().Concat(duty.FinalSummons!).ToArray(); break;
                default:
                    throw new InvalidOperationException("cannot build summon regex for invalid summon type!");
            }

            for(int i = 0; i < toIterate.Length; i++) {
                foreach(var kvp in patterns) {
                    var translatedName = _plugin.TranslateBNpcName(toIterate[i], kvp.Key, ClientLanguage.English);
                    patterns[kvp.Key] += translatedName;
                    if(i == toIterate.Length - 1) {
                        patterns[kvp.Key] += ")";
                    } else {
                        patterns[kvp.Key] += "|";
                    }
                }
            }

            //language-specific terminations
            switch(summonType) {
                case Summon.Lesser:
                case Summon.Greater:
                case Summon.Elder:
                default:
                    patterns.Add(ClientLanguage.English, duty.GetSummonPatternString(summonType) + @"(?=,? appears?)");
                    patterns[ClientLanguage.French] += "(?= apparaît)";
                    patterns[ClientLanguage.German] += "";
                    patterns[ClientLanguage.Japanese] += "(?=が現れた)";
                    break;
                case Summon.Gold:
                    patterns.Add(ClientLanguage.English, "(?<=The circles shift and (a |an )?)" + duty.GetSummonPatternString(Summon.Elder) + "(?=,? appears?)");
                    patterns[ClientLanguage.French] = "(?<=Aubaine! (Un |Une )?)" + patterns[ClientLanguage.French] + "(?= apparaît)";
                    patterns[ClientLanguage.German] = "(?<=Eine glückliche Fügung wird euch zuteil und (ein |eine |einen )?)" + patterns[ClientLanguage.German] + "(?= erscheint)";
                    patterns[ClientLanguage.Japanese] = "(?<=召喚式変動が発動し、)" + patterns[ClientLanguage.Japanese] + "(?=が現れた)";
                    break;
            }

            return new Dictionary<ClientLanguage, Regex>() {
                { ClientLanguage.English, new Regex(patterns[ClientLanguage.English], RegexOptions.IgnoreCase) },
                { ClientLanguage.French, new Regex(patterns[ClientLanguage.French], RegexOptions.IgnoreCase) },
                { ClientLanguage.German, new Regex(patterns[ClientLanguage.German], RegexOptions.IgnoreCase) },
                { ClientLanguage.Japanese, new Regex(patterns[ClientLanguage.Japanese], RegexOptions.IgnoreCase) },
            };
        }
    }
}

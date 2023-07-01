using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapPartyAssist.Services {
    internal class DutyManager : IDisposable {

        private Plugin Plugin;
        private DutyResults? _currentDutyResults;

        private static Dictionary<string, Type> DutyResultTypes = new Dictionary<string, Type>() {
            { "the lost canals of uznair", typeof(LostCanalsOfUznairResults) },
            { "the shifting altars of uznair", typeof(ShiftingAltarsOfUznairResults) },
            { "the hidden canals of uznair", typeof(HiddenCanalsOfUznairResults) }
        };

        public DutyManager(Plugin plugin) {
            Plugin = plugin;
            Plugin.DutyState.DutyStarted += OnDutyStart;
            Plugin.DutyState.DutyCompleted += OnDutyCompleted;
            Plugin.DutyState.DutyWiped += OnDutyWiped;
            Plugin.DutyState.DutyCompleted += OnDutyRecommenced;
            Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
            Plugin.ChatGui.ChatMessage += OnChatMessage;
        }

        public void Dispose() {
            PluginLog.Debug("disposing duty manager");
            Plugin.DutyState.DutyStarted -= OnDutyStart;
            Plugin.DutyState.DutyCompleted -= OnDutyCompleted;
            Plugin.DutyState.DutyWiped -= OnDutyWiped;
            Plugin.DutyState.DutyCompleted -= OnDutyRecommenced;
            Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
            Plugin.ChatGui.ChatMessage -= OnChatMessage;

            //if(IsDutyInProgress()) {
            //    Plugin.ChatGui.ChatMessage -= _currentDutyResults.OnChatMessage;
            //}
        }

        public void StartNewDuty(string dutyName, int dutyId, Dictionary<string, MPAMember> players, string owner) {
            dutyName = dutyName.ToLower();
            if(DutyResultTypes.ContainsKey(dutyName)) {
                //var dutyResults = new DutyResults(dutyName, players, owner);
                //_currentDutyResults = typeof(DutyResultTypes[dutyName]) dutyResults as typeof(DutyResultTypes[dutyName]);
                //_currentDutyResults = typeof(DutyResultTypes[dutyName]).GetConstructor().Invoke(dutyId, players, owner);
                object[] conParams = {dutyId, players, owner};
                _currentDutyResults = DutyResultTypes[dutyName].GetConstructors().First().Invoke(conParams) as DutyResults;
                //_currentDutyResults = DutyResultTypes[dutyName].GetConstructors().First().Invoke(conParams);
                //DutyResultTypes[dutyName].Name;
                Plugin.Configuration.DutyResults.Add(_currentDutyResults!);
            }
        }

        private void OnDutyStart(object? sender, ushort territoryId) {
            PluginLog.Debug($"Duty has started with territory id: {territoryId} name: {Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryId)?.PlaceName.Value?.Name} ");
            var dutyId = Plugin.Functions.GetCurrentDutyId();
            PluginLog.Debug($"Current duty ID: {dutyId}");
            var duty = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            PluginLog.Debug($"Duty Name: {duty?.Name}");

            if(duty != null) {
                StartNewDuty(duty.Name.ToString(), dutyId, Plugin.CurrentPartyList, Plugin.MapManager!.LastMapPlayerKey);
            }

            //should do this in a more robust way...
            //if(Regex.IsMatch(duty.Name.ToString(), @"uznair|aquapolis|lyhe ghiah|gymnasion agonon|excitatron 6000$", RegexOptions.IgnoreCase)) {
            //    StartNewDuty(duty.Name.ToString(), dutyId, Plugin.CurrentPartyList, Plugin.MapManager.LastMapPlayerKey);
            //    //initialize duty results
            //    //if(Regex.IsMatch(duty.Name.ToString(), @"lost canals of uznair$", RegexOptions.IgnoreCase)) {
            //    //    _currentDutyResults = new LostCanalsOfUznairResults(dutyId, Plugin.CurrentPartyList, Plugin.MapManager.LastMapPlayerKey);
            //    //    //Plugin.ChatGui.ChatMessage += _currentDutyResults.OnChatMessage;
            //    //} else if(Regex.IsMatch(duty.Name.ToString(), @"hidden canals of uznair$", RegexOptions.IgnoreCase)) {
            //    //    _currentDutyResults = new HiddenCanalsOfUznairResults(dutyId, Plugin.CurrentPartyList, Plugin.MapManager.LastMapPlayerKey);
            //    //    //Plugin.ChatGui.ChatMessage += _currentDutyResults.OnChatMessage;
            //    //}
            //}
        }

        private void OnDutyCompleted(object? sender, ushort param1) {
            PluginLog.Debug("Duty completed!");
            //EndDuty();
        }

        private void OnDutyWiped(object? sender, ushort param1) {
            PluginLog.Debug("Duty wiped!");
            //EndDuty();
        }

        private void OnDutyRecommenced(object? sender, ushort param1) {
            PluginLog.Debug("Duty recommenced!");
            //EndDuty();
        }

        private void OnTerritoryChanged(object? sender, ushort territoryId) {
            //end duty if it was completed successfully or clear as a fallback for failure
            if(IsDutyInProgress() && (_currentDutyResults!.IsComplete || Plugin.Functions.GetCurrentDutyId() != _currentDutyResults.DutyId)) {
                EndDuty();
            }
            //todo: re-pickup on game crash
        }

        private void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            if(IsDutyInProgress()) {
                //save if changes discovered
                if(_currentDutyResults!.ProcessChat(type, senderId, sender, message)) {
                    Plugin.Configuration.Save();
                }
            }
        }

        private void EndDuty(bool success = false) {
            if(IsDutyInProgress()) {
                //_currentDutyResults.IsComplete = success;
                //Plugin.ChatGui.ChatMessage -= _currentDutyResults.OnChatMessage;
                //only save full runs
                //if(_currentDutyResults.IsComplete) {
                //    //Plugin.Configuration.DutyResults.Add(_currentDutyResults);
                //    Plugin.Configuration.Save();
                //}
                _currentDutyResults = null;
                Plugin.Configuration.Save();
            }
        }

        private bool IsDutyInProgress() {
            return _currentDutyResults != null;
        }
    }
}

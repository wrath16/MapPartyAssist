using Dalamud.Logging;
using Lumina.Excel.GeneratedSheets;
using MapPartyAssist.Types;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MapPartyAssist.Services {
    internal class DutyManager : IDisposable {

        private Plugin Plugin;
        private bool _isDutyInProgress;
        private DutyResults? _currentDutyResults;

        public DutyManager(Plugin plugin) {
            Plugin = plugin;
            Plugin.DutyState.DutyStarted += OnDutyStart;
            Plugin.DutyState.DutyCompleted += OnDutyCompleted;
            Plugin.DutyState.DutyWiped += OnDutyWiped;
            Plugin.DutyState.DutyCompleted += OnDutyRecommenced;
            Plugin.ClientState.TerritoryChanged += OnTerritoryChanged;
        }

        public void Dispose() {
            PluginLog.Debug("disposing duty manager");
            Plugin.DutyState.DutyStarted -= OnDutyStart;
            Plugin.DutyState.DutyCompleted -= OnDutyCompleted;
            Plugin.DutyState.DutyWiped -= OnDutyWiped;
            Plugin.DutyState.DutyCompleted -= OnDutyRecommenced;
            Plugin.ClientState.TerritoryChanged -= OnTerritoryChanged;
        }

        public void StartNewDuty(string dutyName, Dictionary<string, MPAMember> players, string owner) {

        }

        private void OnDutyStart(object? sender, ushort territoryId) {
            PluginLog.Debug($"Duty has started with territory id: {territoryId} name: {Plugin.DataManager.GetExcelSheet<TerritoryType>()?.GetRow(territoryId)?.PlaceName.Value?.Name} ");
            var dutyId = Plugin.Functions.GetCurrentDutyId();
            PluginLog.Debug($"Current duty ID: {dutyId}");
            var duty = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()?.GetRow((uint)dutyId);
            PluginLog.Debug($"Duty Name: {duty?.Name}");

            //should do this in a more robust way...
            if(Regex.IsMatch(duty.Name.ToString(), @"uznair|aquapolis|lyhe ghiah|gymnasion agonon|excitatron 6000$", RegexOptions.IgnoreCase)) {
                //initialize duty results
                if(Regex.IsMatch(duty.Name.ToString(), @"lost canals of uznair$", RegexOptions.IgnoreCase)) {

                    //Configuration.DutyResults.Add(_currentDutyResults);
                } else if(Regex.IsMatch(duty.Name.ToString(), @"hidden canals of uznair$", RegexOptions.IgnoreCase)) {

                }
            }
        }

        private void OnDutyCompleted(object? sender, ushort param1) {
            PluginLog.Debug("Duty completed!");
            _isDutyInProgress = false;
        }

        private void OnDutyWiped(object? sender, ushort param1) {
            PluginLog.Debug("Duty wiped!");
            _isDutyInProgress = false;
        }

        private void OnDutyRecommenced(object? sender, ushort param1) {
            PluginLog.Debug("Duty recommenced!");
            _isDutyInProgress = false;
        }

        private void OnTerritoryChanged(object? sender, ushort territoryId) {
            //clear current duty
        }
    }
}

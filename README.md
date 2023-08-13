# Map Party Assist

Tool for automatically keeping track of treasure maps opened by players in your party. Also records treasure dungeon statistics.

[![image](https://i.imgur.com/JeyAe7l.png) [![image](https://i.imgur.com/OMK8LPU.png)

## Installation

1. Add "https://raw.githubusercontent.com/wrath16/DalamudPluginRepo/main/pluginmaster.json" to your Custom Plugin Repositories under Settings -> Experimental, then save.
2. Plugin should now appear in the available plugins list and be installable.

## Usage instructions 

### Map tracking:

* Enter '/mparty' into chat to open the window.
* When a player uses a map by touching a treasure chest or entering a portal, it will be automatically recorded with a checkmark.
* Maps can be manually added and removed by right-clicking on each party member's name or checkmarks.
* If someone links a waymark in party chat, it will be noted with a magnifying glass next to their name that you can hover over or click on to open your map with the waymark.
* The most recent party member to use a map will be highlighted in yellow.
* Maps auto-archive after 24 hours, and you can see recent party members' maps until then. You can also clear the slate manually if you're running multiple parties a day.

### Treasure dungeon stats:

* Enter '/mpartystats' to open the stats window.
* The "Current" filter only includes duties resulting from maps currently recorded on the main window.

### Important notes:
* The plugin works by parsing system chat messages. I haven't verified that every map uses the same suite of messages, so let me know if it doesn't work for some. Also, it's not always possible to tell who exactly opened a map, so it might get confused if multiple people are using Dig at the same time. It might also fail if you're too far away from the party member who uses Dig. In these cases, you can manually add or remove. Thief's Maps, thankfully, are very reliable since they always generate a portal.
* You can use '/mpartydutyresults' to amend the results of a dungeon in the case of a mistake by the tracker or you miss a checkpoint due to a disconnect. Do so at your own risk!
* Only works with the English language client!

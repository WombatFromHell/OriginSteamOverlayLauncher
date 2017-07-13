# OriginSteamOverlayLauncher
How To Install/Uninstall
========================
This wrapper requires no installation other than copying it into the directory of the game executable you wish to run through Steam BPM/Overlay. It also can simply be deleted if you wish to uninstall it at any time. If you have trouble running it, or are running Windows 8 or earlier, you may need to download and install the [.NET Framework Redistributable v4.5](https://www.microsoft.com/en-us/download/details.aspx?id=40779).


How To Use
==========
* Place the "OriginSteamOverlayLauncher.exe" file in the directory with the game executable you wish to launch through Origin.
* Go into Steam and add the OriginSteamOverlayLauncher.exe as a Non-Steam Game, selecting it from the directory of the game executable you wish to run through Steam.
* Rename the shortcut in Steam to the name of the game in Origin so you can retrieve Steam Community Configurator Profiles.
* Launch the shortcut you've created and you'll be prompted to select the paths of the Origin Launcher (by default: "C:\Program Files (x86)\Origin\Origin.exe"), and the game executable you wish to use.
* This wrapper should run Origin (so Steam Overlay can hook into it) and then the game, and if everything went okay the Steam Overlay will appear in-game.


Notes
=====
**IMPORTANT: (As of v1.02) It is possible to run both the Origin overlay and the Steam overlay together, but it can be a potential cause of crashes if you have another overlay like Afterburner/RTSS on top of it. You have been warned!**

(As of v1.04) OSOL now supports running a command before the launcher starts as well as after the game exits (before OSOL closes). Simply fill in the "PreLaunchExec" and "PostGameExec" options in the INI with a valid path to an executable of your choosing. This could be used, for example, to temporarily stop and restart the "AMD External Events Utility" service or some other problematic system service via a script before/after launching a game that requires it be disabled for overlay hooking to function properly.

If you wish to use additional arguments with your game executable you should edit the "OriginSteamOverlayLauncher.ini", and change the setting "GameArgs" to whatever command line options are necessary. This setting is created after running OSOL and choosing your paths.

(As of v1.03) OSOL now supports launcher URLs (like Battle.net and Origin), if you wish to use this feature you'll need to do the following:
* Place "OriginSteamOverlayLauncher.exe" in the directory of the game you wish to launch.
* Run OSOL, pick your game and launcher paths (make sure you choose the right launcher exe, ex: Battle.net.exe).
* Edit the "OriginSteamOverlayLauncher.ini" and change the option "LauncherMode" to "URI" (no quotes).
* Change the "LauncherURI" option to your launcher URL, for common Battle.net launcher strings look below.

* In the case of Origin, you'll want to launch the game normally first then look in "C:\ProgramData\Origin\Logs\Bootstrapper_Log.txt" for a line similar to this at the bottom of the file:
> Event "C:\ProgramFiles(x86)\Origin\Origin.exe""origin2://game/launch/?offerIds=1019025&title=Mass%u0020Effect%u2122%u003a%u0020Andromeda&authCode=&cmdParams="
* You want the offerIds token, so your URL would look something like:
> origin2://game/launch/?offerIds=1019025
* Now that you have your launch id, change the "LauncherURI" option to:
> LauncherURI=origin2://game/launch/?offerIds=1019025

```
List of common launcher strings for Battle.net:
Heroes of the Storm = battlenet://hots
World of Warcraft   = battlenet://WoW
Hearthstone         = battlenet://WTCG
Starcraft 2         = battlenet://sc2
Overwatch           = battlenet://Pro
Diablo 3            = battlenet://d3
Starcraft           = battlenet://sc
```

(As of v1.02) If you run into any games that don't launch properly by default, either because of overlay problems or just not launching at all via OSOL, you can edit the "OriginSteamOverlayLauncher.ini" after setting your paths and change the "LauncherMode" setting to "LauncherOnly" (no quotes).



Known Issues
============
If you have difficulty getting the Steam overlay to hook into your game when launching with OSOL please follow [these instructions](https://support.steampowered.com/kb_article.php?ref=9828-SFLZ-9289), and make sure OSOL and Steam are both running with the appropriate permissions (if Steam is running as Admin, make sure to run OSOL as Admin as well so that all processes spawned from it can be hooked by Steam). **This is important for older games (circa <2007).**

If you have issues with games not launching with the Steam Overlay and are using a recent AMD graphics device you may need to disable the "AMD External Events Utility" service by following the instructions below:

* Run "services.msc".
* Browse down to the "AMD External Events" service.
* Double-click it, change the startup type to "Disabled", and click "Stop" to disable the service, then click "OK" and exit the dialog.

NOTE: This will break FreeSync functionality, but allow the Steam Overlay to hook into Origin games.


How To Compile
==============
If you wish to compile this project from github source, you'll need Visual Studio v14+ or Visual Studio Express and target the .NET Framework v4.5 for C#. There are no external libraries required. The source code can be modified freely under the MIT license as long as the contributers and creator are given credit.


Credits
=======
Thanks to Dafzor and his bnetlauncher wrapper (http://madalien.com/stuff/bnetlauncher/) for giving me the idea to make this.


Donations [![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://paypal.me/JBrown749)
=========
If you find this project useful and you would like to donate toward on-going development you can use the link above. Any and all donations are much appreciated!

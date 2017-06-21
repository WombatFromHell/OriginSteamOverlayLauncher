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
**IMPORTANT: (As of v1.02) It is possible to run both the Origin overlay and the Steam overlay together, but it can be a potential cause of crashes if you have another overlay hook like Afterburner/RTSS on top of it. Venture onward at your own risk!**

If you wish to use additional arguments with your game executable you should edit the "OriginSteamOverlayLauncher.ini", and change the setting "GameArgs" to whatever command line options are necessary. This setting is created after running OSOL and choosing your paths.

(As of v1.02) If you run into any games that don't launch properly by default, either because of overlay problems or just not launching at all via OSOL, you can edit the "OriginSteamOverlayLauncher.ini" after setting your paths and change the "LauncherMode" setting to "LauncherOnly" (no quotes).


Known Issues
============
If you have issues with games not launching with the Steam Overlay and are using a recent AMD graphics device you may need to disable the "AMD External Events" service.

NOTE: This will break FreeSync functionality, but allow the Steam Overlay to hook into Origin games:

* Run "services.msc".
* Browse down to the "AMD External Events" service.
* Double-click it, change the startup type to "Disabled", and click "Stop" to disable the service.


How To Compile
==============
If you wish to compile this project from github source, you'll need Visual Studio v14+ or Visual Studio Express and target the .NET Framework v4.5 for C#. There are no external libraries required. The source code can be modified freely under the MIT license as long as the contributers and creator are given credit.


Credits
=======
Thanks to Dafzor and his bnetlauncher wrapper (http://madalien.com/stuff/bnetlauncher/) for giving me the idea to make this.

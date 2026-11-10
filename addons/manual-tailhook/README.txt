NO Manual Tailhook 0.1.1

Default controls:
  Left Ctrl + H: deploy or retract the equipped tailhook.
  Left Ctrl + Left Shift + H: restore automatic control.

Automatic control remains active until the first manual command. Manual control
persists until you restore automatic mode or change aircraft. Retracting a caught
hook also releases the cable. The mod does not add hooks to unequipped aircraft.
Automatic conditions are reevaluated within approximately one second.

Configuration: F1 > NO Manual Tailhook (BepInEx Configuration Manager), or
BepInEx/config/br.japensou.nuclearoption.manualtailhook.cfg after the first
launch. Shortcuts use left modifiers by default and can be changed. Commands
are ignored in menus, while paused, when unfocused or while editing text.

Installation: requires BepInEx 5. Extract the BepInEx folder into the Nuclear
Option game directory. The plugin is BepInEx/plugins/ManualTailHook.
If the game is running, the update takes effect on the next launch.
Uninstallation: close the game and remove only that plugin folder.

Built against the local 0.34.6 installation, Steam build 26327221. Includes an
optional FS-20C Vortex hook bridge that preserves native bay-door animation.
Blueprinter and Vortex are not required for native aircraft hooks.

Baseline 0.1.0 verification: 20 offline assertions with real Harmony and simulated
objects, compilation against the installed game and native API inspection.
Version 0.1.1 was compiled and its English strings and package/installation
hashes were checked. Windows Application Control blocked rerunning the harness.
Not yet tested in the game. Animation, physical arrestment, keyboard/HUD behavior
and multiplayer synchronization remain unverified. Manual control is local;
the plugin does not add a tailhook synchronization protocol.

0.1.1: all HUD messages, configuration descriptions and package instructions
are in English. Controls and hook behavior are unchanged.

Author: Jpgamer4045d. MIT license; substantial AI assistance is declared.
Source: https://github.com/jptecr2314/FS-20-Vortex-Family/tree/main/addons/manual-tailhook

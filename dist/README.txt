Flying Azure — a Windows screensaver
====================================
Microsoft Azure logos fly across the screen leaving fading trails, with a
date/time clock. Multi-monitor, smooth, and configurable.

Made with love by Kevin Griffin
  consultwithgriff.com
  x.com/1kevgriff
  bsky.app/profile/consultwithgriff.com
  linkedin.com/in/1kevgriff


INSTALL (easiest)
-----------------
1. Right-click  install.ps1  ->  "Run with PowerShell".
2. That's it — it installs for you and turns on after 10 minutes idle.

Preview it right away by double-clicking FlyingAzure.scr (or right-click -> Test).
Change options (logo count, speed, size, trail, background, clock corner) by
right-clicking FlyingAzure.scr -> Configure.


ADD IT TO THE WINDOWS SCREEN-SAVER LIST (optional)
--------------------------------------------------
If you want it to appear in Settings > Personalization > Lock screen >
Screen saver, open PowerShell in this folder and run:

    ./install.ps1 -System

Approve the administrator prompt.


REMOVE IT
---------
Right-click  uninstall.ps1  ->  "Run with PowerShell".
(If you used -System, run:  ./uninstall.ps1 -System )


IF WINDOWS BLOCKS IT
--------------------
Windows may flag files downloaded from the internet.
- If a script won't run, open PowerShell in this folder and run:
      powershell -ExecutionPolicy Bypass -File install.ps1
- If FlyingAzure.scr is blocked, right-click it -> Properties -> check
  "Unblock" -> OK. (install.ps1 also tries to unblock it for you.)


No installation of .NET is required — everything needed is bundled in
FlyingAzure.scr. Works on 64-bit Windows 10 and 11.

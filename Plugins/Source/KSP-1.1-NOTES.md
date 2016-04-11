# KAS pre-release for KSP 1.1

Please, note that **this is not a playing version**. Do not install it unless your intention is
testing and reporting bugs in the pre-release. If your intention is to play 1.1 with favorite
mods then installing this pre-release is a bad choice: your game experience may get ruined due
to bugs and undetermined behavior of the test code.

### How to install KSP 1.1 via Steam

- Make a full backup of the existing game. Nobody guaranties your savefiles won't be
broken.
- Go to `Kerbal Space Program / Properties / Betas` and choose "prerelease"
([screenshot](http://i.imgur.com/8L5l1Sp.png)). Once Steam is done uploading to the new version
go into the game's folder and drop all your mods. Most of them won't work anyways but they may
interfere with the new KIS/KAS.
- Install latest pre-release builds of KIS/KAS from the development forum the thread (TBD).

### Running two versions of KSP

With Steam on Windows there are strange artefacts when running 1.1.0 not from the original folder.
So, it's encouraged to copy both versions in the independent folders and map required version to
the original Steam's folder via `mklink /D` command. Keep in mind you need to run it under
administrator. Shutdown and don't run Steam when a "wrong" version is mapped, otherwise Steam will
rewrite the files.

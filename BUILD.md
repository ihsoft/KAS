# KIS - How to build a binary and make a release

## WINDOWS users

### Prerequisites
- For building:
  - Get C# runtime of version 4.0 or higher.
  - Create a virtual drive pointing to KSP installation: `subst q: <path to KSP root>`. I.e. if `KSP.exe` lives in `S:\Steam\Kerbal Space Program\` then this is the root.
    - If you choose not to do that or the drive letter is different then you also need to change `KAS.csproj` project file to correct references and post-build actions.
- For making releases:
  - Python 2.7 or greater.
  - Owner or collaborator permissions in [Github repository](https://github.com/ihsoft/KAS).
  - Owner or maintainer permissions on [Curseforge project](http://kerbal.curseforge.com/projects/kerbal-attachment-system-kas).
- For development do one of the following things:
  - Install an open source [SharpDevelop](https://en.wikipedia.org/wiki/SharpDevelop). It will pickup existing project settings just fine but at the same time can add some new changes. Please, don't submit them into the trunk until they are really needed to build the project.
  - Get a free copy of [Visual Studio Express](https://www.visualstudio.com/en-US/products/visual-studio-express-vs). It should work but was not tested.

### Versioning explained
Version number consists of three numbers - X.Y.Z:
- X - MAJOR. A really huge change is required to affect this number. Like releasing a first version: it's always a huge change.
- Y - MINOR. Adding a new functionality or removing an old one (highly discouraged) is that kind of changes.
- Z - PATCH. Bugfixes, small feature requests, and internal cleanup changes.

### Building
- Review file `Tools\make_binary.cmd` and ensure the path to `MSBuild` is right.
- Run `Tools\make_binary.cmd` having folder `Tools` as current.
- Given there were no compile errors the new DLL file can be found in `.\KAS\Plugins\Source\bin\Release\`.

_Note_: If you don't want building yourself you can use the DLL from the repository. It is updated by the maintainer each time a new version is released.

### Releasing
- Review file `Tools\make_binary.cmd` and ensure the path to `MSBuild` is right.
- Review file `Tools\make_release.py` and ensure `ZIP_BINARY` points to a ZIP compatible command line executable.
- Verify that file `KAS\Plugins\Source\Properties\AssemblyInfo.cs` has correct version number. This will be the release number!
- Check if file `KAS\Plugins\Source\CHANGES.txt` has any "alpha" changes since the last release:
  - Only consider changes of types: Fix, Feature, Enhancement, and Change. Anything else is internal stuff which is not interesting to the outer world.
  - Copy the changes into `changelog.md` and add the release date.
  - Go thru issues having #XX in the title, and update each releveant [Github issue](https://github.com/ihsoft/KAS/issues) with the version where it was addressed. Usually it means closing of the issue but there can be exceptions.
  - Drop all changes from `CHANGES.txt`.
- Run `Tools\make_release.py -p` having folder `Tools` as current.
- Given there were no compile errors the new release will live in `Releases` folder.
- Update [Github repository](https://github.com/ihsoft/KAS) with the files updated during the release.
- Create a new release in the [Github repository releases](https://github.com/ihsoft/KAS/releases). Use changes from `changelog.md` as a release description. Do **not** add ZIP into the release. Primary source for the release binaries is [Curseforge](http://kerbal.curseforge.com/projects/kerbal-attachment-system-kas/files).
- Upload new package to [Curseforge](http://kerbal.curseforge.com/projects/kerbal-attachment-system-kas/files). Once verified the package will become available for downloading.
- Add new version entry in [KSP-CKAN](https://github.com/KSP-CKAN/CKAN-meta/tree/master/KAS) (you may need to fork first) and request a pull request. CKAN has an automated system to push new versions which may conflict with the manual update so, keep an eye on the meta for a couple of days.
 - Use "download" link from Curseforge as the file source.

_Note_: You can run `make_release.py` without parameter `-p`. In this case release folder structure will be created in folder `Release` but no archive will be prepared.

_Note_: As a safety measure `make_release.py` checks if the package being built is already existing, and if it does then release process aborts. When you need to override an existing package either delete it manually or pass flag `-o` to the release script.

## iOS & Linux users

...please add your suggestions for the building phase. The release phase should work as is.

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
  - Owner or maintainer permissions on [Spacedock](https://spacedock.info/mod/1987/Kerbal%20Attachment%20System%20%28KAS%29).
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

### Releasing
- Review file `Tools\make_binary.cmd` and ensure the path to `MSBuild` is right.
- Verify that file `KAS\Plugins\Source\Properties\AssemblyInfo.cs` has correct version number. This will be the release number!
- Check if file `KAS\Plugins\CHANGES.txt` has relevant information and the current version/date is properly set. This information will be used as description by the publishing scripts.
- Open command-line console with folder `Tools` set as current.
- Run command `KspReleaseBuilder.py -Jp`:
  - If it reports any errors, check them!
  - If no errors reported, there will be an archive created: `KAS_vX.Y.zip`.
- Update release configs for every target:
  - See files `publish_*_args.txt`.
  - Ensure that authentication information is set correctly. It depends on the target.
  - Fix the release archive name. For now it's not handled automatically.
- Commit the changes made so far into the `next` branch and create a release pull request.
  - ATTENTION! Make sure you're not comitting the authentication information from the publishing configs!
- When ready to release, merge the PR created above.
- Run the publishing scripts. The order and timing are important!
  1. Run `publish_github.cmd`. It'll create draft reelase on GitHub. Go there, review, and submit it.
  2. Run `publish_curseforge.cmd`. Wait for at least 3 hours before proceeding to the step #3. CurseForge is a source for the `CKAN` repository, it's best to wait till it's indexing job picks up the new version. Doing it overnight is an obvious choice. Use [this link](http://status.ksp-ckan.space/) to verify if the new version is picked up by `CKAN`.
  3. Run `publish_spacedock.cmd`. This will ping all the subscribers immedeately.

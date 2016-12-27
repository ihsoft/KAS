# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# Version: 1.1
# GitHub: https://github.com/ihsoft/KerbalReleaseBuilder

# A very simple class to produce a .ZIP archive with a KSP mod distribution.

import glob
import json
import os.path
import re
import shutil
import subprocess


class Builder(object):
  VERSION = None

  # System path to binary that creates ZIP archive from a folder.
  # See InvokeArchiver() method for the executable parameters.
  # It's highly recommended to use open source and free 7-Zip which is 100%
  # compatible with the default setup: http://www.7-zip.org/download.html
  SHELL_ZIP_BINARY = None
  
  # An executable which will be called to build the project's binaraies in release mode.
  # See CompileBinary() method.
  SHELL_COMPILE_BINARY_SCRIPT = None

  # Folder name in game's GameData folder. It's also a release archive name.
  PACKAGE_NAME = None

  # Base path for all the repository files.
  SRC = None

  # Assembly info file to extract version number from. See ExtractVersion() method.
  SRC_VERSIONS_FILE = '/Source/Properties/AssemblyInfo.cs'

  # Path to the release's binary. If it doesn't exist then no release.
  SRC_COMPILED_BINARY = None

  # Version file to be updated during the build (see UpdateVersionInSources).
  SRC_REPOSITORY_VERSION_FILE = None

  # A path where release structure will be constructed.
  DEST = '../Release'

  # A path where resulted ZIP file wil be stored. It's counted relative to the
  # release script.
  ARCHIVE_DEST = '..'

  # A format string which accepts VERSION as argument and return distribution
  # file name with no extension.
  DEST_RELEASE_NAME_FMT = None

  # A file name format for releases with build field other than zero.
  DEST_RELEASE_NAME_WITH_BUILD_FMT = None

  # Definition of the main release structure:
  # - KEY is a path in the DEST folder. Keys are sorted before handling.
  # - VALUE is an array of Python glob.glob paths:
  #   - If prefixed with '?' then it's allowed for the pattern to return no files.
  #   - If prefixed with '-' then patytern will be applied against actual files in
  #     the *destination* folder, and those files will be deleted.
  #   - If there is no special prefix then pattern is expected to return at least
  #     one match.
  # NOTE. Folder for the KEY is not created if no files were found in any of the
  #    copy patterns.
  STRUCTURE = {}

  # File copy actions to do after the build.
  # First item of the tuple defines source, and the second item defines the target.
  # The paths are *not* adjusted to either SRC or DEST. I.e. they can be anything.
  # FIXME: make the glob patterns
  POST_BUILD_COPY = []

  # Settings that are allowed to be accepted from the JSON file.
  JSON_VALUES = [
    'SHELL_ZIP_BINARY',
    'SHELL_COMPILE_BINARY_SCRIPT',
    'PACKAGE_NAME',
    'SRC',
    'DEST',
    'ARCHIVE_DEST',
    'SRC_COMPILED_BINARY',
    'SRC_REPOSITORY_VERSION_FILE',
    'SRC_VERSIONS_FILE',
    'STRUCTURE',
    'POST_BUILD_COPY',
    'DEST_RELEASE_NAME_FMT',
    'DEST_RELEASE_NAME_WITH_BUILD_FMT',
  ]


  def __init__(self, make_script_path, archiver_path):
    self.SHELL_COMPILE_BINARY_SCRIPT = make_script_path
    self.SHELL_ZIP_BINARY = archiver_path
    

  # Makes the binary.
  def CompileBinary(self):
    binary_path = self.SRC + self.ParseMacros(self.SRC_COMPILED_BINARY)
    if os.path.exists(binary_path):
      os.unlink(binary_path)
    print 'Compiling sources in PROD mode...'
    code = subprocess.call([self.SHELL_COMPILE_BINARY_SCRIPT])
    if code != 0 or not os.path.exists(binary_path):
      print 'ERROR: Compilation failed. Cannot find target DLL:', binary_path
      exit(code)
  
  
  # Purges any existed files in the release folder.
  def CleanupReleaseFolder(self):
    print 'Cleanup release folder...'
    shutil.rmtree(self.DEST, True)


  def __TargetsCmpFunction(self, x, y):
    if x and x[0] == '/' and (not y or y[0] != '/'):
      return -1
    if y and y[0] == '/' and (not x or x[0] != '/'):
      return 1
    return cmp(x, y)
  
  
  # Creates whole release structure and copies the required files.
  def MakeFoldersStructure(self):
    # Make.
    print 'START: Building release structure:'
    sorted_targets = sorted(
        self.STRUCTURE.iteritems(), key=lambda x: x[0], cmp=self.__TargetsCmpFunction)
    for (dest_folder, src_patterns) in sorted_targets:
      if not dest_folder or dest_folder[0] != '/':
        dest_path = self.DEST + '/GameData/' + self.PACKAGE_NAME
        if dest_folder:
          dest_path += '/' + dest_folder
      else:
        dest_path = self.DEST + '/GameData' + dest_folder
      print 'Folder:', dest_path
      copy_sources = []
      drop_patterns = []
      for src_pattern in src_patterns:
        allow_no_matches = False
        is_drop_pattern = False
        pattern = self.SRC + self.ParseMacros(src_pattern)

        if src_pattern[0] == '?':
          allow_no_matches = True
          pattern = self.SRC + self.ParseMacros(src_pattern[1:])
        elif src_pattern[0] == '-':
          is_drop_pattern = True
          drop_patterns.append(src_pattern[1:])
          continue

        entry_sources = glob.glob(pattern)
        if not entry_sources and not is_drop_pattern:
          if allow_no_matches:
            print '=> skip pattern "%s" since no matches found' % pattern
          else:
            print 'ERROR: Nothing is found for pattern:', pattern
            exit(-1)
        if not is_drop_pattern:
          copy_sources.extend(entry_sources)
        else:
          drop_patterns.extend(entry_sources)

      # Copy files.
      if copy_sources:
        for source in copy_sources:
          if os.path.isfile(source):
            self.MaybeCreateFolder(dest_path)
            print '=> copy file:', source
            shutil.copy(source, dest_path)
          elif os.path.isdir(source):
            print '=> copy folder:', source
            shutil.copytree(source, dest_path + '/' + os.path.basename(source))
          else:
            print "SKIP:", source
      elif allow_no_matches:
        print '=> skip release folder due to it\'s EMPTY'
      else:
        print 'ERROR: Nothing found for release folder:', dest_folder
        print 'HINT: If this folder is allowed to be emoty then add "?" to the destination'
        exit(-1)

      # Drop files.
      if drop_patterns:
        drop_sources = []
        for pattern in drop_patterns:
          if pattern[0] == '/':
            print 'ERROR: Cleanup pattern must not be absolute:', pattern
            exit(-1)
          drop_sources.extend(glob.glob(os.path.join(dest_path, pattern)))
        for source in drop_sources:
          if os.path.isfile(source):
            print '=> drop file:', os.path.relpath(source, dest_path)
            os.unlink(source)
          else:
            print '=> drop folder:', source
            shutil.rmtree(source)

    print 'END: Building release structure'


  # Creates folder path if one doesn't exist.
  def MaybeCreateFolder(self, folder):
    if not os.path.isdir(folder):
      print 'Create folder:', folder
      os.makedirs(folder)


  # Checks if value is a macro and returns the the appropriate value of the
  # Builder instance. If it's not then the same value is returned.
  # Macros start and ends with "###". E.g. "##ABC###" means value of "ABC"
  # property on the Builder instance.
  def ParseMacros(self, value):
    return re.sub(r'\{(\w+)}', lambda x: self.ParseMacros(getattr(self, x.group(1))), value)
   
  
  # Extarcts version number of the release from the sources.
  def ExtractVersion(self):
    file_path = self.SRC + self.ParseMacros(self.SRC_VERSIONS_FILE)
    with open(file_path) as f:
      content = f.readlines()
    for line in content:
      if line.lstrip().startswith('//'):
        continue
      # Expect: [assembly: AssemblyVersion("X.Y.Z")]
      matches = re.match(r'\[assembly: AssemblyVersion.*\("(\d+)\.(\d+)\.(\d+)(.(\d+))?"\)\]', line)
      if matches:
        self.VERSION = (int(matches.group(1)),  # MAJOR
                        int(matches.group(2)),  # MINOR
                        int(matches.group(3)),  # PATCH
                        int(matches.group(5) or 0))  # BUILD, optional.
        break
        
    if self.VERSION is None:
      print 'ERROR: Cannot extract version from: %s' % file_path
      exit(-1)
    print 'Releasing version: v%d.%d.%d build %d' % self.VERSION
  
  
  # Updates the destination files with the version info.
  def PostBuildCopy(self):
    if self.POST_BUILD_COPY:
      print 'Post-build copy step:'
      for source, target in self.POST_BUILD_COPY:
        source = self.ParseMacros(source)
        target = self.ParseMacros(target)
        print '  ..."%s" into "%s"...' % (source, target)
        shutil.copy(source, target)
  
  
  # Updates the source files with the version info.
  def UpdateVersionInSources(self):
    version_file = self.SRC + self.ParseMacros(self.SRC_REPOSITORY_VERSION_FILE)
    print 'Update version file:', version_file
    with open(version_file) as fp:
      content = json.load(fp);
    if not 'VERSION' in content:
      print 'ERROR: Cannot find VERSION in:', version_file
      exit(-1)
    content['VERSION']['MAJOR'] = self.VERSION[0]
    content['VERSION']['MINOR'] = self.VERSION[1]
    content['VERSION']['PATCH'] = self.VERSION[2]
    content['VERSION']['BUILD'] = self.VERSION[3]
    with open(version_file, 'w') as fp:
      json.dump(content, fp, indent=4, sort_keys=True)
  
  
  def MakeReleaseFileName(self):
    if self.VERSION[3]:
      return self.DEST_RELEASE_NAME_WITH_BUILD_FMT % self.VERSION
    else:
      return self.DEST_RELEASE_NAME_FMT % self.VERSION[:3]
  
  
  # Creates a package for re-destribution.
  def MakePackage(self, overwrite_existing):
    release_name = self.MakeReleaseFileName();
    package_file_name = self.ParseMacros('%s/%s.zip' % (self.ARCHIVE_DEST, release_name))
    if os.path.exists(package_file_name): 
      if not overwrite_existing:
        print 'ERROR: Package for this version already exists: %s' % package_file_name
        exit(-1)
  
      print 'WARNING: Package already exists. Deleting.'
      os.remove(package_file_name)
  
    self.MaybeCreateFolder(self.ARCHIVE_DEST)
    print 'Making %s package...' % self.PACKAGE_NAME
    code = self.InvokeArchiver(package_file_name, self.DEST)
    if code != 0:
      print 'ERROR: Failed to make the package.'
      exit(code)


  # Invokes archiver to compress the release folder. Places ZIP archive into the specified path.
  # @param archive_name Path to th archive uncouding it's name and extension (.ZIP).
  # @param folder_to_compress Release folder to compress.
  def InvokeArchiver(self, archive_name, folder_to_compress):
    return subprocess.call([
        self.SHELL_ZIP_BINARY,
        'a',
        archive_name,
        folder_to_compress + '/*'])


  # Sets basic settings to a common layout:
  # - Versions for build 0 are named as '<mod_name>.1.2.3.zip'.
  # - Versions for build otehr than 0 are named as '<mod_name>.1.2.3_build4.zip'.
  # - Release DLL builds into '{SRC}/Source/bin/Release/<mod_name>.dll'.
  # - AVC-like version file is expected to exist in '{SRC}/<mod_name>.version'
  def SetupDefaultLayout(self):
    self.DEST_RELEASE_NAME_FMT = '{PACKAGE_NAME}_v%d.%d.%d'
    self.DEST_RELEASE_NAME_WITH_BUILD_FMT = '{PACKAGE_NAME}_v%d.%d.%d_build%d'
    self.SRC_COMPILED_BINARY = '/Source/bin/Release/{PACKAGE_NAME}.dll'
    self.SRC_REPOSITORY_VERSION_FILE = '/{PACKAGE_NAME}.version'


  # Loads main build settings from a JSON file.
  def LoadSettingsFromJson(self, file_name):
    print 'Load settings from JSON file:', file_name
    self.SetupDefaultLayout()
    with open(file_name) as fp:
      content = json.load(fp);
    for (key, value) in content.iteritems():
      if key in self.JSON_VALUES:
        setattr(self, key, value)
      else:
        print 'ERROR: Unknown key in JSON:', key
        print 'HINT: Allowed keys are:\n', '\n'.join(self.JSON_VALUES)
        exit(-1)


  # Runs all the steps and produces a release ZIP.
  # If overwrite_existing is not specified and there is a ZIP with the same name then this method fails.
  def MakeRelease(self, make_archive_zip, overwrite_existing=False):
    self.ExtractVersion()
    self.CleanupReleaseFolder()
    self.CompileBinary()
    if self.SRC_VERSIONS_FILE and self.SRC_REPOSITORY_VERSION_FILE:
      self.UpdateVersionInSources()
    else:
      print 'No sources to set version, skipping'
    self.MakeFoldersStructure()
    self.PostBuildCopy()
    if make_archive_zip:    
      self.MakePackage(overwrite_existing)
    else:
      print 'No package requested, skipping.'
    print 'SUCCESS!'


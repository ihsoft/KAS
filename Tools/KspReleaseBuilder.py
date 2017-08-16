# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev/tree/master/Sources/ReleaseBuilder
SCRIPT_VERSION = "2.1"  # (check if SUPPORTED_JSON_SCHEMA_VERSION needs to be updated!)

# A very simple class to produce a .ZIP archive with a KSP mod distribution.

import argparse
import array
import ctypes
import glob
import json
import os.path
import re
import shutil
import subprocess
import sys
import textwrap


# Version of the settings JSON file. When a new version of the script changes the way of
# interpreting the configuration file, the supported version cobnstant must be extended.
# - Increase the minor part when the change is backward-comptible. I.e. the new script version can
#   handle the old minor versions with the same output as the old script would produce.
# - Increase the major part and reset the minor part if the change is NOT backward-comptible.
# Preserve the old versions as comments to keep the history.
#SUPPORTED_JSON_SCHEMA_VERSION = "1.0"  # Starting from script version "2.0"
SUPPORTED_JSON_SCHEMA_VERSION = "1.1"  # Starting from script version "2.1"


class Builder(object):
  # Parsed assembly version. It's an array of integers: MAJOR, MINOR, PATCH, BUILD.
  VERSION = None

  # An executable which will be called to build the project's binaraies in release mode.
  # See __Step_CompileBinary() method.
  # If path is relative then it's counted against either script's or settings file folder.
  SHELL_COMPILE_BINARY_SCRIPT = None

  # Name to identify mod in the game. This value is used in many default settings (see below).
  PACKAGE_NAME = None

  # Base path for all the repository files. It intentionally does NOT support macros.
  # This setting allows relative parts (e.g. '..') which will be counted relative to either
  # script or settings file (depends on the execution mode).
  # A special value "#github" can be set to make script detecting GitHub repository root where
  # either script or the settings file is located. When repository is found this faield is set
  # to the relative path.
  PROJECT_ROOT = '#github'

  # Path to the mod's C# sources.
  # This path is relative to {PROJECT_ROOT} even when prefixed with '/'.
  SOURCE = 'Source'

  # Assembly info file to extract version number from. See __Step_ExtractVersion() method.
  # This path is relative to {SOURCE} unless starts with '/'.
  ASSEMBLY_INFO_FILE = 'Properties/AssemblyInfo.cs'

  # Path to the release's binary. If it doesn't exist then no release.
  # This path is relative to {SOURCE} unless starts with '/'.
  COMPILED_BINARY = 'bin/Release/{PACKAGE_NAME}.dll'

  # Version file to be updated during the build (see __Step_UpdateVersionInSources).
  # This path is relative to {SOURCE} unless starts with '/'.
  MINIAVC_VERSION_FILE = '/{PACKAGE_NAME}.version'

  # Path where release structure will be constructed.
  # This path is relative to {PROJECT_ROOT} even when prefixed with '/'.
  RELEASE = 'Release'

  # Base folder for the mod's release files.
  # This path is relative to {RELEASE} even when prefixed with '/'.
  RELEASE_MOD_FOLDER = '{PACKAGE_NAME}'

  # Path where resulted ZIP file wil be stored.
  ARCHIVE_DEST = ''

  # Format string which accepts MAJOR, MINOR and PATCH numbers as arguments and
  # returns release package file name with no extension.
  RELEASE_NAME_FMT = '{PACKAGE_NAME}_v%d.%d.%d'

  # File name format for releases with build field other than zero.
  RELEASE_NAME_WITH_BUILD_FMT = '{PACKAGE_NAME}_v%d.%d.%d_build%d'

  # Formatting string with the positional arguments for a release package file name.
  # If specified, then the other formatting parameters are ignored. The free format can have any
  # of the following arguments:
  # {0} - major, {1} - minor, {2} - build, {3} - revision.
  RELEASE_NAME_FREE_FORMAT = None

  # Definition of the main release structure:
  # - KEY is a path in the {RELEASE} folder. Keys are sorted before handling.
  #   - If path starts from '/' then it's counted from release 'GameData' folder.
  #   - If doesn't start from '/' then it's counted from {RELEASE_MOD_FOLDER}.
  #   - It's ok to upcast the folder. E.g. "{RELEASE_MOD_FOLDER}/.." will resolve into release
  #     "GameData" folder. The resulted absolute folder must stay within {RELEASE} folder, or else
  #     an error is thrown.
  # - VALUE is an array of Python glob.glob path patterns to find copy sources. Special prefixes
  #   are allowed to change behavior and interpretation of the path item:
  #   - If starts with '?' then it's allowed for the glob() pattern to return no files. Without
  #     this prefix empty result will be treated as error.
  #   - If prefixed with '-' then pattern specifies files that need to be deleted from the
  #     *release* folder. Root, upcast, and downcast modifiers are not allowed. The files must
  #     live exactly in the folder specified by the KEY.
  #
  #   For the source paths root is counted from {PROJECT_ROOT}. Relative paths are resolved to
  #   {SOURCE}.
  #
  # NOTE. Folder for the KEY is not created if no files were found in any of the
  #    copy patterns.
  STRUCTURE = {}

  # Settings that can be overridden from the JSON file.
  __JSON_VALUES = [
    'ARCHIVE_DEST',
    'ASSEMBLY_INFO_FILE',
    'COMPILED_BINARY',
    'MINIAVC_VERSION_FILE',
    'PACKAGE_NAME',
    'PROJECT_ROOT',
    'RELEASE',
    'RELEASE_MOD_FOLDER',
    'RELEASE_NAME_FMT',
    'RELEASE_NAME_WITH_BUILD_FMT',
    'RELEASE_NAME_FREE_FORMAT',
    'SHELL_COMPILE_BINARY_SCRIPT',
    'SOURCE',
    'STRUCTURE',
  ]

  # Absolute path to the project (repository).
  # Do not change or use it outside of the script.
  __ABS_PROJECT_ROOT = None

  # Absolute path to either script's folder or JSON file folder when one used.
  # Do not change or use it outside of the script.
  __ABS_SCRIPT_ROOT = None


  # Runs all the steps and produces a release ZIP.
  # If overwrite_existing is not specified and there is a ZIP with the same name then this method
  # fails.
  def MakeRelease(self, make_archive_zip, overwrite_existing=False):

    if not self.__ABS_SCRIPT_ROOT:
      self.__ABS_SCRIPT_ROOT = os.path.dirname(os.path.abspath(__file__))
    self.__ResolveProjectRoot()

    self.__Step_CompileBinary()
    self.__Step_CleanupReleaseFolder()
    self.__Step_ExtractVersion()
    self.__Step_UpdateVersionInSources()
    self.__Step_MakeFoldersStructure()
    if make_archive_zip:    
      self.__Step_MakePackage(overwrite_existing)
    else:
      print 'No package requested, skipping.'
    print 'SUCCESS!'


  # Loads main build settings from a JSON file.
  def LoadSettingsFromJson(self, file_name):
    abs_file_name = os.path.abspath(file_name)
    print 'Load settings from JSON file:', abs_file_name
    with open(abs_file_name) as fp:
      content = json.load(fp);
    self.__CheckSchemaVersion(content.get('JSON_SCHEMA_VERSION'))
    for (key, value) in content.iteritems():
      if key in self.__JSON_VALUES:
        setattr(self, key, value)
      elif key != 'JSON_SCHEMA_VERSION':
        print 'ERROR: Unknown key in JSON:', key
        print '\nAllowed keys are:\n', '\n'.join(self.__JSON_VALUES)
        exit(-1)
    self.__ABS_SCRIPT_ROOT = os.path.dirname(abs_file_name)


  # Makes and returns release folder.
  def __GetAbsReleaseFolder(self):
    return os.path.join(self.__ABS_PROJECT_ROOT, self.__ParseMacros(self.RELEASE))


  # Resolves all macros in the string. Macro key should be enclosed in {} brackets. When used
  # in path this key will get resolved to an attribute value of builder class. E.g. value
  # '{PACKAGE_NAME}' will resolve to the value of {@code builder.PACKAGE_NAME} attribute. Macros
  # can be nested as long as it doesn't produce endless recursion.
  def __ParseMacros(self, value):
    parents = []

    def get_macro_value(key):
      if key in parents:
        print 'ERRROR: Recursive macro "%s": key "%s" needs self to get resolved' % (value, key)
        exit(-1)
      parents.append(key)
      if key.startswith('__'):
        print 'ERROR: Restricted macro key:', key
        exit(-1)
      if not hasattr(self, key):
        print 'ERROR: Unknown macro key:', key
        exit(-1)
      return getattr(self, key) or '{%s=>NULL}' % key

    def parse_macros(value):
      return re.sub(
          r'\{(\D\w*)}',
          lambda x: parse_macros(get_macro_value(x.group(1))),
          value)

    return parse_macros(value)

  # Expands source path into an absolute path. It does macros expansion as well.
  # Absolute path (started with '/') is counted from {PROJECT_ROOT}. Relative path is counted
  # from {SOURCE}. Path upcasting is allowed but not above the {PROJECT_ROOT}. Otherwise, script
  # aborts with an error.
  def __MakeSrcPath(self, rel_path, allow_unresolved=False):
    abs_root = self.__ABS_PROJECT_ROOT
    path_parts = [abs_root]
    rel_path = self.__ParseMacros(rel_path)
    if not rel_path or rel_path[0] != '/':
      path_parts.append(self.__ParseMacros(self.SOURCE))
    path_parts.append(rel_path)
    abs_path = os.path.normpath(os.sep.join(path_parts))

    if '=>NULL' in abs_path and not allow_unresolved:
      print 'ERROR: Cannot construct source path due to unresolved component(s):', abs_path
      exit(-1)
    elif os.path.relpath(abs_path, abs_root).startswith('..'):
      print 'ERROR: Source path is outside of project root:', abs_path
      exit(-1)

    return abs_path
    

  # Expands destination path parts into an absolute path. It does macros expansion as well.
  # Absolute paths (started with '/') are counted from {RELEASE_GAMEDATA_FOLDER}. Relative paths
  # are counted from {RELEASE_MOD_FOLDER}. Path upcasting is allowed but not above the root. If
  # resulted source path tries to address an entity outside of the root then an exception is risen.
  def __MakeDestPath(self, rel_path, allow_unresolved=False):
    abs_root = os.path.normpath(
        os.path.join(self.__ABS_PROJECT_ROOT, self.__ParseMacros('{RELEASE}/GameData')))
    path_parts = [abs_root]
    rel_path = self.__ParseMacros(rel_path)
    if not rel_path or rel_path[0] != '/':
      path_parts.append(self.__ParseMacros(self.RELEASE_MOD_FOLDER))
    path_parts.append(rel_path)
    abs_path = os.path.normpath(os.sep.join(path_parts))

    if '=>NULL' in abs_path and not allow_unresolved:
      print 'ERROR: Cannot construct release path due to unresolved component(s):', abs_path
      exit(-1)
    elif os.path.relpath(abs_path, abs_root).startswith('..'):
      print 'ERROR: Destination path is outside of GameData release folder:', abs_path, abs_root
      exit(-1)
    return abs_path

  
  # Checks if JSON settings file can be handled by this version of the script.
  def __CheckSchemaVersion(self, schema_version):
    if not schema_version:
      print 'ERROR: JSON schema is not defined'
      exit(-1)
    major, minor = [int(x) for x in schema_version.split('.')]
    supported_major, supported_minor = [int(x) for x in SUPPORTED_JSON_SCHEMA_VERSION.split('.')]
    if major != supported_major or minor > supported_minor:
      print 'ERROR: Unsupported schema version %s' % schema_version
      exit(-1)


  # Resolves absolute path to the project root and adjusts {PROJECT_ROOT} if needed.
  def __ResolveProjectRoot(self):
    if self.PROJECT_ROOT == '#github':
      repository_path = self.__ABS_SCRIPT_ROOT
      i = 50  # Reasonably high nested level value to prevent endless loops.
      while not os.path.exists(os.path.join(repository_path, '.git')):
        i -= 1
        repository_path = os.path.abspath(os.path.join(repository_path, '..'))
        if os.path.relpath(repository_path, '/') == '.' or not i:
          print 'ERROR: Cannot find GitHub repository for:', self.__ABS_SCRIPT_ROOT
          exit(-1)
      self.__ABS_PROJECT_ROOT = repository_path
      self.PROJECT_ROOT = os.path.relpath(self.__ABS_PROJECT_ROOT, self.__ABS_SCRIPT_ROOT)
      print 'Found GitHub repository at: %s (PROJECT_ROOT=%s)' % (
          repository_path, self.PROJECT_ROOT)
    else:
      self.__ABS_PROJECT_ROOT = os.path.join(self.__ABS_SCRIPT_ROOT, self.PROJECT_ROOT)
      print 'Set repository path from settings: %s (PROJECT_ROOT=%s)' % (
          self.__ABS_PROJECT_ROOT, self.PROJECT_ROOT)


  # Makes the binary.
  def __Step_CompileBinary(self):
    os.chdir(self.__ABS_SCRIPT_ROOT)
    binary_path =  self.__MakeSrcPath(self.COMPILED_BINARY)
    print 'Compiling sources in PROD mode...'
    code = subprocess.call(
        [os.path.join(self.__ABS_SCRIPT_ROOT, self.SHELL_COMPILE_BINARY_SCRIPT)])
    if code != 0 or not os.path.exists(binary_path):
      print 'ERROR: Compilation failed. Cannot find target DLL:', binary_path
      exit(code)
  
  
  # Purges any existed files in the release folder.
  def __Step_CleanupReleaseFolder(self):
    path = self.__GetAbsReleaseFolder()
    print 'Cleanup release folder...'
    self.__OsSafeDeleteFromDest(path)


  # Creates whole release structure and copies the required files.
  def __Step_MakeFoldersStructure(self):

    def target_cmp_fn(x, y):
      if x and x[0] == '/' and (not y or y[0] != '/'):
        return -1
      if y and y[0] == '/' and (not x or x[0] != '/'):
        return 1
      return cmp(x, y)

    print '=== START: Building release structure:'
    sorted_targets = sorted(
        self.STRUCTURE.iteritems(), key=lambda x: x[0], cmp=target_cmp_fn)
    for (dest_folder, src_patterns) in sorted_targets:
      dest_path = self.__MakeDestPath(dest_folder)
      print 'Release folder:', dest_path
      copy_sources = None
      drop_patterns = []
      for src_pattern in src_patterns:
        allow_no_matches = False
        is_drop_pattern = False

        if src_pattern[0] == '?':
          allow_no_matches = True
          pattern = self.__MakeSrcPath(src_pattern[1:], allow_unresolved=True)
          if '=>NULL' in pattern:
            print '=> skip unresolved copy pattern:', pattern
            continue
        elif src_pattern[0] == '-':
          is_drop_pattern = True
          drop_patterns.append(src_pattern[1:])
          continue
        else:
          pattern = self.__MakeSrcPath(src_pattern)

        entry_sources = glob.glob(pattern)
        if not entry_sources:
          if allow_no_matches:
            print '=> skip copy pattern "%s" since no matches found' % pattern
          else:
            print 'ERROR: Nothing is found for pattern:', pattern
            print 'HINT: If this pattern is allowed to return nothing then add prefix "?"'
            exit(-1)
        if copy_sources is None:
          copy_sources = []
        copy_sources.extend(entry_sources)

      # Copy files.
      if copy_sources is not None:
        for source in copy_sources:
          self.__OsSafeCopyToRelease(source, dest_path)
        if not copy_sources:
          print '=> skip empty folder:', source

      # Drop files.
      for pattern in drop_patterns:
        cleanup_path = self.__MakeDestPath(os.path.join(dest_folder, pattern))
        relpath = os.path.relpath(cleanup_path, dest_path)
        head, _ = os.path.split(relpath)
        if relpath.startswith('..') or head:
          print ('ERROR: Cleanup pattern must designate an entity in the target folder:'
                 ' pattern=%s, target=%s' % (cleanup_path, dest_path))
          exit(-1)
        targets = glob.glob(cleanup_path)
        if targets:
          for target in targets:
            self.__OsSafeDeleteFromDest(target)
        else:
          print '=> skip cleanup pattern "%s" since no matches found' % cleanup_path

    print '=== END: Building release structure'


  # Extracts version number of the release from the sources.
  def __Step_ExtractVersion(self):
    file_path = self.__MakeSrcPath(self.ASSEMBLY_INFO_FILE)
    print 'Extract release version...'
    if self.VERSION:
      print '=> already set:', self.VERSION
      return
    print '=> AssemblyInfo:', file_path
    with open(file_path) as f:
      content = f.readlines()
    for line in content:
      if line.lstrip().startswith('//'):
        continue
      # Expect: [assembly: AssemblyVersion("X.Y.Z")]
      matches = re.match(
          r'\[assembly: AssemblyVersion.*\("(\d+)\.(\d+)\.(\*|\d+)(.(\*|\d+))?"\)\]', line)
      if matches:
        self.__MakeVersion(
            matches.group(1), matches.group(2), matches.group(3), matches.group(5) or 0)
        break
        
    if self.VERSION is None:
      print 'ERROR: Cannot extract version from: %s' % file_path
      exit(-1)
    print '=> found version: v%d.%d, patch %d, build %d' % self.VERSION


  # Updates the source files with the version info.
  def __Step_UpdateVersionInSources(self):
    print 'Update MiniAVC info...'
    if not self.MINIAVC_VERSION_FILE:
      print '=> no version file defined, skipping'
      return
    version_file = self.__MakeSrcPath(self.MINIAVC_VERSION_FILE)
    print '=> version file:', version_file
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
  
  
  # Creates a package for re-destribution.
  def __Step_MakePackage(self, overwrite_existing):
    print 'Making %s package...' % (self.PACKAGE_NAME or '<NONE>')
    if self.RELEASE_NAME_FREE_FORMAT:
      release_name = self.__ParseMacros(self.RELEASE_NAME_FREE_FORMAT).format(*self.VERSION)
    else:
      release_name = (self.VERSION[3]
          and self.__ParseMacros(self.RELEASE_NAME_WITH_BUILD_FMT % self.VERSION)
          or self.__ParseMacros(self.RELEASE_NAME_FMT % self.VERSION[:3]))
    package_file_name = self.__MakeSrcPath(os.path.join('/', self.ARCHIVE_DEST, release_name))
    archive_name = package_file_name + '.zip'
    if os.path.exists(archive_name): 
      if not overwrite_existing:
        print 'ERROR: Package for this version already exists: %s' % archive_name
        exit(-1)
      print '=> package already exists. DELETING.'
      os.remove(archive_name)
    shutil.make_archive(package_file_name, 'zip', self.__GetAbsReleaseFolder(), 'GameData')
    print '=> stored in:', package_file_name


  # Fills VERSION given the string or int compinents. The patch and build could be "*".
  def __MakeVersion(self, major, minor, patch, build):
    # Get build/rev from the binary if it's auto generated.
    if build == '*' or patch == '*':
      filename = self.__MakeSrcPath(self.COMPILED_BINARY)
      version = self.__GetFileInfo(filename) or ''
      parts = version.split('.')
      if patch == '*' and len(parts) >= 3:
        patch = parts[2]
      if len(parts) >= 4:
        build = parts[3]
    # Handle fallbacks in case of the version wasn't extracted.
    if patch == '*':
      print 'WARNING: Couldn\'t resolve version PATCH, fallback to 0'
      patch = 0
    if build == '*':
      print 'WARNING: Couldn\'t resolve version BUILD, fallback to 0'
      build = 0
    # Fill the version
    self.VERSION = (int(major), int(minor), int(patch), int(build))

 
  # Checks if path doesn't try to address file above the root. All path arguments can contain
  # macros.
  #
  # @param test_path Path to check.
  # @param chroot Optional path to use as root. When not specified root is {PROJECT_ROOT}.
  # @param action Name of the action that needs the check. It will be reported in case of negative
  #     result.
  # @return Absolute path for {@code test_path}.
  def __CheckChroot(self, test_path, chroot=None, action=None):
    abs_test_path = os.path.abspath(self.__ParseMacros(test_path))
    abs_chroot = os.path.abspath(os.path.join(self.__ParseMacros(chroot or self.PROJECT_ROOT)))
    rel_path = os.path.relpath(abs_test_path, abs_chroot)
    if rel_path.startswith('..'):
      print 'ERROR: Action %s is not permitted above the root: %s (root=%s)' % (
          action, abs_test_path, abs_chroot)
      raise RuntimeError('Path is not secure!')
    return abs_test_path


  # Creates all elements in the path if they don't exist. Ensures the folders are created within
  # {PROJECT_ROOT}.
  # Folder name supports macros.
  def __OsSafeMakedirs(self, folder):
    abs_path = self.__CheckChroot(folder, action='MAKE PATH')
    if not os.path.isdir(abs_path):
      print '=> create folder:', abs_path
      os.makedirs(abs_path)


  # Copies file or folder. Ensures that source is defined within {PROJECT_ROOT} and target is in
  # {RELEASE}.
  # Both {@code src} and {@code dest} must be absolute or relative OS paths. No macros supported.
  def __OsSafeCopyToRelease(self, src, dest, source_must_exist=True):
    abs_src = self.__CheckChroot(src, action='COPY-FROM')
    abs_dest = self.__CheckChroot(dest, chroot=self.__GetAbsReleaseFolder(), action='COPY-TO')
    if os.path.isfile(abs_src):
      self.__OsSafeMakedirs(abs_dest)
      print '=> copy file:', abs_src
      shutil.copy(abs_src, abs_dest)
    elif os.path.isdir(abs_src):
      print '=> copy folder:', abs_src
      shutil.copytree(abs_src, os.path.join(abs_dest, os.path.basename(abs_src)))
    else:
      if source_must_exist:
        print 'ERROR: Source path not found"', abs_src
        exit(-1)
      print "=> skipping:", abs_src


  # Copies file or folder. Ensures that path is defined within {RELEASE}.
  def __OsSafeDeleteFromDest(self, path):
    abs_path = self.__CheckChroot(path, chroot=self.__GetAbsReleaseFolder(), action='DELETE')
    if os.path.isfile(abs_path):
      print '=> drop file:', abs_path
      os.unlink(abs_path)
    else:
      print '=> drop folder:', abs_path
      shutil.rmtree(abs_path, True)

  # Extracts information from a DLL file.
  def __GetFileInfo(self, filename):
    filename = u'' + filename  # Ensure it's wide-string encoding.
    size = ctypes.windll.version.GetFileVersionInfoSizeW(filename, None)
    if not size:
      return None
    res = ctypes.create_string_buffer(size)
    if not ctypes.windll.version.GetFileVersionInfoW(filename, None, size, res):
      return None
    l = ctypes.c_uint()
    r = ctypes.c_void_p()
    if not ctypes.windll.version.VerQueryValueA(
        res, '\\VarFileInfo\\Translation', ctypes.byref(r), ctypes.byref(l)):
      return None
    if not l.value:
      return None
    codepages = array.array('H', ctypes.string_at(r.value, l.value))
    codepage = tuple(codepages[:2].tolist())
    r = ctypes.c_char_p()
    if not ctypes.windll.version.VerQueryValueA(
        res, ('\\StringFileInfo\\%04x%04x\\FileVersion') % codepage,
        ctypes.byref(r), ctypes.byref(l)):
      return None
    return ctypes.string_at(r)


# Default JSON settings file to search in the current folder when "-J"
# argument is supplied.
RELEASE_JSON_FILE = 'release_setup.json'


# Custom method to setup builder when JSON is disabled.
def SetupBuildVariables(builder):
  raise NotImplementedError(
    'When RELEASE_JSON_FILE is not set SetupBuildVariables() method must be'
    ' implemented');


def main(argv):
  global RELEASE_JSON_FILE

  parser = argparse.ArgumentParser(
      description='KSP mod release builder.',
      fromfile_prefix_chars='@',
      formatter_class=argparse.RawDescriptionHelpFormatter,
      epilog=textwrap.dedent('''
          Arguments can be provided via a file:
            %(prog)s @input.txt

          Examples:
            %(prog)s -c
            %(prog)s -Jpo
            %(prog)s -J -p -o
            %(prog)s -j my_mod_settings.json
            %(prog)s -j c:/repositories/ksp/build/my_mod_settings.json
      '''))
  source_group = parser.add_mutually_exclusive_group(required=True)
  source_group.add_argument(
      '-c', action='store_true',
      help='force code setup mode')
  source_group.add_argument(
      '-j', action='store', metavar='<filename>',
      help='''load settings from JSON file. In this mode script doesn\'t need
         to be located in the building repository. Working directory will be
         counted from the file path.''')
  source_group.add_argument(
      '-J', action='store_true',
      help=('find file <%s> in the working directory and load settings from it'
            % RELEASE_JSON_FILE))
  parser.add_argument(
      '-p', action='store_true',
      help='request creating of the release ZIP archive')
  parser.add_argument(
      '-o', action='store_true',
      help='allow overwriting release archive when one exists')
  parser.add_argument(
      '--version', action='version',
      version='%%(prog)s v%s\nJSON schema version v%s ' % (
          SCRIPT_VERSION, SUPPORTED_JSON_SCHEMA_VERSION))
  opts = vars(parser.parse_args(argv[1:]))

  builder = Builder()
  if opts['j']:
    builder.LoadSettingsFromJson(opts['j'])
  elif opts['J']:
    builder.LoadSettingsFromJson(RELEASE_JSON_FILE)
  else:
    print 'Setting up from the code...'
    SetupBuildVariables(builder)
  builder.MakeRelease(opts['p'], opts['o'])
  exit(-2)


main(sys.argv)

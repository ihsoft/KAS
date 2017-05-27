# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# Version: 1.1

# A very simple script to produce a .ZIP archive with the product distribution.

import getopt
import glob
import json
import logging
import os
import os.path
import re
import shutil
import subprocess
import sys
import time
import collections

ZIP_BINARY = 'C:/Program Files/7-Zip/7z.exe'
PACKAGE_TITLE = 'Kerbal Attachment System'

# SRC configs.
SRC = '..'
# Extract version number from here. See ExtractVersion() method.
SRC_VERSIONS_FILE = SRC + '/Source/Properties/AssemblyInfo.cs'
# An executable which will be called to build the project's binaraies in release mode.
SRC_COMPILE_BINARY_SCRIPT = 'make_binary-1.0.cmd'
# Path to the release's binary. If it doesn't exist then no release.
SRC_COMPILED_BINARY = '/Source/bin/Release/KAS-1.0.dll'


# DEST configs.
# A path where releaae structure will be constructed.
DEST = '../Release'
# A path to place resulted ZIP file. It must exist.
DEST_RELEASES = '..'
# A format string which accepts VERSION as argument and return distribution
# file name with no extension.
DEST_RELEASE_NAME_FMT = 'KAS-1.0_v%d.%d.%d'
# A file name format for releases with build field other than zero.
DEST_RELEASE_NAME_WITH_BUILD_FMT = 'KAS-1.0_v%d.%d.%d_build%d'


# Sources to be updated post release (see UpdateVersionInSources).
# All paths must be full.
SRC_REPOSITORY_VERSION_FILE = SRC + '/Source/KAS.version'


# Destinations to be updated post release (see UpdateVersionInDestinations).
# All paths must be full.
DEST_PLUGIN_VERSION_COPY = (
    SRC_REPOSITORY_VERSION_FILE, DEST + '/GameData/KAS-1.0/Plugins/KAS.version')


# Keys are paths in DEST, values are paths/patterns in SRC.
# When value is a string then the entire source follder is copied.
# Destination folder is not created recursively so, if you need to copy a file
# into "a/b/c" folder then you need three keys: "a", "a/b", and 'a/b/c".
STRUCTURE = collections.OrderedDict({
  '/': [],  # Create release root folder.
  '/GameData' : [],  # Create parent folder.
  '/GameData/KAS-1.0': [
    '/LICENSE-1.0.md',
  ],
  '/GameData/KAS-1.0/Parts' : '/Parts',
  '-/GameData/KAS-1.0/Parts' : '/Winch1',
  '/GameData/KAS-1.0/Patches' : '/Patches',
  '/GameData/KAS-1.0/Models' : '/Models',
  '-/GameData/KAS-1.0/Models' : '/**/*.psd',  # Drop texture sources.
  '/GameData/KAS-1.0/Sounds' : [
    '/Sounds/broke.ogg',
    '/Sounds/plugdocked.ogg',
    '/Sounds/unplugdocked.ogg',
    '/Sounds/grappleAttachEva.ogg',
    '/Sounds/grappleDetach.ogg',
  ],
  '/GameData/KAS-1.0/Textures' : [
    '/Textures/piston180.png',
    '/Textures/KASFilterIcon.png',
    '/Textures/cable.png',
    '/Textures/ProceduralSteelCable.tga',
    '/Textures/ProceduralSteelCableNRM.dds',
  ],
  '/GameData/KAS-1.0/Plugins' : [
    '/Binaries/MiniAVC.dll',
    '/Binaries/KSPDev_Utils.0.23.0.dll',
    '/Binaries/KSPDev_Utils_License.md',
    '/Source/bin/Release/KAS-1.0.dll',
  ],
})

VERSION = None

# Argument values.
MAKE_PACKAGE = False
OVERWRITE_PACKAGE = False


def CopyByRegex(src_dir, dst_dir, pattern):
  for name in os.listdir(src_dir):
    if name == 'CVS':
      continue
    src_file_path = os.path.join(src_dir, name)
    if os.path.isfile(src_file_path) and re.search(pattern, name):
      print 'Copying:', src_file_path
      shutil.copy(src_file_path, dst_dir)


# Makes the binary.
def CompileBinary():
  if not SRC_COMPILED_BINARY is None:
    binary_path = SRC + SRC_COMPILED_BINARY
    if os.path.exists(binary_path):
      os.unlink(binary_path)
  print 'Compiling the sources in PROD mode...'
  code = subprocess.call([SRC_COMPILE_BINARY_SCRIPT])

  if (code != 0
      or not SRC_COMPILED_BINARY is None
      and not os.path.exists(SRC + SRC_COMPILED_BINARY)):
    print 'ERROR: Compilation failed.'
    exit(code)


# Purges any existed files in the release folder.
def CleanupReleaseFolder():
  print 'Cleanup release folder...'
  shutil.rmtree(DEST, True)


# Creates whole release structure and copies the required files.
def MakeFoldersStructure():
  print 'Make release folders structure...'
  folders = sorted(STRUCTURE.keys(), key=lambda x: x[0] != '-' and x or x[1:] + 'Z')
  # Make.
  for folder in folders:
    if folder.startswith('-'):
      # Drop files/directories.
      del_path = DEST + folder[1:] + STRUCTURE[folder]
      print 'Drop targets by pattern: %s' % del_path
      for file_name in glob.glob(del_path):
        if os.path.isfile(file_name):
          print 'Dropping file "%s"' % file_name
          os.unlink(file_name)
        else:
          print 'Dropping directory "%s"' % file_name
          shutil.rmtree(file_name, True)
      continue

    # Copy files.
    dest_path = DEST + folder 
    sources = STRUCTURE[folder]
    if not isinstance(sources, list):
      src_path = SRC + sources
      print 'Copying folder "%s" into "%s"' % (src_path, dest_path)
      shutil.copytree(src_path, dest_path)
    else:
      print 'Making folder "%s"' % dest_path
      os.mkdir(dest_path)
      for file_path in STRUCTURE[folder]:
        source_path = SRC + file_path
        if file_path.endswith('/*'):
          print 'Copying files "%s" into folder "%s"' % (source_path, dest_path)
          CopyByRegex(SRC + file_path[:-2], dest_path, '.+')
        else:
          print 'Copying file "%s" into folder "%s"' % (source_path, dest_path)
          shutil.copy(source_path, dest_path)


# Extarcts version number of the release from the sources.
def ExtractVersion():
  global VERSION
  with open(SRC_VERSIONS_FILE) as f:
    content = f.readlines()
  for line in content:
    if line.lstrip().startswith('//'):
      continue
    # Expect: [assembly: AssemblyVersion("X.Y.Z")]
    matches = re.match(r'\[assembly: AssemblyVersion.*\("(\d+)\.(\d+)\.(\d+)(.(\d+))?"\)\]', line)
    if matches:
      VERSION = (int(matches.group(1)),  # MAJOR
                 int(matches.group(2)),  # MINOR
                 int(matches.group(3)),  # PATCH
                 int(matches.group(5) or 0))  # BUILD, optional.
      break
      
  if VERSION is None:
    print 'ERROR: Cannot extract version from: %s' % SRC_VERSIONS_FILE
    exit(-1)
  print 'Releasing version: v%d.%d.%d build %d' % VERSION


# Updates the destination files with the version info.
def UpdateVersionInDestinations():
  print 'Copy plugin version file "%s" into "%s"' % (
      DEST_PLUGIN_VERSION_COPY[0], DEST_PLUGIN_VERSION_COPY[1])
  shutil.copy(DEST_PLUGIN_VERSION_COPY[0], DEST_PLUGIN_VERSION_COPY[1])


# Updates the source files with the version info.
def UpdateVersionInSources():
  print 'Update repository version file:', SRC_REPOSITORY_VERSION_FILE
  UpdateVersionInJsonFile_(SRC_REPOSITORY_VERSION_FILE)


def UpdateVersionInJsonFile_(name):
  with open(name) as fp:
    content = json.load(fp);
  content['VERSION'] = {}
  content['VERSION']['MAJOR'] = VERSION[0]
  content['VERSION']['MINOR'] = VERSION[1]
  content['VERSION']['PATCH'] = VERSION[2]
  content['VERSION']['BUILD'] = VERSION[3]
  with open(name, 'w') as fp:
    json.dump(content, fp, indent=4, sort_keys=True)


def MakeReleaseFileName():
  if VERSION[3]:
    return DEST_RELEASE_NAME_WITH_BUILD_FMT % VERSION
  else:
    return DEST_RELEASE_NAME_FMT % VERSION[:3]


# Creates a package for re-destribution.
def MakePackage():
  if not MAKE_PACKAGE:
    print 'No package requested, skipping.'
    return

  release_name = MakeReleaseFileName();
  package_file_name = '%s/%s.zip' % (DEST_RELEASES, release_name)
  if os.path.exists(package_file_name):
    if not OVERWRITE_PACKAGE:
      print 'ERROR: Package for this version already exists.'
      exit(-1)

    print 'WARNING: Package already exists. Deleting.'
    os.remove(package_file_name)

  print 'Making %s package...' % PACKAGE_TITLE
  code = subprocess.call([
      ZIP_BINARY,
      'a',
      package_file_name,
      DEST + '/*'])
  if code != 0:
    print 'ERROR: Failed to make the package.'
    exit(code)


def main(argv):
  global MAKE_PACKAGE, OVERWRITE_PACKAGE, VERSION

  try:
    opts, _ = getopt.getopt(argv[1:], 'po', )
  except getopt.GetoptError:
    print 'make_release.py [-po]'
    exit(2)
  opts = dict(opts)
  MAKE_PACKAGE = '-p' in opts
  OVERWRITE_PACKAGE = '-o' in opts

  ExtractVersion()
  CompileBinary()
  CleanupReleaseFolder()
  MakeFoldersStructure()
  UpdateVersionInSources()
  UpdateVersionInDestinations()
  MakePackage()
  print 'SUCCESS!'

main(sys.argv)

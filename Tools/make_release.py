# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# Version: 1.0
# GitHub: https://github.com/ihsoft/KerbalReleaseBuilder

# Template runner for the builder script.

import getopt
import sys

import KspReleaseBuilder


# BEGIN: ====== ADJUST variables in this section before doing the release!

# See KspReleaseBuilder.Builder.SHELL_ZIP_BINARY
SHELL_ZIP_BINARY = 'C:/Program Files/7-Zip/7z.exe'

# See KspReleaseBuilder.Builder.SHELL_COMPILE_BINARY_SCRIPT
BUILD_SCRIPT = 'make_binary.cmd'

# Name of the file to read settings from. If set to None then method
# SetupBuildVariables() will be invoked to obtaine the builder settings.
RELEASE_JSON_FILE = 'release_setup.json'

# END: ====== ADJUST section ends here.


# Custom method to setup builder when JSON is disabled.
def SetupBuildVariables(builder):
  raise NotImplementedError(
    'When RELEASE_JSON_FILE is not set SetupBuildVariables() method must be'
    ' implemented');


def main(argv):
  try:
    opts, _ = getopt.getopt(argv[1:], 'po', )
  except getopt.GetoptError:
    print 'make_release.py [-po]'
    exit(2)
  opts = dict(opts)
  need_package = '-p' in opts
  overwrite_existing  = '-o' in opts

  builder = KspReleaseBuilder.Builder(BUILD_SCRIPT, SHELL_ZIP_BINARY)
  if RELEASE_JSON_FILE:
    builder.LoadSettingsFromJson(RELEASE_JSON_FILE)
  else:
    SetupBuildVariables(builder)
  builder.MakeRelease(need_package, overwrite_existing)


main(sys.argv)

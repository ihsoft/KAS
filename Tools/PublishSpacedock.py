# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev_ReleaseBuilder
# $version: 5
# $date: 10/28/2018

""" Script to publish releases to Spacedock.

Example:

  $ PublishSpacedock.py\
    --project=1906
    --changelog=CHANGELOG.md
    --versions=^1\.4\.4$
    --archive=./test_v1.5.zip

This script takes the release description from a CHANGELOG file. It assumes,
the topmost block of the lines till the first empty line is the description
of the latest release. If this is not the case, use the --changelog_breaker
parameter:

  --changelog_breaker=BreakHere

The script needs to know the mod version to report to Spacedock. By default,
it takes it from the release arhcive name, assuming it has format:
'*_v<version>.zip'. When it's different, you need to specify the extraction
RegExp:

  --version_extract=.+?_v(.+?)\\.zip

HINT. Passing all the arguments via the command line may be not convinient.
Not to mention the quotes and back slashes issues. To avoid all this burden,
simple put all the options into a text file and provide it to the script:

  $ PublishSpacedock.py @my_params.txt

Don't bother about escaping in this file. Anything, placed in a line goes to
the parameter value *as-is*. So you don't need to escape backslashes,
whitespaces, quotes, etc.

Example of the args file (you may copy it "as-is"):

  --project=123456
  --login=foo
  --changelog=CHANGELOG.md
  --version=1\.4\.
  --archive=./test_v1.5.zip

When the script is properly configured and ran, it will present the extracted
portion of CHANGELOG and the other release settings, which will be applied to
the project. Review them *CAREFULLY* before answering "y". The new versions
will be published right away!
"""
import atexit
import argparse
import getpass
import logging
import logging.config
import os.path
import re
import sys
import textwrap

from clients import SpacedockClient
from utils import ChangelogUtils


LOGGER = logging.getLogger()


def main(argv):
  global LOGGER

  if os.path.isfile('logging.conf'):
    logging.config.fileConfig('logging.conf')
    atexit.register(_Shutdown)
    LOGGER = logging.getLogger('publishApp')
  else:
    print 'Log config not found: logging.conf'

  LOGGER.info('Staring new SpaceDock session...')

  parser = argparse.ArgumentParser(
      description='Publishes the release to a Spacedock mod.',
      fromfile_prefix_chars='@',
      formatter_class=argparse.RawDescriptionHelpFormatter,
      epilog=textwrap.dedent('''
          Arguments can be provided via a file:
            %(prog)s @input.txt
      '''))
  parser.add_argument(
      '--project', action='store', metavar='<project ID>', required=True,
      help='''the ID of the project to publish to. To get it, go to the mod
          overview on Spacedock and extract the number from the URL:
          /mod/<project ID>/...''')
  parser.add_argument(
      '--changelog', action='store', metavar='<file path>', required=True,
      help='''the file to get the release description from. The top lines till
          the first empty line are taken. The description is expected to use
          the 'markdown' syntax.''')
  parser.add_argument(
      '--archive', action='store', metavar='<file path>', required=True,
      help='''the archive file to publish.''')
  parser.add_argument(
      '--ksp_version', action='store', metavar='<"latest" | regexp>',
      default='latest',
      help='''the RegExp pattern to match the target KSP version. If set to the
          keyword "latest", then the script will use the maximum version,
          known to Spacedock. [Default: %(default)s]''')
  parser.add_argument(
      '--changelog_breaker', action='store', metavar='<regexp>',
      default=r'^\s*$',
      help='''the RegExp to detect the end of the release description in the
          CHANGELOG. This expression is applied per the file line.
          [Default: %(default)s]''')
  parser.add_argument(
      '--version_extract', action='store', metavar='<regexp>',
      default='.+?_v(.+?)\\.zip',
      help='''the RegExp to extract the version tag from the archive name.
          [Default: %(default)s]''')
  parser.add_argument(
      '--login', action='store', metavar='<SD login>',
      help='''the login for the Spacedock account. If not set, then it will be
          asked in the command line.''')
  parser.add_argument(
      '--pass', action='store', metavar='<SD password>',
      help='''the password for the Spacedock account. If not set, then it will
          be asked in the command line.''')
  parser.add_argument(
      '--github', action='store', metavar='<GitHub>',
      help='''the GitHub project and user, separated by "/" symbol. Used when
          expanding the GitHub links. Example: "ihsoft/KIS"''')
  opts = vars(parser.parse_args(argv[1:]))

  mod_id = opts['project']

  versions_re = opts['ksp_version']
  if versions_re != 'latest':
    all_versions = map(
        lambda x: x['name'],
        SpacedockClient.GetKSPVersions(pattern=versions_re))
    if not all_versions:
      print 'ERROR: No versions found for RegExp: %s' % versions_re
      exit(-1)
    if len(all_versions) > 1:
      print 'ERROR: Multiple versions matched RegExp: %s' % all_versions
      exit(-1)
    game_version = all_versions[0]
  else:
    all_versions = sorted(
        SpacedockClient.GetKSPVersions(), key=lambda x: x['id'], reverse=True)
    if not all_versions:
      print 'ERROR: No versions found!'
      exit(-1)
    game_version = all_versions[0]['name']

  desc = ChangelogUtils.ExtractDescription(
      opts['changelog'], opts['changelog_breaker'])
  if opts['github']:
    desc = ChangelogUtils.ProcessGitHubLinks(desc, opts['github'])
  filename = opts['archive']

  parts = re.findall(opts['version_extract'], os.path.basename(filename))
  if len(parts) != 1:
    print 'ERROR: cannot extract version tag from file name: %s' % filename
    exit(-1)
  mod_version = parts[0]

  if not os.path.isfile(filename):
    print 'ERROR: Cannot find archive: %s' % filename
    exit(-1)

  try:
    mod_details = SpacedockClient.GetModDetails(mod_id)
  except SpacedockClient.AuthorizationRequiredError, ex:
    # The unpublished mods are not available via API (see issue #186).
    mod_details = { 'id': mod_id, 'name': '<UNPUBLISHED>' }

  # Verify the user's choice...
  print '======> BEGIN CHANGELOG:'
  print desc
  print '======> END CHANGELOG:'
  print
  print 'Mod name: %s (#%s)' % (mod_details['name'], mod_details['id'])
  print 'Upload file:', os.path.abspath(filename)
  print 'Mod version tag:', mod_version
  print 'Add for version:', game_version

  sys.stdout.write('\nContinue? [y/N]: ')
  choice = raw_input().lower()
  if choice != 'y' and choice != 'Y':
    LOGGER.info('ABORTED BY USER!')
    print 'ABORTED!'
    exit(-1)

  print 'Publishing the release...'

  # Init Spacedock client.
  login = opts['login']
  if not login:
    sys.stdout.write('Spacedock login: ')
    login = raw_input().lower()
  else:
    print 'Spacedock login:', login
  SpacedockClient.API_LOGIN = login

  password = opts['pass']
  if not password:
    password = getpass.getpass('Spacedock password: ')
  else:
    print 'Spacedock password: <PROVIDED>'
  SpacedockClient.API_PASS = password

  SpacedockClient.UploadFile(mod_id, filename, desc, mod_version, game_version)
  print 'DONE!'
  print 'The new version is now availabe and the followers are notified!'


def _Shutdown():
  """Finilizes the logging system."""
  LOGGER.info('Ending SpaceDock session...')
  logging.shutdown()


main(sys.argv)

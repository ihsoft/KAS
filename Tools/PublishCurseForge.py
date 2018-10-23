# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev_ReleaseBuilder
# $version: 2
# $date: 10/16/2018

""" Script to publish releases to Kerbal CurseForge.

First of all you have to obtain the API token. It will authorize the requests.
Once you have it, define how you'd like the target release be created.

Example:

  $ PublishCurseForge.py\
    --project=123456
    --token=11111111-2222-3333-4444-555555555555
    --changelog=CHANGELOG.md
    --versions=1\.4\.
    --archive=./test_v1.5.zip

This script takes the release description from a CHANGELOG file. It assumes,
the topmost block of the lines till the first empty line is the description
of the latest release. If this is not the case, use the --changelog_breaker
parameter:

  --changelog_breaker=BreakHere

By default, the script will name the reelase the same way as the archive.
However, you may request to change the behavior. For this, you need to specify
how to extract the versions specific tag from the archive file name. Then, you
need to provide a new pattern for the relase name.

  --tag_extract=.+?_(v.+?)\\.zip
  --title=NewName_{tag}\

Note, that "tag_extract" has a default value, which extracts the archive name
version suffix. E.g. if the name was "my_mod_v1.5.zip", then the tag will be
"v1.5".

HINT. Passing all the arguments via the command line may be not convinient.
Not to mention the quotes and back slashes issues. To avoid all this burden,
simple put all the options into a text file and provide it to the script:

  $ PublishCurseForge.py @my_params.txt

Don't bother about escaping in this file. Anything, placed in a line goes to
the parameter value *as-is*. So you don't need to escape backslashes,
whitespaces, quotes, etc.

Example of the args file (you may copy it "as-is"):

  --project=123456
  --token=11111111-2222-3333-4444-555555555555
  --changelog=CHANGELOG.md
  --versions=1\.4\.
  --archive=./test_v1.5.zip
  --title=MyMod "Awesome!" {tag}

When the script is properly configured and ran, it will present the extracted
portion of CHANGELOG and the other release settings, which will be applied to
the project. Review them *CAREFULLY* before answering "y". CurseForge doesn't
allow deleting the relases. Regardless to how bad was your attempt, it will
stay in the history forever (but you can archive it).
"""
import argparse
import atexit
import os.path
import logging
import logging.config
import re
import sys
import textwrap

from clients import CurseForgeClient


LOGGER = logging.getLogger()


def main(argv):
  global LOGGER

  if os.path.isfile('logging.conf'):
    logging.config.fileConfig('logging.conf')
    atexit.register(_Shutdown)
    LOGGER = logging.getLogger('publishApp')
  else:
    print 'Log config not found: logging.conf'

  LOGGER.info('Staring new CurseForge session...')

  parser = argparse.ArgumentParser(
      description='Publishes the release to a Kerbal CurseForge project.',
      fromfile_prefix_chars='@',
      formatter_class=argparse.RawDescriptionHelpFormatter,
      epilog=textwrap.dedent('''
          Arguments can be provided via a file:
            %(prog)s @input.txt
      '''))
  parser.add_argument(
      '--project', action='store', metavar='<project ID>', required=True,
      help='''the ID of the project to publish to. To get it, go to the project
          overview on CurseForge.''')
  parser.add_argument(
      '--token', action='store', metavar='<API token>', required=True,
      help='''the token to authorize in API. To obtain this token go to the
          mod's profile on Forge, choose: "Account/Preferences/My API
          Tokens"''')
  parser.add_argument(
      '--changelog', action='store', metavar='<file path>', required=True,
      help='''the file to get the release description from. The top lines till
          the first empty line are taken. The description is expected to use
          the 'markdown' syntax.''')
  parser.add_argument(
      '--archive', action='store', metavar='<file path>', required=True,
      help='''the archive file to publish.''')
  parser.add_argument(
      '--title', action='store', metavar='<pattern>',
      help='''the pattern to build the release name. Use placeholder {tag} for
          the version tag. If omitted, then the file name is used as the one.
          Example: "NewName_{tag}".''')
  parser.add_argument(
      '--versions', action='store',
      metavar='<"latest" | "latest_all_builds" | regexp>',
      default='latest_all_builds',
      help='''the pattern to match the target KSP versions. If "latest" is
         specified, then a single version is used - the most recent one. If
         "latest_all_builds" is specified, then all builds of the latest
         version are used (e.g. "1.4.*"). [Default: %(default)s]''')
  parser.add_argument(
      '--changelog_breaker', action='store', metavar='<regexp>',
      default=r'^\s*$',
      help='''the RegExp to detect the end of the release description in the
          CHANGELOG. This expression is applied per the file line.
          [Default: %(default)s]''')
  parser.add_argument(
      '--tag_extract', action='store', metavar='<regexp>',
      default='.+?_(v.+?)\\.zip',
      help='''the RegExp to extract the version tag from the archive name.
          [Default: %(default)s]''')
  opts = vars(parser.parse_args(argv[1:]))

  project_id = opts['project']

  # Init CurseForge client.
  CurseForgeClient.API_TOKEN = opts['token']

  versions_re = opts['versions']
  if versions_re == 'latest' or versions_re == 'latest_all_builds':
    game_versions = sorted(
        CurseForgeClient.GetKSPVersions(), key=lambda x: x['id'], reverse=True)
    if not game_versions:
      print 'ERROR: No versions found!'
      exit(-1)
    game_versions = map(lambda x: x['name'], game_versions)
    if versions_re == 'latest':
      game_versions = game_versions[0:1]
    else:
      prefix = re.match(r'^(.+?\..+?)(\..+)?$', game_versions[0]).group(1)
      game_versions = filter(lambda x: x.startswith(prefix), game_versions)
  else:
    game_versions = map(
        lambda x: x['name'],
        CurseForgeClient.GetKSPVersions(pattern=versions_re))
    if not game_versions:
      print 'ERROR: No versions found for RegExp: %s' % versions_re
      exit(-1)

  desc = _ExtractDescription(opts['changelog'], opts['changelog_breaker'])
  filename = opts['archive']

  if opts['title']:
    if not opts['tag_extract']:
      print ('ERROR: When title rewrite is requested, the "tag_extract"'
             ' parameter is required')
      exit(-1)
    parts = re.findall(opts['tag_extract'], os.path.basename(filename))
    if len(parts) != 1:
      print 'ERROR: cannot extract version tag from file name: %s' % filename
      exit(-1)
    title = opts['title'].format(tag=parts[0])
  else:
    title = os.path.splitext(os.path.basename(filename))[0]


  if not os.path.isfile(filename):
    print 'ERROR: Cannot find archive: %s' % filename
    exit(-1)

  project_details = CurseForgeClient.GetProjectDetails(project_id)

  # Verify the user's choice...
  print '======> BEGIN CHANGELOG:'
  print desc
  print '======> END CHANGELOG:'
  print
  print 'Project name: %s (#%s)' % (project_details['title'], project_id)
  print 'Upload file:', os.path.abspath(filename)
  print 'Name release as:', title
  print 'Add for versions:', ', '.join(game_versions)
  sys.stdout.write('\nContinue? [y/N]: ')
  choice = raw_input().lower()
  if choice != 'y' and choice != 'Y':
    LOGGER.info('ABORTED BY USER!')
    print 'ABORTED!'
    exit(-1)

  print 'Publishing the release...'
  CurseForgeClient.UploadFile(
      project_id, filename, desc, game_versions,
      title=title, release_type='release')
  print 'DONE!'
  print 'Watch for the verification status on CurseForge.'


def _Shutdown():
  """Finilizes the logging system."""
  LOGGER.info('Ending CurseForge session...')
  logging.shutdown()


def _ExtractDescription(changelog_file, breaker_re):
  """Helper method to extract the meaningful part of the release changelog."""
  with open(changelog_file, 'r') as f:
    lines= f.readlines()
  changelog = ''
  for line in lines:
    # Ignore any trailing empty lines.
    if not changelog and not line.strip():
      continue
    # Stop at the breaker.
    if re.match(breaker_re, line.strip()):
      break
    changelog += line
  return changelog.strip()


main(sys.argv)

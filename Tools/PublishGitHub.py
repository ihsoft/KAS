# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev_ReleaseBuilder
# $version: 1
# $date: 07/17/2018

""" Script to publish releases to GitHub.

First of all you have to obtain the API token. It will authorize the requests.
Once you have it, define how you'd like the target release be created.

Example:

  $ PublishCurseForge.py\
    --user=my_github_user\
    --repo=MyGitHubMod\
    --token=1111111111122222222222222333333333333333\
    --changelog=CHANGELOG.md\
    --archive=MyGitHubMod_v1.2.zip\
    --as_draft\
    --title=MyGitHubMod v{tag}

Note, the `--title` argument can be omitted. In this case the release will be
named as the archive file.

This script takes the release description from a CHANGELOG file. It assumes,
the topmost block of the lines till the first empty line is the description
of the latest release. If this is not the case, use the --changelog_breaker
parameter:

  --changelog_breaker=BreakHere

Every GitHub reelase must have a version tag. By default, it's extracted from
the archive name, using a default RegExp. If the archive's name is unusual,
then a custome RegExp needs to be specified:

  --tag_extract=MyName_v(.+?)

HINT. Passing all the arguments via the command line may be not convinient.
Not to mention the quotes and back slashes issues. To avoid all this burden,
simple put all the options into a text file and provide it to the script:

  $ PublishCurseForge.py @my_params.txt

Don't bother about escaping in this file. Anything, placed in a line goes to
the parameter value *as-is*. So you don't need to escape backslashes,
whitespaces, quotes, etc.

When the script is properly configured and ran, it will present the extracted
portion of CHANGELOG and the other release settings, which will be applied to
the release. Review them and answer "y" if the look OK to have the release
created.

If you've provided `--as_draft` argument, then the release will be created but
not published, and there will be no version tags assigned to the trunk. This is
the recommended way to use the publish script. It allows creating a release
before committing and syncing the final release changes to the trunk. In order
to have the release published, go to GitHub and edit the draft.
"""
import argparse
import atexit
import os.path
import logging
import logging.config
import re
import sys
import textwrap

from clients import GitHubClient


LOGGER = logging.getLogger()


def main(argv):
  global LOGGER

  if os.path.isfile('logging.conf'):
    logging.config.fileConfig('logging.conf')
    atexit.register(_Shutdown)
    LOGGER = logging.getLogger('publishApp')
  else:
    print 'Log config not found: logging.conf'

  LOGGER.info('Staring new GitHub session...')

  parser = argparse.ArgumentParser(
      description='Publishes the release to a GitHub repository.',
      fromfile_prefix_chars='@',
      formatter_class=argparse.RawDescriptionHelpFormatter,
      epilog=textwrap.dedent('''
          Arguments can be provided via a file:
            %(prog)s @input.txt
      '''))
  parser.add_argument(
      '--user', action='store', metavar='<GitHub user>', required=True,
      help='''the user name on GitHub''')
  parser.add_argument(
      '--repo', action='store', metavar='<GitHub repo>', required=True,
      help='''the repository name on GitHub''')
  parser.add_argument(
      '--token', action='store', metavar='<Personal Access Token>',
      required=True,
      help='''the token to authorize in API. To obtain this token go to the
          repository on GitHub, choose: "Settings/Developer settings/Personal
          access tokens". Generate a token with permision "repo".''')
  parser.add_argument(
      '--changelog', action='store', metavar='<file path>', required=True,
      help='''the file to get the release description from. The top lines till
          the first empty line are taken. The description is expected to use
          the 'markdown' syntax.''')
  parser.add_argument(
      '--archive', action='store', metavar='<file path>', required=True,
      help='''the archive file to publish.''')
  parser.add_argument(
      '--tag_extract', action='store', metavar='<regexp>',
      default='.+?_v(.+?)\\.zip',
      help='''the RegExp to extract the version tag from the archive name.
          [Default: %(default)s]''')
  parser.add_argument(
      '--changelog_breaker', action='store', metavar='<regexp>',
      default=r'^\s*$',
      help='''the RegExp to detect the end of the release description in the
          CHANGELOG. This expression is applied per the file line.
          [Default: %(default)s]''')
  parser.add_argument(
      '--title', action='store', metavar='<pattern>',
      help='''the pattern to build the release name. Use placeholder {tag} for
          the version tag. If omitted, then the file name is used as the one.
          Example: "NewName_{tag}".''')
  parser.add_argument(
      '--as_draft', action='store_true',
      help='''forces the created release to be draft''')
  opts = vars(parser.parse_args(argv[1:]))

  user = opts['user']
  repo = opts['repo']

  # Init CurseForge client.
  GitHubClient.API_TOKEN = opts['token']

  desc = _ExtractDescription(opts['changelog'], opts['changelog_breaker'])
  filename = opts['archive']
  as_draft = opts['as_draft']

  parts = re.findall(opts['tag_extract'], os.path.basename(filename))
  if len(parts) != 1:
    print 'ERROR: cannot extract version tag from file name: %s' % filename
    exit(-1)
  version_tag = parts[0]

  if opts['title']:
    title = opts['title'].format(tag=version_tag)
  else:
    title = os.path.splitext(os.path.basename(filename))[0]

  if not os.path.isfile(filename):
    print 'ERROR: Cannot find archive: %s' % filename
    exit(-1)

  # Verify the user's choice...
  print '======> BEGIN CHANGELOG:'
  print desc
  print '======> END CHANGELOG:'
  print
  print 'GitHub repo: %s/%s' % (user, repo)
  print 'Upload file:', os.path.abspath(filename)
  print 'Name release as:', title
  print 'Release status:', ('DRAFT' if as_draft else 'PUBLISHED')
  sys.stdout.write('\nContinue? [y/N]: ')
  choice = raw_input().lower()
  if choice != 'y' and choice != 'Y':
    LOGGER.info('ABORTED BY USER!')
    print 'ABORTED!'
    exit(-1)

  print 'Publishing the release...'
  GitHubClient.CreateRelease(
      user, repo, version_tag, title, desc,
      files=[{'filename': filename }],
      draft=opts['as_draft'])
  print 'DONE!'
  if opts['as_draft']:
    print 'You must publish the release to make it visible!'


def _Shutdown():
  """Finilizes the logging system."""
  LOGGER.info('Ending GitHub session...')
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


try:
  main(sys.argv)
except Exception, ex:
  LOGGER.exception(ex)

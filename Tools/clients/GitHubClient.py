# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev_ReleaseBuilder
# $version: 1
# $date: 07/17/2018

"""A client library to communicate with GitHub via API.

Example:
  import GitHubClient

  GitHubClient.API_TOKEN = '#token'
  GitHubClient.CreateRelease(
      'user', 'repo', '1.2.3', 'MyMod v1.2.3 - release', '# BLAH!',
      files=[{'filename': '/var/tmp/MyMod_v1.2.3.zip'}],
      draft=True)
"""
import json
import logging
import os.path
import mimetypes
import re
import urllib
import urllib2

from utils import FormDataUtil

import sys
if (sys.version_info.major < 2 or sys.version_info.minor < 7
    or sys.version_info.micro < 11):
  # GitHub needs SNI compatible client.
  print '!!!!! Python version 2.7.11+ is required for the GitHub client to work !!!!!\n'
  exit(-1)


# The personal access token to use to authenticate. To get one, read this:
# https://help.github.com/articles/creating-a-personal-access-token-for-the-command-line/
API_TOKEN = None

# Endpoint for all the API requests
API_BASE_URL = 'https://api.github.com'

# The actions paths.
API_CREATE_RELEASE_TMPL = '/repos/{owner}/{repo}/releases'

# Headers to provide to the API to specify the API version.
API_COMMON_HEADERS = {
    'Accept': 'application/vnd.github.v3+json',
    'User-Agent': 'KSPDev-Publish-Script',
    'Content-Type': 'application/json',
}

LOGGER = logging.getLogger('ApiClient')


class Error(Exception):
  """Genric API client error."""
  pass


class AuthorizationRequiredError(Error):
  """The method called requires authorization, but none has been provided."""
  pass


class BadCredentialsError(Error):
  """The provided authorization token is refused."""
  pass


class BadRequestError(Error):
  """The provided arguments cannot be passed to API."""
  pass


class BadResponseError(Error):
  """Generic error from the API endpoint."""
  pass


class BinaryFileObject:
  """Simple wrapper for a binary file for urllib2."""

  def __init__(self, filename):
    self.__size = int(os.stat(filename).st_size)
    self.__f = open(filename, 'rb')

  def read(self, blocksize):
    return self.__f.read(blocksize)

  def __len__(self):
    return self.__size


def ExpandURI(url_tmpl, **kwargs):
  """Expands a hypermedia template.

  This a simplest implementation of the Templates Level 1 that can only handle
  the GET URL parameters expansion. See RFC6570 for more details.

  Args:
    url_tmpl: The template.
    **kwargs: The arguments to expand. The None values are not expanded.
  Returns:
    The expanded URL string.
  """
  matches = list(re.finditer(r'\{\?(.+?)\}', url_tmpl))
  url = url_tmpl
  for match in reversed(matches):
    names = filter(
        lambda x: kwargs.get(x) is not None, match.group(1).split(','))
    fields = map(lambda x: (x, kwargs[x]), names)
    if fields:
      url = (url[:match.start()]
          + '?' + urllib.urlencode(fields) + url[match.end():])
  return url


def CreateRelease(user, repo, version_tag, name, changelog,
                  files=None, draft=False, prerelease=False):
  """Creates a GitHuib release and uploads the provided files to it.

  Args:
    user: The user name at GitHub.
    repo: The repository name.
    version_tag: The tag to apply to the code for this release.
    name: The title string of the release.
    changelog: The text (markup style) to provide as a description.
    files: An array of object that describe the files to attach. The object
        fields are:
        - "filename": the path to the local file to upload. It must be ZIP
          archive, no other types are supported.
        - [OPT] "name": the file name on GitHub. If omitted, then the local
          file name is used.
        - [OPT] "label": a user friendly name to present in the release
          description. If omitted, then "name" is used.
    draft: If set to True, then the release is created but not published.
    prerelease: If True, then the release is marked as "pre-release".
  Raises:
    AuthorizationRequiredError if authorization token is not set.
    BadCredentialsError if the provided authorization token is not valid.
    BadRequestError if the provided files are not ZIP archives.
    BadResponseError if the API has responded with error.
  """
  upload_files = []
  for item in files:
    filename = os.path.abspath(item['filename'])
    if not os.path.isfile(filename):
      raise BadRequestError('File not found: %s', filename)
    basename = os.path.basename(filename)
    content_type, _ = mimetypes.guess_type(
        basename, strict=False)
    if not content_type:
      raise BadRequestError('Cannot guess the MIME type for: ' + basename)
    upload_files.append({
        'filename': filename,
        'type': content_type,
        'name': item.get('name', basename),
        'label': item.get('label'),
    })

  url, headers = _GetAuthorizedEndpoint(
      API_CREATE_RELEASE_TMPL, owner=user, repo=repo)
  data = {
      'tag_name': version_tag,
      'name': name,
      'body': changelog,
      'prerelease': prerelease,
      'draft': draft,
  }
  release_resp, _ = _CallAPI(url, data, headers)
  LOGGER.debug('Created release: id=%d, draft=%s', release_resp['id'], draft)
  UploadFilesToRelease(release_resp['upload_url'], upload_files, headers)

  return release_resp


def UploadFilesToRelease(upload_url_tmpl, upload_files, headers):
  """Uploads the files to the release.

  Args:
    upload_url_tmpl: The remplate, provided by GitHub, for uplaoding the files
        into the release. See 'upload_url' field in the release response.
    upload_files: The list of files to upload. Each entry must be a dict;
        - 'filename' - the path to the file;
        - 'type' - the MIME type of the file;
        - 'name' - the name of the file;
        - 'label' - optional user friendly name of the file.
    headers: The headers to pass to th server. The authorzation session must
        be set.
  Returns:
    A list of response obejcts from API.
  Throws:
    Error if API responds with error.
  """
  results = []
  for item in upload_files:
    upload_url = ExpandURI(
        upload_url_tmpl, name=item['name'], label=item['label'])
    headers['Content-Type'] = item['type']
    data = BinaryFileObject(item['filename'])
    resp, _ = _CallAPI(upload_url, data, headers)
    LOGGER.debug('Added file: name=%s, id=%d', item['name'], resp['id'])
    results.append(resp)
  return results


def _MakeAPIUrl(action_path, **kwargs):
  """Makes a URL for the action."""
  return API_BASE_URL + action_path.format(**kwargs)


def _CallAPI(url, data, req_headers, raise_on_error=True):
  """Invokes the API call.

  Args:
    url: The full action URL.
    data: The object with the action params.
    req_headers: The headers dict to provide to the call. If None, then empty
        headers are sent.
    raise_on_error: Tells whether the call must raise anexception in case of
        non-200 response. If set to False, then <response dict> will be:
        { 'error': True, 'reason': <reason> }
  Returns:
    Tuple: <response dict>, <response headers>
  Raises:
    AuthorizationRequiredError if action requires authorization token, but it's
        wrong or not provided.
    BadResponseError in case of any other error.
  """
  LOGGER.debug('API call: %s', url)
  resp_obj = { 'error': True, 'reason': 'unknown' }
  headers = dict(API_COMMON_HEADERS)
  headers.update(req_headers)
  if headers.get('Content-Type') == 'application/json':
    data = json.dumps(data)
  try:
    request = urllib2.Request(url, data, headers=headers)
    response = urllib2.urlopen(request)
    resp_obj = json.loads(response.read())
    resp_headers = response.info().dict
    resp_headers = response.headers
  except urllib2.HTTPError as ex:
    resp_headers = ex.headers.dict
    resp_obj = { 'error': True, 'reason': '%d - %s' % (ex.code, ex.reason) }
    try:
      error_obj = json.loads(ex.read())
    except ValueError:
      raise BadResponseError(resp_obj['reason'])
    if error_obj.get('message'):
      resp_obj['reason'] = error_obj['message']
    if error_obj.get('errors'):
      resp_obj['reason'] = '%s: %s' % (
          resp_obj['reason'], str(error_obj['errors']))
    if ex.code in [401, 403]:
      raise AuthorizationRequiredError(resp_obj['reason'])
    if ex.code == 422:
      raise BadResponseError(resp_obj['reason'])

  if type(resp_obj) is dict and resp_obj.get('reason'):
    LOGGER.error('API call failed: %s', resp_obj['reason'])
    if raise_on_error:
      raise BadResponseError(resp_obj['reason'])
    return resp_obj, resp_headers
  return resp_obj, resp_headers


def _GetAuthorizedEndpoint(api_path, headers=None, **kwargs):
  """Gets API URL and the authorization headers.

  The login/password must be set in the global variables API_LOGIN/API_PASS.
  """
  global authorized_cookie

  url = _MakeAPIUrl(api_path, **kwargs)
  LOGGER.debug('Getting authorized endpoint for: %s', url)
  if not headers:
    headers = {}
  if not API_TOKEN:
    raise AuthorizationRequiredError('API_TOKEN not set')
  headers['Authorization'] = 'token ' + API_TOKEN
  return url, headers

# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev_ReleaseBuilder
# $version: 1
# $date: 07/14/2018

"""A client library to communicate with Kerbal CurseForge via API.

Example:
  import CurseForgeClient

  CurseForgeClient.PROJECT_ID = '123456'
  CurseForgeClient.API_TOKEN = '11111111-2222-3333-4444-555555555555'
  print 'KSP 1.4.*:', CurseForgeClient.GetVersions(r'1\.4\.\d+')
  CurseForgeClient.UploadFile(
      '/var/files/archive.zip', '# BLAH!', r'1\.4\.\d+')
"""
import json
import logging
import os.path
import re
import urllib2

from utils import FormDataUtil


# The token to use when accessing CurseForge. NEVER commit it to GitHub!
# The caller code must set this variable before using the client.
# To get it, go tho the projects account on CurseForge:
# Account/Preferences/My API Tokens/Generate Token
API_TOKEN = None

# This binds this client to the KSP namespace.
API_BASE_URL = 'https://kerbal.curseforge.com'

# The actions paths.
API_UPLOAD_URL_TMPL = '/api/projects/{project_id}/upload-file'
API_GET_VERSIONS = '/api/game/versions'
# The project details are not available via the stock API. So using CFWidgets.
API_GET_PROJECT = 'https://api.cfwidget.com/project/{project_id}'

LOGGER = logging.getLogger('ApiClient')

# The cache for the known versions of the game. It's requested only once.
cached_versions = None


class Error(Exception):
  """Genric API client error."""
  pass


class AuthorizationRequiredError(Error):
  """The method called requires authorization, but none has been provided."""
  pass


class BadCredentialsError(Error):
  """The provided authorization token is refused."""
  pass


class BadResponseError(Error):
  """Generic error from the API endpoint."""
  pass


def GetKSPVersions(pattern=None):
  """Gets the available versions of the game.

  This method caches the versions, fetched from the server. It's OK to call it
  multiple times, it will only request the server once.

  This call requires authorization.

  Args:
    pattern: A regexp string to apply on the result. If not provided, all the
        versions will be returned.
  Returns:
    A list of objects: { 'name': <KSP name>, 'id': <CurseForge ID> }. The list
    will be filtered if the pattern is set.
  """
  global cached_versions
  if not cached_versions:
    LOGGER.debug('Requesting versions to cache...')
    url, headers = _GetAuthorizedEndpoint(API_GET_VERSIONS);
    response_obj, _ = _CallAPI(url, None, headers)
    cached_versions = map(lambda x: {'name': x['name'], 'id': x['id']}, response_obj)
  if pattern:
    regex = re.compile(pattern)
    return filter(lambda x: regex.match(x['name']), cached_versions)
  return cached_versions


def GetProjectDetails(project_id):
  """Gets the project details.

  This call does *not* require authorization.

  This method is not supported by the stock API. Instead, the CurseForge
  Widgets website is used to obtain the data. Due to how that website works,
  it's possible to get a stale data or fail with "project not found" error.
  If it happens, simply retry with a reasonable delay.

  Args:
    project_id: The project to get info for.
  Returns:
    Response object.
  """
  url = API_GET_PROJECT.format(project_id=project_id)
  response_obj, _ = _CallAPI(url, None, None)
  return response_obj


def UploadFileEx(project_id, metadata, filepath):
  """Uploads the file to the CurseForce project given the full metadata.

  This call requires authorization.

  Args:
    project_id: The Curse project ID.
    metadata: See https://authors.curseforge.com/docs/api for details.
    filepath: A full or relative path to the local file.
  Returns:
    The response object.
  """
  headers, data = FormDataUtil.EncodeFormData([
      { 'name': 'metadata', 'data': metadata },
      { 'name': 'file', 'filename': filepath },
  ])
  url, headers = _GetAuthorizedEndpoint(
      API_UPLOAD_URL_TMPL, headers, project_id=project_id)
  response_obj, _ = _CallAPI(url, data, headers)
  return response_obj


def UploadFile(project_id, filepath, changelog, game_versions,
               title=None, release_type='release',
               changelog_type='markdown'):
  """Uploads the file to the CurseForge project.

  This call requires authorization.

  Args:
    project_id: The Curse project ID.
    filepath: A full or relative path to the local file.
    changelog: The change log content.
    game_versions: The KSP versions to upload the file for.
    title: The user friendly title of the file. If not provided, then the file
        name will be used.
    release_type: The type of the release. Allowed values: release, alpha, beta.
    changelog_type: The formatting type of the changelog. Allowed values:
        text, html, markdown.
  Returns:
    The response object, returned by the API.
  """
  versions = filter(lambda x: x['name'] in game_versions, GetKSPVersions())
  if not title:
    title = os.path.splitext(os.path.basename(filepath))[0]
  metadata = {
    'changelog': changelog,
    'changelogType': changelog_type,
    'displayName': title,
    'gameVersions': map(lambda x: x['id'], versions),
    'releaseType': release_type,
  }
  return UploadFileEx(project_id, metadata, filepath)


def _CallAPI(url, data, headers, raise_on_error=True):
  """Invokes the API call."""
  resp_obj = { 'error': True, 'reason': 'unknown' }
  try:
    request = urllib2.Request(url, data, headers=headers or {})
    response = urllib2.urlopen(request)
    resp_obj = json.loads(response.read())
    headers = response.info().dict
  except urllib2.HTTPError as ex:
    resp_obj = { 'error': True, 'reason': '%d - %s' % (ex.code, ex.reason) }
    try:
      resp_obj = json.loads(ex.read())
    except:
      pass  # Not a JSON response
    if ex.code == 401:
      raise AuthorizationRequiredError(resp_obj['errorMessage'])
    if ex.code == 403:
      raise BadCredentialsError(resp_obj['errorMessage'])

  # Rewrite the known error responses.
  if type(resp_obj) is dict:
    if resp_obj.get('message'):
      resp_obj = { 'error': True, 'reason': resp_obj.get('message') }
    elif resp_obj.get('errorMessage'):
      resp_obj = { 'error': True, 'reason': resp_obj.get('errorMessage') }

  if type(resp_obj) is dict and resp_obj.get('error'):
    LOGGER.error('API call failed: %s', resp_obj['reason'])
    if raise_on_error:
      raise BadResponseError(resp_obj['reason'])
    return resp_obj, None
  return resp_obj, headers


def _MakeAPIUrl(action_path, **kwargs):
  """Makes a URL for the action."""
  return API_BASE_URL + action_path.format(**kwargs)


def _GetAuthorizedEndpoint(api_path, headers=None, **kwargs):
  """Gets API URL and the authorization headers.

  The authorization token must be set in the global variable API_TOKEN.
  """
  url = _MakeAPIUrl(api_path, **kwargs)
  LOGGER.debug('Getting authorized endpoint for: %s', url)
  if not headers:
    headers = {}
  if not API_TOKEN:
    raise AuthorizationRequiredError('API_TOKEN not set')
  headers['X-Api-Token'] = API_TOKEN

  return url, headers

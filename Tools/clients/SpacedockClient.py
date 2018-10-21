# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev_ReleaseBuilder
# $version: 1
# $date: 07/13/2018

"""A client library to communicate with Spacedock via API.

Example:
  import SpacedockClient

  print 'KSP 1.4.*:', SpacedockClient.GetVersions(r'1\.4\.\d+')

  SpacedockClient.API_LOGIN = 'foo'  # Skip to have it asked from the console.
  SpacedockClient.API_PASS = 'bar'  # Skip to have it asked from the console.
  CurseForgeClient.UploadFile(
      '/var/files/archive.zip', '# BLAH!', '1.4.4', 'MyMod-1.4')
"""
import json
import logging
import re
import urllib2

from utils import FormDataUtil


# The account credentials.
API_LOGIN = None
API_PASS = None

# Endpoint for all the API requests
API_BASE_URL = 'https://spacedock.info'

# The actions paths.
API_AUTHORIZE = '/api/login'
API_UPDATE_MOD_TMPL = '/api/mod/{mod_id}/update'
API_GET_VERSIONS = '/api/kspversions'
API_GET_MOD = '/api/mod/{mod_id}'

LOGGER = logging.getLogger('ApiClient')

# The cache for the known versions of the game. It's requested only once.
cached_versions = None

# The authorization cookie. It's only created once. To refresh it, simply
#  set it to None.
authorized_cookie = None


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

  This call does NOT require authorization.

  Args:
    pattern: A regexp string to apply on the result. If not provided, all the
        versions will be returned.
  Returns:
    A list of objects: { 'name': <KSP name>, 'id': <Spacedock ID> }. The list
    will be filtered if the pattern is set.
  """
  global cached_versions
  if not cached_versions:
    LOGGER.debug('Requesting versions to cache...')
    response = _CallAPI(_MakeAPIUrl(API_GET_VERSIONS), None, None)
    cached_versions = map(
        lambda x: {'name': x['friendly_version'], 'id': x['id']}, response[0])
  if pattern:
    regex = re.compile(pattern)
    return filter(lambda x: regex.match(x['name']), cached_versions)
  return cached_versions


def GetModDetails(mod_id):
  """Gets the mod informnation.

  This call does NOT require authorization.

  Args:
    mod_id: The mod to request.
  Returns:
    The response object.
  """
  url = _MakeAPIUrl(API_GET_MOD, mod_id=mod_id)
  response_obj, _ = _CallAPI(url, None, None)
  return response_obj


def UploadFile(mod_id, filepath, changelog, mod_version, game_version):
  """Uploads the file to the CurseForge project.

  The new file immediately becomes a default version.

  Args:
    mod_id: The mod ID to update.
    filepath: A full or relative path to the local file.
    changelog: The change log content.
    mod_version: The version of the mod being published.
    game_version: The KSP version to publish for.
  Returns:
    The response object.
  """

  headers, data = FormDataUtil.EncodeFormData([
      { 'name': 'version', 'data': mod_version },
      { 'name': 'changelog', 'data': changelog },
      { 'name': 'game-version', 'data': game_version },
      { 'name': 'notify-followers', 'data': 'yes' },
      { 'name': 'zipball', 'filename': filepath },
  ])
  url, headers = _GetAuthorizedEndpoint(
      API_UPDATE_MOD_TMPL, headers, mod_id=mod_id)
  resp = _CallAPI(url, data=data, headers=headers)


def _MakeAPIUrl(action_path, **kwargs):
  """Makes a URL for the action."""
  return API_BASE_URL + action_path.format(**kwargs)


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
      raise AuthorizationRequiredError(resp_obj['reason'])

  if type(resp_obj) is dict and resp_obj.get('error'):
    LOGGER.error('API call failed: %s', resp_obj['reason'])
    if raise_on_error:
      raise BadResponseError(resp_obj['reason'])
    return resp_obj, None
  return resp_obj, headers


def _GetAuthorizedEndpoint(api_path, headers=None, **kwargs):
  """Gets API URL and the authorization headers.

  The login/password must be set in the global variables API_LOGIN/API_PASS.
  """
  global authorized_cookie

  url = _MakeAPIUrl(api_path, **kwargs)
  LOGGER.debug('Getting authorized endpoint for: %s', url)
  if not headers:
    headers = {}

  if not authorized_cookie:
    if not API_LOGIN or not API_PASS:
      raise BadCredentialsError('API_LOGIN and/or API_PASS not set')
    LOGGER.info('Authorizing for login: %s', API_LOGIN)
    auth_headers, data = FormDataUtil.EncodeFormData([
        { 'name': 'username', 'data': API_LOGIN },
        { 'name': 'password', 'data': API_PASS },
    ])
    resp, auth_headers = _CallAPI(
        API_BASE_URL + API_AUTHORIZE, data, auth_headers,
        raise_on_error=False)
    if resp['error']:
      raise BadCredentialsError(resp['reason'])
    authorized_cookie = auth_headers['set-cookie']

  headers['Cookie'] = authorized_cookie
  return url, headers

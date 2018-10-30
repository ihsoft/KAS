# Public domain license.
# Author: igor.zavoychinskiy@gmail.com
# GitHub: https://github.com/ihsoft/KSPDev_ReleaseBuilder
# $version: 1
# $date: 10/28/2018

"""Provides helpers to deal with the markup CHANGELOG file."""
import re


def ExtractDescription(changelog_file, breaker_re):
  """Loads the file and extarcts the lines up to the specified breaker."""
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


def ProcessGitHubLinks(markup, github):
  """Replaces the GitHub local links by the external variants."""
  markup = re.sub(
      r'#(\d+)',
      r'[#\1](https://github.com/%s/issues/\1)' % github,
      markup)
  markup = re.sub(
      r'\[(.+?)\]\((wiki/.+?)\)',
      r'[\1](https://github.com/%s/\2)' % github,
      markup)
  return markup

"""Expose the game's four tools through MCP stdio."""

import argparse
import os
import sys
from pathlib import Path

from mcp_tools import actions
from mcp_tools.server import serve


def main():
  for stream in (sys.stdin, sys.stdout, sys.stderr):
    stream.reconfigure(encoding='utf-8')
  parser = argparse.ArgumentParser(description=__doc__)
  parser.add_argument(
    '--bridge', default=os.environ.get('ALIEN_BRIDGE_URL', 'http://127.0.0.1:8765')
  )
  parser.add_argument('--log', type=Path, help='Append tool requests and results to a JSONL file')
  args = parser.parse_args()

  def request(path, body=None):
    return actions.api(path, body, url=args.bridge)

  serve(lambda name, arguments: actions.call(name, arguments, request), args.log)


if __name__ == '__main__':
  main()

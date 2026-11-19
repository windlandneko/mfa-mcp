"""Line-delimited MCP JSON-RPC over stdin/stdout."""

import json
import sys
import time

from .schema import HELP, TOOLS


def serve(call, log=None):
  for line in sys.stdin:
    try:
      msg = json.loads(line)
    except json.JSONDecodeError:
      print(
        json.dumps(
          {'jsonrpc': '2.0', 'id': None, 'error': {'code': -32700, 'message': 'Parse error'}}
        ),
        flush=True,
      )
      continue
    if (
      not isinstance(msg, dict)
      or not isinstance(msg.get('method'), str)
      or not isinstance(msg.get('params', {}), dict)
    ):
      key = msg.get('id') if isinstance(msg, dict) else None
      print(
        json.dumps(
          {'jsonrpc': '2.0', 'id': key, 'error': {'code': -32600, 'message': 'Invalid Request'}}
        ),
        flush=True,
      )
      continue
    if 'id' not in msg:
      continue
    method = msg['method']
    response = {'jsonrpc': '2.0', 'id': msg['id']}
    if method == 'initialize':
      result = {
        'protocolVersion': msg.get('params', {}).get('protocolVersion', '2025-06-18'),
        'capabilities': {'tools': {}},
        'serverInfo': {'name': 'mfa-mcp', 'version': '2.0.0'},
        'instructions': HELP,
      }
    elif method == 'tools/list':
      result = {'tools': TOOLS}
    elif method == 'tools/call':
      try:
        params = msg['params']
        result = {'content': call(params['name'], params.get('arguments', {})), 'isError': False}
      except Exception as error:
        result = {'content': [{'type': 'text', 'text': str(error)}], 'isError': True}
    elif method == 'ping':
      result = {}
    else:
      response['error'] = {'code': -32601, 'message': 'Method not found'}
      result = None
    if result is not None:
      response['result'] = result
    if method == 'tools/call' and log:
      try:
        log.parent.mkdir(parents=True, exist_ok=True)
        with log.open('a', encoding='utf-8') as file:
          file.write(
            json.dumps(
              {'time': time.time(), 'request': msg, 'response': response}, ensure_ascii=False
            )
            + '\n'
          )
      except OSError as error:
        print(f'Cannot write tool log: {error}', file=sys.stderr)
    print(json.dumps(response, ensure_ascii=False), flush=True)

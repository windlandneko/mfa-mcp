"""Game actions over the local HTTP bridge."""

import base64
import json
import urllib.error
import urllib.parse
import urllib.request

from .display import image_png, screen
from .schema import validate_args


def api(path, body=None, *, url):
  req = urllib.request.Request(
    url.rstrip('/') + path,
    data=None if body is None else json.dumps(body).encode(),
    headers={
      'Content-Type': 'application/json',
    },
  )
  try:
    with urllib.request.urlopen(req, timeout=15) as response:
      return json.load(response)
  except urllib.error.HTTPError as error:
    raise RuntimeError(json.loads(error.read()).get('error', str(error))) from None
  except urllib.error.URLError:
    raise RuntimeError('Game bridge unavailable. Run: uv run run.py') from None


def click(x, y, button, request):
  return screen(request('/click', {'x': x, 'y': y, 'button': button}))


def note(items, request):
  errors = []
  state = None
  for item in items:
    try:
      state = request('/annotate', item)
    except RuntimeError as error:
      errors.append(str(error))
  result = screen(request('/state') if errors or state is None else state)
  return result + ('\nErrors: ' + '; '.join(errors) if errors else '')


def render(ids, request):
  shapes = {code: request('/glyph?id=' + urllib.parse.quote(code)) for code in dict.fromkeys(ids)}
  png = image_png([shapes[code] for code in ids])
  return [{'type': 'image', 'data': base64.b64encode(png).decode(), 'mimeType': 'image/png'}]


def call(name, args, request):
  validate_args(name, args)
  if name == 'render':
    return render(args['ids'], request)
  text = (
    note(args['notes'], request)
    if name == 'note'
    else click(args['x'], args['y'], 'right' if name == 'rclick' else 'left', request)
  )
  return [{'type': 'text', 'text': text}]

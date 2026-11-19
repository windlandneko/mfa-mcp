"""Tool descriptions, JSON schemas and runtime argument validation."""

import re

SCREEN_FORMAT = (
  '24×9 grid, coordinates (x,y) from top-left (0,0). '
  'Cells are space-separated IDs or ..; | and -- are borders, not cells. '
  '* highlight, ~ grey; <>/[]/{} solid/grey/faint underline; □/▢/⬚ boxes, ^ tall. '
  'notes lists visible annotations. IDs are random and have no meaning.'
)
HELP = SCREEN_FORMAT


COORDS = {
  'x': {'type': 'integer', 'minimum': 0, 'maximum': 23},
  'y': {'type': 'integer', 'minimum': 0, 'maximum': 8},
}
RO = {'readOnlyHint': True, 'openWorldHint': False}
RW = {
  'readOnlyHint': False,
  'destructiveHint': False,
  'idempotentHint': False,
  'openWorldHint': False,
}
TOOLS = [
  {
    'name': name,
    'description': description,
    'annotations': RW,
    'inputSchema': {
      'type': 'object',
      'properties': COORDS,
      'required': ['x', 'y'],
      'additionalProperties': False,
    },
  }
  for name, description in [
    ('click', 'Left-click. Returns the screen.'),
    ('rclick', 'Right-click. Returns the screen.'),
  ]
] + [
  {
    'name': 'note',
    'description': 'Set annotations. Empty text clears; notes:[] reads the screen.',
    'annotations': RW,
    'inputSchema': {
      'type': 'object',
      'properties': {
        'notes': {
          'type': 'array',
          'maxItems': 216,
          'items': {
            'type': 'object',
            'properties': {
              'id': {'type': 'string', 'pattern': '^(?:[A-Za-z]{2}|[0-9]{2})$'},
              'text': {'type': 'string', 'maxLength': 20},
            },
            'required': ['id', 'text'],
            'additionalProperties': False,
          },
        }
      },
      'required': ['notes'],
      'additionalProperties': False,
    },
  },
  {
    'name': 'render',
    'description': 'Return glyphs as a PNG, preserving order and repetitions.',
    'annotations': RO,
    'inputSchema': {
      'type': 'object',
      'properties': {
        'ids': {
          'type': 'array',
          'items': {'type': 'string', 'pattern': '^(?:[A-Za-z]{2}|[0-9]{2})$'},
          'minItems': 1,
          'maxItems': 216,
        }
      },
      'required': ['ids'],
      'additionalProperties': False,
    },
  },
]


def validate_args(name, args):
  if not isinstance(args, dict):
    raise ValueError('Tool arguments must be an object')
  expected = {'click': {'x', 'y'}, 'rclick': {'x', 'y'}, 'note': {'notes'}, 'render': {'ids'}}
  if name not in expected or set(args) != expected[name]:
    raise ValueError('Invalid tool name or arguments')
  if name in ('click', 'rclick'):
    if any(
      type(args[k]) is not int or not 0 <= args[k] < limit for k, limit in [('x', 24), ('y', 9)]
    ):
      raise ValueError('Coordinates must be integers: x=0..23, y=0..8')
    return
  values = args['notes' if name == 'note' else 'ids']
  if not isinstance(values, list) or len(values) > 216 or (name == 'render' and not values):
    raise ValueError('Expected 0–216 notes or 1–216 glyph IDs')
  for value in values:
    if name == 'note':
      if (
        not isinstance(value, dict)
        or set(value) != {'id', 'text'}
        or not isinstance(value['text'], str)
      ):
        raise ValueError('Each note requires id and text strings')
      if len(value['text']) > 20:
        raise ValueError('Annotations are limited to 20 characters')
      value = value['id']
    if not isinstance(value, str) or re.fullmatch(r'(?:[A-Za-z]{2}|[0-9]{2})', value) is None:
      raise ValueError('Glyph IDs must be two letters or two digits')

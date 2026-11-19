"""Compact screen layout and ordered glyph rendering."""

import csv
import io
import json
from functools import lru_cache
from itertools import pairwise
from pathlib import Path

FONT_ROOT = Path(__file__).resolve().parents[1] / 'font'
_event_session = None
_event_sequence = 0
_achievement_context = None
_earned = set()
ACHIEVEMENTS = json.loads(Path(__file__).with_name('achievements.json').read_text(encoding='utf-8'))
BOXES = {'solid': '□', 'grey': '▢', 'faint': '⬚'}
UNDERLINE_BRACKETS = {'solid': ('<', '>'), 'grey': ('[', ']'), 'faint': ('{', '}')}


def screen(state):
  cells = [['..' for _ in range(24)] for _ in range(9)]
  notes = {}
  for cell in state['cells']:
    text = '..' if cell.get('blank') else cell.get('id') or '..'
    if cell.get('note') and text != '..':
      notes[text] = cell['note']
    text += '*' if cell.get('hl') or cell.get('hover') else ''
    text += '~' if cell.get('dim') else ''
    cells[cell['y']][cell['x']] = text
  horizontal, vertical = borders(state)
  underlines = {}
  for mark in state.get('marks', []):
    if mark['type'] == 'underline':
      for x in range(max(0, mark['x0']), min(24, mark['x1'] + 1)):
        underlines[x, mark['y']] = mark['style']
    elif 0 <= mark['x'] < 24 and 0 <= mark['y'] < 9:
      cells[mark['y']][mark['x']] += BOXES[mark['style']] + ('^' if mark.get('tall') else '')
  for (x, y), style in underlines.items():
    if not 0 <= y < 9:
      continue
    opening, closing = UNDERLINE_BRACKETS[style]
    if underlines.get((x - 1, y)) != style or (x, y) in vertical:
      cells[y][x] = opening + cells[y][x]
    if underlines.get((x + 1, y)) != style or (x + 1, y) in vertical:
      cells[y][x] += closing
  lines = grid_lines(cells, horizontal, vertical)
  if notes:
    lines.append(
      'notes: '
      + ' '.join(f'{code}={json.dumps(text, ensure_ascii=False)}' for code, text in notes.items())
    )
  global _event_session, _event_sequence, _achievement_context, _earned
  if 'achievements' in state:
    context = (state.get('session'), state.get('slot'))
    earned = set(state['achievements'])
    if context != _achievement_context:
      lines.append(f'成就（{len(earned)}/{len(ACHIEVEMENTS)}）：')
      for code, item in ACHIEVEMENTS.items():
        status = '已解锁' if code in earned else '未解锁'
        lines.append(f'{code} {item["title"]} — {item["description"]}（{status}）')
      _achievement_context, _earned = context, earned
    else:
      for code in sorted(earned - _earned):
        item = ACHIEVEMENTS.get(code)
        lines.append(
          f'成就解锁：{code} {item["title"]} — {item["description"]}'
          if item
          else f'成就解锁：{code}'
        )
      _earned.update(earned)
  if state.get('session') != _event_session:
    _event_session, _event_sequence = state.get('session'), 0
  for event in state.get('events', []):
    if event['seq'] > _event_sequence:
      lines.append(event['text'])
      _event_sequence = event['seq']
  return '\n'.join(lines)


def grid_lines(cells, horizontal, vertical):
  """Keep one space between tokens; .. also marks gaps in partial divider rows."""
  lines = []
  for y in range(10):
    if horizontal.get(y):
      spans = horizontal[y]
      row = []
      for x in range(25):
        if (x, y - 1) in vertical or (x, y) in vertical:
          row.append('+' if x - 1 in spans or x in spans else '|')
        if x < 24:
          row.append('--' if x in spans else '..')
      lines.append(' '.join(row))
    if y < 9:
      row = []
      for x in range(25):
        if (x, y) in vertical:
          row.append('|')
        if x < 24:
          row.append(cells[y][x])
      lines.append(' '.join(row))
  return lines


def borders(state):
  horizontal, vertical = {}, set()
  for shape in state.get('geometry', []):
    # Selected cell underlines/boxes already have precise inline notation.
    if shape.get('selected'):
      continue
    points = shape['points']
    if not points:
      continue
    xs, ys = zip(*points)
    width, height = max(xs) - min(xs), max(ys) - min(ys)
    if shape.get('fill') and min(width, height) < 0.12:
      # Shapes2D represents a divider as a thin filled rectangle.
      points = (
        [((min(xs) + max(xs)) / 2, min(ys)), ((min(xs) + max(xs)) / 2, max(ys))]
        if width < height
        else [(min(xs), (min(ys) + max(ys)) / 2), (max(xs), (min(ys) + max(ys)) / 2)]
      )
    if shape['kind'] == 'ellipse':
      continue
    for (x0, y0), (x1, y1) in pairwise(points):
      if abs(y0 - y1) < 0.03 and abs(x1 - x0) >= 0.5:
        y = round((6.75 - y0) / 1.5)
        start, end = sorted((round(x0 + 12), round(x1 + 12)))
        if 0 <= y <= 9:
          horizontal.setdefault(y, set()).update(range(max(0, start), min(24, end)))
      elif abs(x0 - x1) < 0.03 and abs(y1 - y0) >= 0.5:
        x = round(x0 + 12)
        start, end = sorted((round((6.75 - y0) / 1.5), round((6.75 - y1) / 1.5)))
        if 0 <= x <= 24:
          vertical.update((x, y) for y in range(max(0, start), min(9, end)))
  return horizontal, vertical


def shape_key(strokes):
  scale = max([0.4] + [abs(v) for stroke in strokes for point in stroke for v in point])
  return tuple(
    tuple((round(x / scale, 2), round(y / scale, 2)) for x, y in stroke) for stroke in strokes
  )


@lru_cache(maxsize=1)
def font_shapes():
  source = json.loads((FONT_ROOT / 'source/glyphs.json').read_text(encoding='utf-8'))
  with (FONT_ROOT / 'source/registry.csv').open(encoding='utf-8') as file:
    return {
      chr(int(row['codepoint'].removeprefix('U+'), 16)): shape_key(source[row['word']])
      for row in csv.DictReader(file)
      if row['word'] in source
    }


def image_png(glyphs):
  from PIL import Image, ImageDraw, ImageFont

  columns = min(len(glyphs), 24)
  rows = (len(glyphs) + columns - 1) // columns
  size, margin, scale = 64, 8, 2
  width, height = columns * size + 2 * margin, rows * size + 2 * margin
  image = Image.new('RGB', (width * scale, height * scale), 'white')
  draw = ImageDraw.Draw(image)
  font = ImageFont.truetype(str(FONT_ROOT / 'data/Xingyu-VF.ttf'), size * scale)
  known = font_shapes()
  for index, glyph in enumerate(glyphs):
    cx = margin + (index % columns + 0.5) * size
    cy = margin + (index // columns + 0.5) * size
    strokes, unicode = glyph['strokes'], glyph.get('unicode')
    # Use the font only when its outline matches this game build.
    if unicode in known and known[unicode] == shape_key(strokes):
      draw.text(
        ((cx - size / 2) * scale, (cy + 0.358 * size) * scale),
        unicode,
        font=font,
        fill='black',
        anchor='ls',
      )
    else:
      extent = max([0.4] + [abs(v) for stroke in strokes for point in stroke for v in point])
      for stroke in strokes:
        points = [
          ((cx + x / extent * size * 0.4) * scale, (cy - y / extent * size * 0.4) * scale)
          for x, y in stroke
        ]
        if len(points) >= 2:
          draw.line(points, fill='black', width=4, joint='curve')
  image = image.resize((width, height), Image.Resampling.LANCZOS)
  output = io.BytesIO()
  image.save(output, format='PNG')
  return output.getvalue()

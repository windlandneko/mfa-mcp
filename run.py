"""Start the patched game and wait for its MCP bridge."""

import argparse
import json
import os
import subprocess
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent
from mcp_tools.actions import api


def main():
  parser = argparse.ArgumentParser(description=__doc__)
  parser.add_argument('--save', type=Path, default=ROOT / 'saves/default')
  parser.add_argument('--port', type=int, default=8765)
  parser.add_argument('--window', action='store_true', help='Show the game window')
  args = parser.parse_args()
  if not 1 <= args.port <= 65535:
    parser.error('--port must be 1..65535')
  bridge = f'http://127.0.0.1:{args.port}'
  try:
    health = api('/health', url=bridge)
  except RuntimeError:
    health = None
  if health:
    if health.get('api') != 2:
      parser.error(f'Port {args.port} is occupied by another service')
    previous_save = health.get('save_dir')
    if not previous_save or Path(previous_save).resolve() != args.save.expanduser().resolve():
      parser.error('This port runs another save; stop that game or choose --port')
    print(f'Bridge ready: {bridge} (pid {health["pid"]})')
    return
  runtime = ROOT / 'runtime.json'
  if not runtime.exists():
    parser.error('Run build.py first')
  executable = Path(json.loads(runtime.read_text(encoding='utf-8'))['executable']).resolve()
  if not executable.is_file():
    parser.error(f'Game executable not found: {executable}')
  save = args.save.expanduser().resolve()
  save.mkdir(parents=True, exist_ok=True)
  environment = dict(
    os.environ,
    ALIEN_SAVE_DIR=str(save),
    ALIEN_BRIDGE_PORT=str(args.port),
  )
  command = [
    str(executable),
    '-logFile',
    str(save / 'player.log'),
    '-screen-fullscreen',
    '0',
    '-screen-width',
    '1600',
    '-screen-height',
    '900',
  ]
  if not args.window:
    command.append('-batchmode')
  options = (
    {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP}
    if os.name == 'nt'
    else {'start_new_session': True}
  )
  process = subprocess.Popen(
    command,
    cwd=executable.parent,
    env=environment,
    stdout=subprocess.DEVNULL,
    stderr=subprocess.DEVNULL,
    **options,
  )
  log = save / 'player.log'
  for _ in range(80):
    if os.name == 'nt' and log.is_file():
      tail = log.read_text(encoding='utf-8', errors='replace')[-12000:]
      if 'HttpListenerException' in tail and (
        'Access is denied' in tail or 'Access denied' in tail
      ):
        process.terminate()
        raise RuntimeError(
          'Run PowerShell as administrator once: '
          f'netsh http add urlacl url={bridge}/ user="$env:USERDOMAIN\\$env:USERNAME"'
        )
    if process.poll() is not None:
      raise RuntimeError(f'Game exited; see {save / "player.log"}')
    try:
      health = api('/health', url=bridge)
      if health.get('pid') == process.pid:
        print(f'Bridge ready: {bridge}\nSave: {save}')
        return
    except RuntimeError:
      pass
    time.sleep(0.5)
  process.terminate()
  raise RuntimeError(f'Bridge startup timed out; see {save / "player.log"}')


if __name__ == '__main__':
  main()

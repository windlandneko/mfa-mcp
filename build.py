"""Build the bridge and patch a Windows Unity Mono game copy."""

import argparse
import csv
import json
import os
import shutil
import subprocess
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parent
NATIVE = ROOT / 'native'


def game_files(path):
  path = Path(path).expanduser().resolve()
  if path.is_file():
    executable = path
    folder = path.parent
  else:
    folder = path
    executables = [p for p in folder.glob('*.exe') if not p.name.startswith('UnityCrashHandler')]
    preferred = folder / 'MessageFromAliens.exe'
    executable = preferred if preferred.exists() else next(iter(executables), None)
  if executable is None or executable.suffix.lower() != '.exe':
    raise ValueError('Pass the game folder or its .exe with --game')
  managed = folder / (executable.stem + '_Data') / 'Managed'
  if not managed.exists():
    candidates = list(folder.glob('*_Data/Managed'))
    if len(candidates) == 1:
      managed = candidates[0]
  return folder, executable, managed


def compile_bridge(managed):
  if not (managed / 'Assembly-CSharp.dll').exists():
    raise ValueError(
      f'Unity Mono Assembly-CSharp.dll not found in {managed}; IL2CPP builds are not supported'
    )
  steam = next(
    iter(
      sorted(
        managed.glob('Facepunch.Steamworks*.dll'), key=lambda p: ('Win64' not in p.name, p.name)
      )
    ),
    None,
  )
  if steam is None:
    raise ValueError(f'Facepunch.Steamworks DLL not found in {managed}')
  build = ROOT / 'build'
  subprocess.run(
    [
      'dotnet',
      'build',
      str(NATIVE / 'AlienBridge.csproj'),
      f'-p:GameManaged={managed}',
      f'-p:SteamAssembly={steam}',
      '-o',
      str(build / 'bridge'),
      '-v',
      'q',
    ],
    check=True,
  )
  subprocess.run(
    [
      'dotnet',
      'build',
      str(NATIVE / 'Patcher/Patcher.csproj'),
      '-o',
      str(build / 'patcher'),
      '-v',
      'q',
    ],
    check=True,
  )
  return build


def main():
  parser = argparse.ArgumentParser(description=__doc__)
  parser.add_argument('--game', default=os.environ.get('ALIEN_GAME'), help='Game folder or .exe')
  parser.add_argument('--output', type=Path, default=ROOT / 'game', help='Patched game copy')
  args = parser.parse_args()
  if not args.game:
    parser.error('--game is required')
  folder, executable, managed = game_files(args.game)
  build = compile_bridge(managed)
  target = args.output.resolve()
  if target.exists():
    parser.error(f'Output already exists: {target}; choose another --output directory')
  if target == folder or folder in target.parents:
    parser.error('--output must be outside the installed game directory')
  relative_managed = managed.relative_to(folder)
  target.parent.mkdir(parents=True, exist_ok=True)
  with tempfile.TemporaryDirectory(prefix='.alien-build-', dir=target.parent) as temp:
    stage = Path(temp) / 'game'
    shutil.copytree(folder, stage)
    patched = stage / relative_managed
    subprocess.run(
      [
        'dotnet',
        str(build / 'patcher/Patcher.dll'),
        str(patched),
        str(build / 'bridge/AlienBridge.dll'),
      ],
      check=True,
    )
    with (ROOT / 'font/source/registry.csv').open(encoding='utf-8') as source:
      mapping = {
        r['word']: chr(int(r['codepoint'].removeprefix('U+'), 16)) for r in csv.DictReader(source)
      }
    (patched / 'unicode.json').write_text(json.dumps(mapping, ensure_ascii=False), encoding='utf-8')
    shutil.copy2(NATIVE / 'ids.txt', patched / 'ids.txt')
    stage.rename(target)
  (ROOT / 'runtime.json').write_text(
    json.dumps(
      {
        'executable': str(target / executable.relative_to(folder)),
      },
      indent=2,
    ),
    encoding='utf-8',
  )
  print(target)


if __name__ == '__main__':
  main()

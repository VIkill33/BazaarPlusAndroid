#!/usr/bin/env python3
"""Verify local history artwork against a read-only history inspection; never changes device files.

PNG format checks catch the native material shader-mask alpha regression. Pixel
content still requires screenshot review; these checks do not claim visual recognition.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import struct
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--game-root', required=True)
    parser.add_argument('--inspection', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    if not re.fullmatch(r'/sdcard/BazaarPlusPlusAndroid/[A-Za-z][A-Za-z0-9_]*(?:\.[A-Za-z][A-Za-z0-9_]*)+', args.game_root):
        parser.error('Expected a confirmed target game root')
    adb = ['adb', '-s', args.serial, 'exec-out', 'cat']

    def read(path):
        return subprocess.check_output(adb + [path], timeout=15)

    root = args.game_root + '/BepInEx/history-art/.cache/v3/'
    version = read(root + 'current.txt').decode().strip()
    if not re.fullmatch('[a-f0-9]{64}', version):
        raise AssertionError('Invalid cache version pointer')
    assert read(root + version + '/.nomedia') == b'', 'Image directory marker missing or nonempty'
    source = json.loads(read(root + version + '/source.json'))
    assert source['schema'] == 3
    assert hashlib.sha256(source['gameVersion'].encode()).hexdigest() == version
    summary = json.loads(args.inspection.read_text())['summary']
    keys = {'hero-Pygmalien', 'hero-Vanessa', 'hero-TheDragons', 'state-broken', 'state-pending', 'Trophy_Diamond_TUI'}
    card_types = set()
    for row in summary['records']:
        if row['kind'] != 'pvp' or not row['payload']:
            continue
        for card in json.loads(row['payload'])['cards']:
            identifier = card.get('templateId', '')
            if re.fullmatch('[a-f0-9-]{36}', identifier):
                keys.add('card-' + identifier)
                card_types.add(card['type'])
    evidence = []
    for key in sorted(keys):
        data = read(root + version + '/' + key + '.bppart')
        assert data[:8] == b'\x89PNG\r\n\x1a\n' and data[12:16] == b'IHDR', key
        width, height, depth, color = struct.unpack('>IIBB', data[16:26])
        assert 0 < width <= 384 and 0 < height <= 384 and depth == 8, key
        assert color == (2 if key.startswith('card-') else 6), key + ': shader-mask alpha must not hide card artwork'
        evidence.append({'key': key, 'width': width, 'height': height, 'colorType': color,
                         'sha256': hashlib.sha256(data).hexdigest()})
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps({'passed': True, 'readOnly': True, 'source': source,
        'cardTypes': sorted(card_types), 'images': evidence}, ensure_ascii=False, indent=2) + '\n')
    print(f'PASS {len(evidence)} PNGs: current source version, dimensions and correct card/portrait alpha formats; types {sorted(card_types)}')


if __name__ == '__main__':
    main()

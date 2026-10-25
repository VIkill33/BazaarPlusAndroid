#!/usr/bin/env python3
"""Check the open launcher history page against a read-only device inspection.

Requires Android CLI and an already open history run list. Only browses saved
records; never launches games or changes history. Screen rotation settings are
restored even on failure. Evidence belongs in the ignored artifacts directory.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import time


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serial', required=True)
    parser.add_argument('--inspection', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    expected = json.loads(args.inspection.read_text())['summary']
    run = expected['browserRuns']['items'][0]
    battles = expected['browserBattles']['items']
    first = expected['browserBattle']['battle']
    if not battles or first['id'] != battles[0]['id']:
        parser.error('Inspection must contain a first battle and its snapshot')
    args.output.mkdir(parents=True, exist_ok=True)
    (args.output / 'passed.json').unlink(missing_ok=True)
    adb = ['adb', '-s', args.serial]
    checks = []
    sequence = 0

    def command(parts):
        return subprocess.run(parts, check=True, text=True, capture_output=True, timeout=30).stdout.strip()

    def shell(*parts):
        return command(adb + ['shell'] + list(parts))

    def nodes(value):
        if isinstance(value, dict):
            yield value
            for item in value.values():
                yield from nodes(item)
        elif isinstance(value, list):
            for item in value:
                yield from nodes(item)

    def layout():
        nonlocal sequence
        sequence += 1
        path = args.output / f'layout-{sequence:03}.json'
        command(['android', 'layout', '--device', args.serial, '--full', '--output', str(path)])
        return list(nodes(json.loads(path.read_text())))

    def texts(items):
        return '\n'.join(str(item.get('text', '')) for item in items)

    def tap(node, hold=False):
        match = re.fullmatch(r'\[(\d+),(\d+)\]\[(\d+),(\d+)\]', node['bounds'])
        if match is None:
            raise AssertionError('Missing observed UI bounds')
        left, top, right, bottom = map(int, match.groups())
        x, y = str((left + right) // 2), str((top + bottom) // 2)
        if hold:
            shell('input', 'swipe', x, y, x, y, '800')
        else:
            shell('input', 'tap', x, y)

    def row(items, needle):
        return next((item for item in items if item.get('resource-id') == 'android:id/text1' and needle in item.get('text', '') and 'bounds' in item), None)

    def scroll(items):
        container = next(item for item in items if item.get('class') == 'android.widget.ListView' and 'bounds' in item)
        left, top, right, bottom = map(int, re.findall(r'\d+', container['bounds']))
        shell('input', 'swipe', str((left + right) // 2), str(top + (bottom - top) * 3 // 4),
              str((left + right) // 2), str(top + (bottom - top) * 7 // 20), '800')

    def find(needle, attempts=12):
        for _ in range(attempts):
            items = layout()
            found = row(items, needle)
            if found is not None:
                return found
            scroll(items)
        raise AssertionError(f'Expected visible row: {needle}')

    def check(value, label):
        if not value:
            raise AssertionError(label)
        checks.append(label)
        print('PASS ' + label, flush=True)

    def screenshot(name):
        command(['android', 'screen', 'capture', '--device', args.serial, '--output', str(args.output / (name + '.png'))])

    original = {key: shell('settings', 'get', 'system', key) for key in ['accelerometer_rotation', 'user_rotation']}
    try:
        items = layout()
        check('对局历史' in texts(items), 'history list is open')
        tap(find(run['hero']))
        items = layout()
        check('整局详情' in texts(items) and f"{run['battleCount']} 场已存战斗" in texts(items), 'run identity and persisted battle count match the database')
        screenshot('battles-portrait')
        first_row = find(f"#{battles[0]['sequence']} ·")
        first_bounds = first_row['bounds']
        tap(first_row)
        items = layout()
        check('双方阵容' in texts(items), 'selected battle opens its own snapshot page')
        cards = first['snapshots']['playerHand']['cards']
        if cards:
            card = cards[0]
            tap(find(card['name']))
            body = texts(layout())
            check(all(str(card[key]) in body for key in ['name', 'instanceId', 'templateId', 'tier', 'size', 'type']), 'card details preserve identity, quality, size and type')
            check(all(f'{key}：{value}' in body for key, value in card['attributes'].items()), 'every saved instance attribute matches the database')
            screenshot('card-fields')
            shell('input', 'keyevent', '4')
            tap(find(card['name']), hold=True)
            check(card['instanceId'] in texts(layout()), 'long press opens the same saved card without changing selection')
            shell('input', 'keyevent', '4')
        seen, previous = set(), None
        card_count = sum(len(group['cards']) for group in first['snapshots'].values())
        for _ in range(card_count * 2 + 20):
            items = layout()
            current = tuple(item.get('text', '') for item in items if item.get('resource-id') == 'android:id/text1')
            seen.update(current)
            if current == previous:
                break
            previous = current
            scroll(items)
        joined = '\n'.join(seen)
        for key, label in [('playerHand', '我方物品'), ('playerSkills', '我方技能'), ('opponentHand', '对方物品'), ('opponentSkills', '对方技能')]:
            group = first['snapshots'][key]
            state = {'captured': '已完整采集', 'partial': '部分采集', 'failed': '采集失败', 'unavailable': '未采集'}[group['status']]
            check(f'{label} · {state}' in joined and all(card['name'] in joined for card in group['cards']), label + ' states and saved cards are present through the scroll end')
        shell('input', 'keyevent', '4')
        check('整局详情' in texts(layout()), 'back returns to the battle list')
        check(find(f"#{battles[0]['sequence']} ·")['bounds'] == first_bounds, 'back preserves the battle list scroll anchor')
        pending = next((item for item in battles if item['outcome'] is None), None)
        if pending:
            tap(find(f"#{pending['sequence']} ·"))
            check('结果未记录' in texts(layout()), 'unfinished battle remains unknown after switching selection')
        else:
            tap(find(f"#{battles[0]['sequence']} ·"))
        shell('settings', 'put', 'system', 'accelerometer_rotation', '0')
        shell('settings', 'put', 'system', 'user_rotation', '1')
        time.sleep(2)
        items = layout()
        check('双方阵容' in texts(items), 'selected snapshot survives landscape recreation')
        find('品质 ')
        screenshot('snapshot-landscape')
        shell('settings', 'put', 'system', 'user_rotation', '0')
        time.sleep(2)
        check('双方阵容' in texts(layout()), 'selected snapshot survives portrait recreation')
        screenshot('snapshot-portrait')
        shell('input', 'keyevent', '4')
        shell('input', 'keyevent', '4')
        check('对局历史' in texts(layout()), 'back restores the run list')
    finally:
        for key, value in original.items():
            shell('settings', 'delete' if value == 'null' else 'put', 'system', key, *([] if value == 'null' else [value]))
        (args.output / 'checks.json').write_text(json.dumps({'passedChecks': checks, 'rotationRestored': True}, ensure_ascii=False, indent=2) + '\n')
    (args.output / 'passed.json').write_text(json.dumps({'passed': True, 'checks': len(checks)}, indent=2) + '\n')


if __name__ == '__main__':
    main()

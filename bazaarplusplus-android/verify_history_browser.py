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
    parser.add_argument('--native-ratios', type=Path, help='Measured mobile CardPreviewItem artwork geometry for aspect assertions')
    parser.add_argument('--battle-sequence', type=int, help='Verify another persisted battle, including dense skill formations')
    parser.add_argument('--expect-artwork', action='store_true', help='Require cached hero and selected card images to decode and bind')
    args = parser.parse_args()
    expected = json.loads(args.inspection.read_text())['summary']
    run = expected['browserRuns']['items'][0]
    battles = expected['browserBattles']['items']
    first = expected['browserBattle']['battle']
    if args.battle_sequence is not None:
        chosen = next(item for item in battles if item['sequence'] == args.battle_sequence)
        records = [row for row in expected['records'] if row['runId'] == run['id'] and row['sequence'] == args.battle_sequence]
        first = dict(chosen, opening=json.loads(records[0]['opening_json']),
            snapshots={row['group_name']: json.loads(row['payload']) for row in records if row['payload']})
        battles = [chosen] + [item for item in battles if item['id'] != chosen['id']]
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
        return next((item for item in items if (item.get('resource-id') == 'android:id/text1' and needle in item.get('text', '') or needle in item.get('content-desc', '')) and 'bounds' in item), None)

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
        if args.expect_artwork:
            check(any(item.get('content-desc', '').startswith('hero-') and '图片已加载' in item.get('content-desc', '') for item in items), 'cached hero artwork is decoded and bound in the run list')
        tap(find(run['hero']))
        items = layout()
        check('本局 PvP 交手记录' in texts(items) and f"{run['battleCount']} 场已存 PvP 交手" in texts(items), 'run identity and persisted battle count match the database')
        screenshot('battles-portrait')
        first_row = find(f"#{battles[0]['sequence']} ·")
        first_bounds = first_row['bounds']
        tap(first_row)
        items = layout()
        check('双方阵容' in texts(items), 'selected battle opens its own snapshot page')
        cards = first['snapshots']['playerHand']['cards']
        if cards:
            card = cards[0]
            target = next(item for item in layout() if item.get('content-desc', '').startswith('card-' + card['templateId']) and 'bounds' in item)
            tap(target)
            check('双方阵容' in texts(layout()) and card['instanceId'] not in texts(layout()), 'item tap keeps the screenshot sheet open without a details popup')
            tap(target, hold=True)
            check('双方阵容' in texts(layout()) and card['instanceId'] not in texts(layout()), 'item long press does not open stored fields')
        def complete_sheet(items, selected, label):
            sheet = next(item for item in items if item.get('content-desc') == '完整对战阵容 · 所有卡牌在一屏内展示' and 'bounds' in item)
            box = list(map(int, re.findall(r'\d+', sheet['bounds'])))
            images = [item for item in items if item.get('content-desc', '').startswith('card-') and 'bounds' in item]
            from collections import Counter
            expected_images = Counter('card-' + card['templateId'] for group in selected['snapshots'].values() for card in group['cards'])
            actual_images = Counter(item['content-desc'].split(' · ')[0] for item in images)
            check(actual_images == expected_images, label + ': every saved card image is present simultaneously')
            for item in images:
                left, top, right, bottom = map(int, re.findall(r'\d+', item['bounds']))
                check(box[0] <= left < right <= box[2] and box[1] <= top < bottom <= box[3], label + ': card image stays inside the sheet')
            check(not any(item.get('class') in ['android.widget.ListView', 'android.widget.ScrollView', 'android.widget.HorizontalScrollView'] for item in items), label + ': formation has no scrolling container')
            item_keys = {'card-' + card['templateId'] for key in ['playerHand', 'opponentHand'] for card in selected['snapshots'][key]['cards']}
            skill_keys = {'card-' + card['templateId'] for key in ['playerSkills', 'opponentSkills'] for card in selected['snapshots'][key]['cards']}
            native_profiles = {row['size']: row['width'] / row['height'] for row in json.loads(args.native_ratios.read_text())} if args.native_ratios else {}
            saved_cards = [card for group in ['playerHand', 'opponentHand'] for card in selected['snapshots'][group]['cards']]
            item_heights, skill_heights = [], []
            item_tops = {}
            for image in images:
                rect = list(map(int, re.findall(r'\d+', image['bounds'])))
                key = image['content-desc'].split(' · ')[0]
                if key in item_keys:
                    item_heights.append(rect[3] - rect[1])
                    saved = next(card for card in saved_cards if 'card-' + card['templateId'] == key)
                    if saved['size'] in native_profiles:
                        check(abs((rect[2] - rect[0]) - (rect[3] - rect[1]) * native_profiles[saved['size']]) <= 1.1, label + ': item aspect matches measured native artwork geometry')
                    for side in ['playerHand', 'opponentHand']:
                        if any('card-' + card['templateId'] == key for card in selected['snapshots'][side]['cards']):
                            item_tops.setdefault(side, set()).add(rect[1])
                if key in skill_keys: skill_heights.append(rect[3] - rect[1])
                check(not image.get('clickable', False) and not image.get('long-clickable', False), label + ': image has no details action')
            check(not item_heights or max(item_heights) - min(item_heights) <= 1, label + ': all item image heights are equal across both sides')
            check(all(len(tops) == 1 for tops in item_tops.values()), label + ': each side displays all items in exactly one row')
            density = int(re.findall(r'\d+', shell('wm', 'density'))[-1]) / 160
            if box[3] - box[1] >= box[2] - box[0]:
                opponent = next(item for item in items if item.get('text', '').startswith('对方：') and 'bounds' in item)
                opponent_top = int(re.findall(r'\d+', opponent['bounds'])[1])
                player_skills = next(item for item in items if item.get('text', '').startswith('我方技能 · ') and 'bounds' in item)
                player_bottom = int(re.findall(r'\d+', player_skills['bounds'])[3])
                for item in images:
                    rect = list(map(int, re.findall(r'\d+', item['bounds'])))
                    if rect[1] < opponent_top:
                        player_bottom = max(player_bottom, rect[3])
                check(0 <= opponent_top - player_bottom <= 8 * density + 1,
                    label + ': opponent follows actual player content with a compact gap')
            check(not skill_heights or max(skill_heights) <= 22 * density + 1, label + ': skills use at most one quarter of the former 88dp image edge')
            check('品质 ' not in texts(items) and all(card['name'] not in texts(items) for group in selected['snapshots'].values() for card in group['cards']), label + ': image sheet displays no card names or quality captions')
            joined = texts(items)
            for key, name in [('playerHand', '我方物品'), ('playerSkills', '我方技能'), ('opponentHand', '对方物品'), ('opponentSkills', '对方技能')]:
                state = {'captured': '已完整采集', 'partial': '部分采集', 'failed': '采集失败', 'unavailable': '未采集'}[selected['snapshots'][key]['status']]
                check(name + ' · ' + state in joined, label + ': ' + name + ' capture state remains visible')
        complete_sheet(layout(), first, 'portrait')
        shell('input', 'keyevent', '4')
        check('本局 PvP 交手记录' in texts(layout()), 'back returns to the battle list')
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
        if not pending:
            complete_sheet(items, first, 'landscape')
        screenshot('snapshot-landscape')
        shell('settings', 'put', 'system', 'user_rotation', '0')
        time.sleep(2)
        portrait_items = layout()
        check('双方阵容' in texts(portrait_items), 'selected snapshot survives portrait recreation')
        if not pending:
            complete_sheet(portrait_items, first, 'portrait after recreation')
        screenshot('snapshot-portrait')
        shell('input', 'keyevent', '4')
        shell('input', 'keyevent', '4')
        check('对局历史' in texts(layout()), 'back restores the run list')
        def tap_text(label):
            tap(next(item for item in layout() if item.get('text') == label and 'bounds' in item))
        all_heroes = ['Vanessa', 'Pygmalien', 'Dooley', 'Mak', 'Stelle', 'Jules', 'Karnok', 'TheDragons']
        saved_heroes = {item['hero'] for item in expected['browserRuns']['items']}
        absent = next((hero for hero in all_heroes if hero not in saved_heroes), None) if not expected['browserRuns']['hasMore'] else None
        if absent:
            tap_text('全部英雄')
            tap_text(absent)
            check('此页没有记录。' in texts(layout()), 'hero filter shows an explicit empty result without unrelated runs')
            tap_text('英雄：' + absent)
        else:
            tap_text('全部英雄')
        check(any(item.get('content-desc', '').startswith('hero-') and '图片已加载' in item.get('content-desc', '') for item in layout()), 'hero filter displays portraits beside hero names')
        screenshot('hero-filter')
        tap_text(run['hero'])
        check(f"{run['battleCount']} 场已存 PvP 交手" in texts(layout()), 'hero filter reloads matching run summaries')
        tap(find(run['hero']))
        shell('input', 'keyevent', '4')
        check('英雄：' + run['hero'] in texts(layout()), 'return from run detail preserves the selected hero filter')
        tap_text('英雄：' + run['hero'])
        tap_text('全部英雄')
        check('全部英雄' in texts(layout()), 'all-heroes filter is restored after verification')
    finally:
        for key, value in original.items():
            shell('settings', 'delete' if value == 'null' else 'put', 'system', key, *([] if value == 'null' else [value]))
        (args.output / 'checks.json').write_text(json.dumps({'passedChecks': checks, 'rotationRestored': True}, ensure_ascii=False, indent=2) + '\n')
    (args.output / 'passed.json').write_text(json.dumps({'passed': True, 'checks': len(checks)}, indent=2) + '\n')


if __name__ == '__main__':
    main()

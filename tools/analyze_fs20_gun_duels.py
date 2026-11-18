"""Summarize completed native gun-duel rounds, including unlimited-ammo counters.

Percentages count samples; command suppression alone does not prove a bad limiter.
Usage: python tools/analyze_fs20_gun_duels.py OUTPUT_DIRECTORY
"""
import argparse
import json
from pathlib import Path


def quant(values, percentile):
    values = sorted(values)
    return round(values[int((len(values) - 1) * percentile)], 2) if values else None


def metrics(samples):
    if not samples:
        return None
    roll = [x for x in samples if abs(x['rawRoll']) > .6]
    pitch = [x for x in samples if abs(x['rawPitch']) > .7]
    pct = lambda count, total: round(100 * count / max(1, total), 1)
    return dict(
        samples=len(samples),
        speedMin=quant([x['speed'] for x in samples], 0),
        speedMedian=quant([x['speed'] for x in samples], .5),
        aoaP95=quant([abs(x['alpha']) for x in samples], .95),
        gP95=quant([x['g'] for x in samples], .95),
        rollRateP95=quant([abs(x['rollRate']) for x in samples], .95),
        below140Pct=pct(sum(x['speed'] < 140 for x in samples), len(samples)),
        rollDemandSamples=len(roll),
        slowRollDemandPct=pct(sum(abs(x['rollRate']) < 8 for x in roll), len(roll)),
        pitchSuppressionPct=pct(sum(abs(x['filteredPitch']) < .2 for x in pitch), len(pitch)),
        rollCommandReversals=sum(
            1 for x, y in zip(samples, samples[1:])
            if x['rawRoll'] * y['rawRoll'] < 0 and abs(x['rawRoll'] - y['rawRoll']) > .8
        ),
        minAltitude=quant([x['altitude'] for x in samples], 0),
        gunModePct=pct(sum(x['mode'] == 'UsingFixedGuns' for x in samples), len(samples)),
        closeAimPct=pct(sum(x['angle'] < 10 and x['distance'] < 1500 for x in samples), len(samples)),
    )


def analyze(root):
    receipt = json.loads((root / 'duels.json').read_text(encoding='utf-8-sig'))
    rows = []
    for case in receipt['rounds']:
        data = [json.loads(line) for line in (root / (case['label'] + '.jsonl')).read_text().splitlines()]
        first_shot = min((x['t'] for x in data if x.get('shotsFired', 0) > 0), default=None)
        if first_shot is None and 'shotsFired' not in case:
            first_shot = min((x['t'] for x in data if x['ammo'] < case['initial'][x['side']]['ammo']), default=None)
        for side in (0, 1):
            samples = [x for x in data if x['side'] == side]
            if not samples:
                raise ValueError('Completed round has no telemetry: ' + case['label'])
            shots = case['shotsFired'][side] if 'shotsFired' in case else max(0, case['initial'][side]['ammo'] - case['end'][side]['ammo'])
            gun = [x for x in samples if x['mode'] == 'UsingFixedGuns']
            captured = [x for x in gun if x.get('capture') and (x['capture'].get('weight') or 0) > 0]
            approach = [x for x in gun if x.get('capture') and (x['capture'].get('throttleCeiling') or 1) < .999]
            rows.append(dict(
                case=case['label'], side=side, variant=samples[0]['variant'],
                seconds=round(case['elapsed'], 2), outcome=case['outcome'],
                shotsUsed=shots, endAlive=case['end'][side]['alive'],
                releaseFuel=case['initial'][side]['fuel'], corner=case['initial'][side]['corner'],
                ammoMinimum=min(x['ammo'] for x in samples),
                timeScales=sorted(set(x.get('timeScale') for x in samples)),
                cameraStates=sorted(set(x.get('cameraState', 'unrecorded') for x in samples)),
                initialSpeedKmh=round(samples[0]['speed'] * 3.6, 2),
                initialSeparation=samples[0]['distance'],
                closestSeparation=min(x['distance'] for x in samples),
                firstDetachedTime=next((x['t'] for x in samples if x['partsLost'] > 0), None),
                maxPartsLost=max(x['partsLost'] for x in samples),
                firstShotEitherSide=first_shot,
                gunLeadMedian=quant([x['leadError'] for x in gun], .5),
                geometricGunGatePct=round(100 * sum(x['leadError'] < x['gunGate'] for x in gun) / max(1, len(gun)), 2),
                captureSamples=len(captured),
                captureWeightMax=max((x['capture']['weight'] for x in captured), default=0),
                capturedPointMeanError=round(sum(abs(x['capture']['angle'] - x['leadError']) for x in captured) / max(1, len(captured)), 5),
                approachManagementSamples=len(approach),
                minimumThrottleCeiling=min((x['capture']['throttleCeiling'] for x in approach), default=1),
                unchangedOutputChecks=case.get('nativeChecks', [None, None])[side],
                captureOutputChecks=case.get('captureChecks', [None, None])[side],
                fullCommandChecks=case.get('fullCommandChecks', [None, None])[side],
                # No gunfire yet does not exclude collisions or structural damage.
                beforeAnyShot=metrics([x for x in samples if x['t'] > 5 and (first_shot is None or x['t'] < first_shot)]),
                **metrics(samples),
            ))
    (root / 'metrics.json').write_text(json.dumps(rows, indent=2), encoding='utf-8')
    return rows


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    for x in analyze(args.output):
        print(f"{x['case']:12} side={x['side']} shots={x['shotsUsed']:4} "
              f"speedMin={x['speedMin']:6} AoAp95={x['aoaP95']:5} "
              f"duration={x['seconds']:6} alive={x['endAlive']} {x['outcome']}")

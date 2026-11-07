"""Author Corbie's secondary joint curves; stdlib only, no runtime Python process."""
import argparse
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parent
CHANNELS = ('neckX', 'neckY', 'tail', 'wingTip', 'torso', 'leftKnee', 'rightKnee')
LIMITS = (4, 3, 12, 25, 5, 10, 10)
STATES = ('idle', 'walk', 'talk', 'stretch', 'preen', 'sleep', 'flight', 'dance')


def pose(state, phase):
    a = phase * math.tau
    s, c = math.sin(a), math.cos(a)
    values = dict.fromkeys(CHANNELS, 0.0)
    if state == 'idle':
        values.update(neckY=.35*s, tail=2*s, wingTip=1.5*c)
    elif state == 'walk':
        values.update(neckX=-.8*s, neckY=-.8*abs(s), tail=-5*s, wingTip=4*s,
                      torso=1.5*s, leftKnee=8*max(0, s), rightKnee=8*max(0, -s))
    elif state == 'talk':
        # A nod and small lean, with a quieter half of each gesture cycle.
        pulse = max(0, s)**2
        values.update(neckX=.9*s, neckY=-1.6*pulse, tail=2*math.sin(a-.5),
                      wingTip=3*pulse, torso=1.2*pulse)
    elif state == 'stretch':
        values.update(neckY=-1.5, tail=-7, wingTip=18, torso=-2)
    elif state == 'preen':
        values.update(neckX=-2.2, neckY=1.2, tail=3*s, wingTip=10, torso=-1)
    elif state == 'sleep':
        values.update(neckX=-1.5, neckY=1.8+.2*s, tail=-3, wingTip=-2, torso=1)
    elif state == 'flight':
        values.update(neckX=1, tail=-6+3*s, wingTip=12+12*math.sin(a-.55),
                      torso=-2, leftKnee=6, rightKnee=6)
    elif state == 'dance':
        values.update(neckX=1.5*s, neckY=-abs(s), tail=8*math.sin(a-.4),
                      wingTip=8*math.sin(a-.7), torso=2*s,
                      leftKnee=5*max(0, s), rightKnee=5*max(0, -s))
    else:
        raise ValueError('Unknown pose state: ' + state)
    return {key: round(values[key], 5) for key in CHANNELS}


def library():
    return dict(version=1, frames={state: [pose(state, i/48) for i in range(48)] for state in STATES})


def validate(data):
    assert data['version'] == 1 and set(data['frames']) == set(STATES)
    for frames in data['frames'].values():
        assert len(frames) == 48
        for frame in frames:
            assert set(frame) == set(CHANNELS)
            assert all(math.isfinite(frame[key]) and abs(frame[key]) <= bound for key, bound in zip(CHANNELS, LIMITS))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true', help='check authored data without rewriting it')
    args = parser.parse_args()
    data = library()
    validate(data)
    target = ROOT / 'avian-poses.json'
    if args.check:
        assert json.loads(target.read_text(encoding='utf-8')) == data, 'Generated poses differ; regenerate them.'
        print('PASS: 384 authored poses, bounded joints and deterministic export.')
    else:
        target.write_text(json.dumps(data, separators=(',', ':')) + '\n', encoding='utf-8')
        print('Wrote ' + str(target))


if __name__ == '__main__':
    main()

"""Visible depth only: keep doorway widths instead of three coarse readings."""
import numpy as np


def scan(depth):
    depth = np.asarray(depth)
    if depth.ndim != 2 or min(depth.shape) < 4 or depth.shape[1] < 6 or not np.isfinite(depth).all():
        raise ValueError('Missing or invalid depth vision; paused.')
    h, w = depth.shape
    columns = np.median(depth[int(h * .38):int(h * .52)], axis=0)
    front = float(np.percentile(columns[int(w * .42):int(w * .58)], 30))
    left = float(np.percentile(columns[:int(w * .4)], 70))
    right = float(np.percentile(columns[int(w * .6):], 70))
    openings = []
    start = None
    for index, clear in enumerate(np.r_[columns >= 12, False]):
        if clear and start is None:
            start = index
        elif not clear and start is not None:
            span = (index - start) / w
            if span >= .14:
                openings.append(dict(center=(start + index) / (2 * w), width=span,
                                     clearance=float(np.percentile(columns[start:index], 30))))
            start = None
    preferred = max(openings, key=lambda gap: gap['width'] - abs(gap['center'] - .5) * .45,
                    default=None)
    return dict(left=left, front=front, right=right,
                sectors=[float(np.percentile(part, 30)) for part in np.array_split(columns, min(9, w))],
                openings=openings, preferred=preferred)

"""Check actual captured cloth triangles for nonconforming scanline edges.

Input is TowerLimbAudit's little-endian *-cast-mesh.bin export. This compares
explicit rendered vertices with neighboring triangle edges at the same original
painted position; no runtime skinning implementation is reproduced here.
"""
import argparse
import json
import struct
from collections import defaultdict
from pathlib import Path

import numpy as np


def inspect(path):
    data = path.read_bytes()
    count, index_count, texture_width, texture_height = struct.unpack_from("<4i", data)
    rect_x, rect_y, width, height, ground_x, ground_y, body_height = struct.unpack_from("<7f", data, 16)
    dtype = np.dtype([
        ("rest", "<f4", (2,)), ("posed", "<f4", (2,)),
        ("part", "<i4"), ("uv", "<f4", (2,)),
    ])
    vertices = np.frombuffer(data, dtype, count=count, offset=44)
    triangles = np.frombuffer(data, "<i4", count=index_count, offset=44 + count * 28).reshape(-1, 3)
    unit = 1.75 / body_height
    rest = np.column_stack((
        vertices["rest"][:, 0] / unit + ground_x,
        height - vertices["rest"][:, 1] / unit - ground_y,
    ))
    posed = vertices["posed"]
    rows = defaultdict(set)
    for triangle in triangles:
        for first, second in ((triangle[0], triangle[1]), (triangle[1], triangle[2]), (triangle[2], triangle[0])):
            if vertices["part"][first] != 13 or vertices["part"][second] != 13:
                continue
            if abs(rest[first, 1] - rest[second, 1]) > .001 or abs(rest[first, 0] - rest[second, 0]) < .001:
                continue
            if rest[first, 0] > rest[second, 0]:
                first, second = second, first
            rows[round(float(rest[first, 1]), 3)].add((int(first), int(second)))
    maximum = 0.0
    worst = None
    compared = 0
    for row, edges in rows.items():
        endpoints = {first for first, _ in edges} | {second for _, second in edges}
        for first, second in edges:
            left, right = rest[first, 0], rest[second, 0]
            for vertex in endpoints:
                x = rest[vertex, 0]
                if not left + .001 < x < right - .001:
                    continue
                fraction = (x - left) / (right - left)
                interpolated = posed[first] * (1 - fraction) + posed[second] * fraction
                difference = float(np.linalg.norm(posed[vertex] - interpolated))
                compared += 1
                if difference > maximum:
                    maximum = difference
                    worst = {"sourceTop": [float(x), row], "edgeX": [float(left), float(right)]}
    return {"file": path.name, "vertices": count, "junctionComparisons": compared,
            "maxEdgeGapWorld": maximum, "worst": worst}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("files", nargs="+", type=Path)
    parser.add_argument("--limit", type=float, default=.001)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    results = [inspect(path) for path in args.files]
    report = {"limitWorld": args.limit, "passed": all(result["maxEdgeGapWorld"] <= args.limit for result in results),
              "results": results}
    rendered = json.dumps(report, ensure_ascii=False, indent=2)
    if args.output:
        args.output.write_text(rendered + "\n", encoding="utf-8")
    print(rendered)
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())

# Usage:
# python chipmaker.py inner-diameter outer-diameter height nfaces
# Creates the outer shell of a chip with the given shape.
# nfaces should be an even number.
# The bottom of the chip is centered at the origin.

import math
import sys

if len(sys.argv) == 1:
    ID, OD, H = map(float, map(input, ['ID=', 'OD=', 'H=']))
    N = int(input('N='))
else:
    ID, OD, H = map(float, sys.argv[1:4])
    N = int(sys.argv[4])

# Vertices
# Each ring starts east and goes counterclockwise.
#    1 to  N : bottom inner
#  N+1 to 2N : bottom outer
# 2N+1 to 3N : top inner
# 3N+1 to 4N : top outer
for z in [0, H]:
    for r in [ID/2, OD/2]:
        for i in range(N):
            x = r * math.cos(2*math.pi * i / N)
            y = r * math.sin(2*math.pi * i / N)
            print('v %f %f %f' % (x, y, z))

# UV values
#      1/32  equal space  31/32
# 0.875 +---+---+       +---+
#       |   |   |       |   |  (inner, solid color)
# 0.75  +---+---+       +---+
#       |   |   |       |   |
#       |   |   |       |   |  (top of chip)
#       |   |   |       |   |
# 0.5   +---+---+ . . . +---+
#       |   |   |       |   |  (outer, striped)
# 0.375 +---+---+       +---+
#       |   |   |       |   |
#       |   |   |       |   |  (bottom of chip)
#       |   |   |       |   |
# 0.125 +---+---+       +---+
#        \__ N+1 points ___/
for v in [0.875, 0.75, 0.5, 0.375, 0.125]:
    for i in range(N + 1):
        u = (1 + 30.0 * i / N) / 32.0
        print('vt %f %f' % (u, v))

# Normals
# 1 to N: starts east, goes counterclockwise
# N+1: up
# N+2: down
for i in range(N):
    x = math.cos(2*math.pi * i / N)
    y = math.sin(2*math.pi * i / N)
    print('vn %f %f 0' % (x, y))
print('vn 0 0 1')
print('vn 0 0 -1')

# Faces
for i in range(N):
    a = i % N + 1
    b = (i + 1) % N + 1
    # Inner
    vertices = (a, a+2*N, b+2*N, b)
    textures = (a, a+N+1, a+N+2, a+1)
    norm1 = a + N/2
    norm2 = b + N/2
    if norm1 > N:
        norm1 -= N
    if norm2 > N:
        norm2 -= N
    normals = (norm1, norm1, norm2, norm2)
    values = sum(zip(vertices, textures, normals), ())
    print('f %d/%d/%d %d/%d/%d %d/%d/%d %d/%d/%d' % values)
    # Top
    vertices = (a+2*N, a+3*N, b+3*N, b+2*N)
    textures = (a+N+1, a+2*N+2, a+2*N+3, a+N+2)
    normals = (N+1, N+1, N+1, N+1)
    values = sum(zip(vertices, textures, normals), ())
    print('f %d/%d/%d %d/%d/%d %d/%d/%d %d/%d/%d' % values)
    # Outer
    vertices = (a+3*N, a+N, b+N, b+3*N)
    textures = (a+2*N+2, a+3*N+3, a+3*N+4, a+2*N+3)
    normals = (a, a, b, b)
    values = sum(zip(vertices, textures, normals), ())
    print('f %d/%d/%d %d/%d/%d %d/%d/%d %d/%d/%d' % values)
    # Bottom
    vertices = (a+N, a, b, b+N)
    textures = (a+3*N+3, a+4*N+4, a+4*N+5, a+3*N+4)
    normals = (N+2, N+2, N+2, N+2)
    values = sum(zip(vertices, textures, normals), ())
    print('f %d/%d/%d %d/%d/%d %d/%d/%d %d/%d/%d' % values)
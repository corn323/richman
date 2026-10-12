import sys, importlib
sys.path.insert(0, r"C:\Users\User\Desktop\code\richman\tools")
import chargen as g
importlib.reload(g)
from chargen import *
import math

reset(); std_mats()
mat("Shirt", (0.12, 0.17, 0.2)); mat("Vest", (0.2, 0.3, 0.2)); mat("Pants", (0.38, 0.36, 0.24))
mat("Boot", (0.34, 0.2, 0.1)); mat("Hat", (0.42, 0.42, 0.25)); mat("Hat2", (0.32, 0.32, 0.18)); mat("Beard", (0.28, 0.2, 0.14))
mat("Hair", (0.28, 0.2, 0.14)); mat("Strap", (0.9, 0.5, 0.12)); mat("Cam", (0.25, 0.27, 0.3)); mat("Pack", (0.4, 0.25, 0.13))
mat("Gunmetal", (0.12, 0.13, 0.15)); mat("Cyan", (0.3, 0.85, 1.0))

legs("Pants", "Boot", boots=True, baggy=0.03)
for s in (-1, 1):
    cube("Pants", (s * 0.2, -0.04, 0.42), (0.08, 0.1, 0.16))
torso("Shirt", vest="Vest", bulk=1.0)
for s in (-1, 1):
    slab("Vest", (s * 0.06, -0.24, 1.24), (s * 0.15, -0.3, 0.9), 0.06, 0.03)
    cube("Vest", (s * 0.15, -0.37, 0.85), (0.1, 0.04, 0.09))
    cube("Vest", (s * 0.15, -0.37, 1.05), (0.1, 0.04, 0.08))
    slab("Strap", (s * 0.13, -0.2, 1.28), (s * 0.08, -0.34, 0.9), 0.02, 0.02)
# camera on chest
cube("Cam", (0, -0.39, 0.96), (0.2, 0.09, 0.12)); cyl("Gunmetal", (0, -0.44, 0.96), 0.045, 0.06, 8, rot=(math.pi / 2, 0, 0))
cyl("Cyan", (0, -0.475, 0.96), 0.03, 0.01, 8, rot=(math.pi / 2, 0, 0)); cube("Steel", (0.06, -0.39, 1.04), (0.05, 0.05, 0.03))
arms("Shirt", "Shirt", glove="Black")
# belt, pouches, key
loft("Belt" if False else "Gunmetal", [(0.58, .39, .33, -0.03), (0.66, .4, .34, -0.03)])
cube("Steel", (0, -0.365, 0.62), (0.1, 0.03, 0.08))
cube("Cam", (-0.28, -0.28, 0.58), (0.12, 0.1, 0.16), (0, 0, -0.4)); tube("Gold", (-0.12, -0.36, 0.6), (-0.12, -0.38, 0.46), 0.012, 4)
# tripod backpack
cube("Pack", (0, 0.36, 0.95), (0.4, 0.2, 0.5))
for dx in (-0.1, 0, 0.1): tube("Gunmetal", (dx, 0.5, 1.2), (dx * 1.8, 0.52, 0.55), 0.02, 4)
cube("Cam", (0, 0.5, 1.26), (0.14, 0.08, 0.1)); cube("Cam", (0.0, 0.45, 1.4), (0.08, 0.1, 0.12))
# head
head(hair="Hair", hair_sides=True, beard="Beard", stache="Beard", eye="Blue", brow="Beard", smile=True)
shades()
# bucket hat
loft("Hat", [(1.62, .25, .25, 0), (1.72, .27, .27, 0), (1.82, .2, .2, 0), (1.84, .17, .17, 0)])
loft("Hat2", [(1.6, .42, .42, 0), (1.64, .33, .33, 0), (1.66, .25, .25, 0)], 8)
loft("Gunmetal", [(1.66, .275, .275, 0), (1.72, .28, .28, 0)])
for s in (-1, 1):
    torus("Steel", (s * 0.1, -0.29, 1.78), 0.065, 0.016, (math.pi / 2, 0, 0))
    cyl("Cyan" if s < 0 else "Gunmetal", (s * 0.1, -0.29, 1.78), 0.062, 0.012, 8, rot=(math.pi / 2, 0, 0))
# cane: rainbow + scale
cane_shaft("rainbow")
scale_head(-0.5, -0.15, 1.05)
print(finish("Photographer"))

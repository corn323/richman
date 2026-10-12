import sys, importlib
sys.path.insert(0, r"C:\Users\User\Desktop\code\richman\tools")
import chargen as g
importlib.reload(g)
from chargen import *
import math

reset(); std_mats()
mat("Coat", (0.14, 0.27, 0.27)); mat("Coat2", (0.1, 0.2, 0.22)); mat("Vest", (0.45, 0.08, 0.14)); mat("Stripe", (0.95, 0.6, 0.12))
mat("Pants", (0.1, 0.13, 0.18)); mat("Boot", (0.32, 0.18, 0.1)); mat("Hat", (0.5, 0.08, 0.16)); mat("Hat2", (0.65, 0.4, 0.15))
mat("Beard", (0.2, 0.13, 0.1)); mat("Pack", (0.45, 0.27, 0.13)); mat("Cravat", (0.9, 0.55, 0.2)); mat("Cravat2", (0.3, 0.2, 0.5))
mat("Pouch", (0.4, 0.25, 0.12)); mat("Hair", (0.2, 0.13, 0.1))

legs("Pants", "Boot", boots=True, baggy=0.03)
for s in (-1, 1): cube("Gold", (s * 0.12, -0.12, 0.2), (0.08, 0.03, 0.06))
torso("Coat", vest="Vest", bulk=1.0)
for s in (-1, 1):
    slab("Stripe", (s * 0.07, -0.25, 1.22), (s * 0.07, -0.34, 0.65), 0.025, 0.03)
    slab("Coat2", (s * 0.06, -0.22, 1.25), (s * 0.19, -0.3, 0.9), 0.08, 0.03)
    cube("Coat2", (s * 0.3, -0.3, 0.98), (0.1, 0.04, 0.09), (0, 0, s * 0.4))
for z in (0.7, 0.8, 0.9): ico("Gold", (0, -0.36, z), 0.022)
# cravat
slab("Cravat", (0, -0.26, 1.24), (0, -0.33, 1.05), 0.1, 0.03); cube("Cravat2", (0.02, -0.34, 1.12), (0.04, 0.01, 0.06))
ico("Cravat2", (0, -0.26, 1.23), 0.04, (1.3, 0.8, 1))
# magnifier badge on chest + purple book
cube("Purple", (-0.24, -0.33, 1.02), (0.06, 0.03, 0.08), (0, 0, 0.4))
torus("Gold", (0.24, -0.33, 1.08), 0.03, 0.007, (math.pi / 2, 0, 0)); cyl("Glass", (0.24, -0.33, 1.08), 0.028, 0.004, 8, rot=(math.pi / 2, 0, 0))
arms("Coat", "Coat2", glove="Skin")
ico("Gold", (0.5, -0.04, 0.88), 0.02); ico("Gold", (-0.46, -0.1, 0.91), 0.02)
# belt, pouch, keys
loft("Boot", [(0.58, .39, .33, -0.03), (0.66, .4, .34, -0.03)])
cube("Steel", (0, -0.365, 0.62), (0.1, 0.03, 0.08)); cube("Pouch", (0.26, -0.3, 0.55), (0.14, 0.1, 0.14), (0, 0, 0.45))
tube("Gold", (-0.2, -0.35, 0.62), (-0.2, -0.38, 0.46), 0.012, 4); cube("Purple", (-0.26, -0.35, 0.5), (0.06, 0.03, 0.08))
cube("Pouch", (-0.28, -0.26, 0.55), (0.12, 0.1, 0.16), (0, 0, -0.4))
# backpack and suspender straps
cube("Pack", (0, 0.36, 0.95), (0.4, 0.2, 0.5)); cube("Pouch", (0, 0.5, 0.8), (0.26, 0.08, 0.2))
cube("Red", (-0.12, 0.38, 1.22), (0.05, 0.1, 0.1)); cube("Green", (-0.05, 0.38, 1.22), (0.05, 0.1, 0.1))
# head
head(hair="Hair", hair_sides=True, beard="Beard", stache="Beard", eye="Blue", brow="Beard", smile=True)
glasses("Gold", lens="Glass", sq=False, r=0.045)
# cap (maroon) with magnifier
dome("Hat", z0=1.6, h=0.23, r=0.26, bulge=0.285)
cube("Hat2", (0.1, -0.15, 1.78), (0.14, 0.2, 0.06), (0, 0.0, 0.4))
brim_front("Hat", z=1.69, w=0.34, d=0.2)
torus("Gold", (0.19, -0.27, 1.76), 0.04, 0.009, (math.pi / 2, 0, 0)); cyl("Glass", (0.19, -0.27, 1.76), 0.037, 0.005, 8, rot=(math.pi / 2, 0, 0))
tube("Gold", (0.22, -0.27, 1.72), (0.27, -0.27, 1.66), 0.008, 4)
# cane: rainbow + scale
cane_shaft("rainbow")
scale_head(-0.5, -0.15, 1.05)
print(finish("Shopkeeper"))

import csv
import math
import pathlib
import sys

import matplotlib.pyplot as plt
import numpy as np
from matplotlib.patches import Circle

csv_path = pathlib.Path(sys.argv[1])
output = pathlib.Path(sys.argv[2])
with csv_path.open() as stream:
    rows = list(csv.DictReader(stream))
xyz = np.array([[float(r["x_mm"]), float(r["y_mm"]), float(r["z_mm"])] for r in rows])

sphere_r = 7.5
gtv_r = 60.0
contracted_r = 55.0
ctc = 40.0

fig = plt.figure(figsize=(14, 7))
ax = fig.add_subplot(121, projection="3d")
u = np.linspace(0, 2*np.pi, 32)
v = np.linspace(0, np.pi, 17)

def surface(center, radius, color, alpha):
    x = center[0] + radius*np.outer(np.cos(u), np.sin(v))
    y = center[1] + radius*np.outer(np.sin(u), np.sin(v))
    z = center[2] + radius*np.outer(np.ones_like(u), np.cos(v))
    ax.plot_surface(x, y, z, color=color, alpha=alpha, linewidth=0)

surface((0,0,0), contracted_r, "#78909c", 0.07)
for p in xyz:
    surface(p, sphere_r, "#d32f2f", 0.78)
ax.scatter(xyz[:,0], xyz[:,1], xyz[:,2], color="#7f0000", s=14, depthshade=False)
ax.set(xlabel="LR x (mm)", ylabel="AP y (mm)", zlabel="SI z (mm)",
       title="3D lattice: 13 complete 15 mm spheres")
ax.set_box_aspect((1,1,1))
ax.set_xlim(-65,65); ax.set_ylim(-65,65); ax.set_zlim(-65,65)
ax.view_init(elev=23, azim=-52)

ax2 = fig.add_subplot(122)
ax2.add_patch(Circle((0,0),gtv_r,fill=False,color="#263238",lw=2,label="GTV phantom (R=60 mm)"))
ax2.add_patch(Circle((0,0),contracted_r,fill=False,color="#607d8b",lw=1.8,ls="--",label="5 mm contraction"))
on_plane=[]
for row,p in zip(rows,xyz):
    dz=abs(p[2])
    if dz < sphere_r:
        cross=math.sqrt(sphere_r*sphere_r-dz*dz)
        ax2.add_patch(Circle((p[0],p[1]),cross,color="#ef5350",alpha=.72))
        ax2.text(p[0],p[1],row["id"].replace("LAT_V",""),ha="center",va="center",fontsize=7)
        on_plane.append(p)
ax2.set_aspect("equal")
ax2.set_xlim(-65,65); ax2.set_ylim(-65,65)
ax2.grid(alpha=.2)
ax2.set(xlabel="LR x (mm)",ylabel="AP y (mm)",title="Axial plane z = 0 mm")
ax2.legend(loc="upper right",fontsize=8)

fig.suptitle("Local Gaudreault-style lattice verification\n15 mm diameter, 40 mm centre-to-centre spacing",fontsize=15,weight="bold")
fig.subplots_adjust(left=.055,right=.98,bottom=.14,top=.81,wspace=.22)
fig.text(.5,.025,"Analytic test phantom only. Complete spheres constrained inside the 5 mm contracted target; minimum pair distance = 40.000 mm.",ha="center",fontsize=9)
fig.savefig(output,dpi=180,facecolor="white")
print(f"WROTE {output} ({len(rows)} vertices; {len(on_plane)} intersect z=0 plane)")

import csv, pathlib, sys
import numpy as np
import matplotlib.pyplot as plt
from matplotlib.patches import Circle

summary_path, centres_path, output_path = map(pathlib.Path, sys.argv[1:])
with summary_path.open() as f: summary=list(csv.DictReader(f))
with centres_path.open() as f: centres=list(csv.DictReader(f))
for r in summary:
    for k in set(r)-{"status"}: r[k]=float(r[k])

lobes=np.array([[0,0,0,55,43,48],[38,9,7,38,29,34],[-34,-17,-13,33,35,29],[5,31,-18,31,25,27]],float)
scales=sorted(set(r["scale"] for r in summary)); nominal={r["scale"]:r for r in summary if r["phase_id"]==0}
chosen=1.25
points=np.array([[float(r["x_mm"]),float(r["y_mm"]),float(r["z_mm"])] for r in centres if float(r["scale"])==chosen and int(r["phase_id"])==0])

def inside(x,y,z,scale):
    result=np.zeros(np.broadcast(x,y,z).shape,dtype=bool)
    for cx,cy,cz,a,b,c in lobes:
        result |= ((x-scale*cx)/(scale*a))**2+((y-scale*cy)/(scale*b))**2+((z-scale*cz)/(scale*c))**2 <= 1
    return result

fig,axs=plt.subplots(2,2,figsize=(14,10));fig.subplots_adjust(left=.07,right=.97,bottom=.08,top=.88,hspace=.3,wspace=.25)
grid=np.linspace(-105,110,600); X,Y=np.meshgrid(grid,grid)
mask=inside(X,Y,np.zeros_like(X),chosen)
axs[0,0].contourf(X,Y,mask,levels=[.5,1.5],colors=["#cfd8dc"],alpha=.8)
axs[0,0].contour(X,Y,mask,levels=[.5],colors=["#263238"],linewidths=2)
for i,p in enumerate(points):
    if abs(p[2])<7.5:
        rr=np.sqrt(7.5**2-p[2]**2);axs[0,0].add_patch(Circle((p[0],p[1]),rr,color="#ef5350",alpha=.8))
axs[0,0].set(title=f"Irregular phantom, axial z=0 (scale {chosen})",xlabel="LR x (mm)",ylabel="AP y (mm)",aspect="equal",xlim=(-105,110),ylim=(-105,110))

X,Z=np.meshgrid(grid,grid);mask=inside(X,np.zeros_like(X),Z,chosen)
axs[0,1].contourf(X,Z,mask,levels=[.5,1.5],colors=["#cfd8dc"],alpha=.8);axs[0,1].contour(X,Z,mask,levels=[.5],colors=["#263238"],linewidths=2)
for p in points:
    if abs(p[1])<7.5: axs[0,1].add_patch(Circle((p[0],p[2]),np.sqrt(7.5**2-p[1]**2),color="#ef5350",alpha=.8))
axs[0,1].set(title="Coronal y=0",xlabel="LR x (mm)",ylabel="SI z (mm)",aspect="equal",xlim=(-105,110),ylim=(-105,110))

vol=np.array([nominal[s]["volume_cc"] for s in scales]); count=np.array([nominal[s]["vertex_count"] for s in scales])
mins=np.array([min(r["vertex_count"] for r in summary if r["scale"]==s) for s in scales]);maxs=np.array([max(r["vertex_count"] for r in summary if r["scale"]==s) for s in scales])
axs[1,0].fill_between(vol,mins,maxs,color="#90caf9",alpha=.5,label="±5 mm phase range");axs[1,0].plot(vol,count,"o-",color="#1565c0",label="Nominal centroid phase")
axs[1,0].axhline(2,color="#c62828",ls="--",lw=1,label="Minimum lattice count")
axs[1,0].set(title="Vertex-count variability with tumour size",xlabel="Irregular target volume (cc)",ylabel="Accepted vertices");axs[1,0].grid(alpha=.25);axs[1,0].legend(fontsize=8)

spacing=np.array([nominal[s]["min_spacing_mm"] for s in scales]);clearance=np.array([nominal[s]["min_extra_clearance_mm"] for s in scales])
spacing=np.where(np.isfinite(spacing),spacing,np.nan);clearance=np.where(np.isfinite(clearance),clearance,np.nan)
axs[1,1].plot(vol,spacing,"o-",color="#6a1b9a",label="Minimum pair spacing")
axs[1,1].axhline(40,color="#6a1b9a",ls="--",lw=1);axs[1,1].set(title="Nominal-phase geometric checks",xlabel="Irregular target volume (cc)",ylabel="Minimum spacing (mm)");axs[1,1].grid(alpha=.25)
axr=axs[1,1].twinx();axr.plot(vol,clearance,"s-",color="#2e7d32",label="Extra containment clearance");axr.axhline(0,color="#2e7d32",ls=":",lw=1);axr.set_ylabel("Extra clearance beyond required 12.5 mm (mm)",color="#2e7d32")
lines=axs[1,1].lines[:1]+axr.lines[:1];axs[1,1].legend(lines,[x.get_label() for x in lines],fontsize=8,loc="upper left")

fig.suptitle("Irregular-target robustness: 15 mm spheres, 40 mm CTC, 5 mm target contraction",fontsize=15,weight="bold")
fig.text(.5,.025,"90 trials: 10 scaled tumour sizes × 9 lattice-phase perturbations. Containment verified with 8,198 surface directions per accepted centre.",ha="center",fontsize=9)
fig.savefig(output_path,dpi=180,facecolor="white")
print(f"WROTE {output_path}; trials={len(summary)}; selected_vertices={len(points)}")

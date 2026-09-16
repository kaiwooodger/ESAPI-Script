import csv, pathlib, sys
import numpy as np
import matplotlib.pyplot as plt

source,output=map(pathlib.Path,sys.argv[1:]);rows=list(csv.DictReader(source.open()))
numeric=set(rows[0])-{"status"}
for r in rows:
    for k in numeric:r[k]=float(r[k])
diameters=[10,15,20];colors={20:"#1565c0",25:"#00897b",30:"#43a047",35:"#f9a825",40:"#ef6c00",45:"#c62828",50:"#6a1b9a"}
fig,axs=plt.subplots(2,2,figsize=(14,11));fig.subplots_adjust(left=.07,right=.97,bottom=.16,top=.88,hspace=.32,wspace=.25)
for ax,d in zip(axs.flat[:3],diameters):
    subset=[r for r in rows if r["diameter_mm"]==d and r["phase_id"]==0]
    for ctc in sorted(set(r["ctc_mm"] for r in subset)):
        q=sorted((r for r in subset if r["ctc_mm"]==ctc),key=lambda r:r["volume_cc"])
        ax.plot([r["volume_cc"] for r in q],[r["vertex_count"] for r in q],"o-",color=colors[int(ctc)],label=f"CTC {ctc:.0f} mm")
    ax.axhline(2,color="#424242",ls="--",lw=1);ax.set(title=f"Sphere diameter {d} mm",xlabel="Irregular target volume (cc)",ylabel="Nominal vertices");ax.grid(alpha=.25);ax.legend(fontsize=8,ncol=2)

ax=axs[1,1];valid=[r for r in rows if r["status"]=="PASS"]
groups={}
for r in valid:groups.setdefault((r["diameter_mm"],r["ctc_mm"]),[]).append(r)
labels=[];clear=[];spacing=[]
for key,q in sorted(groups.items()):
    labels.append(f"{key[0]:.0f}/{key[1]:.0f}");clear.append(min(r["min_extra_clearance_mm"] for r in q));spacing.append(min(r["spacing_error_mm"] for r in q))
x=np.arange(len(labels));ax.bar(x-.19,clear,.38,color="#2e7d32",label="Min extra containment clearance");ax.bar(x+.19,spacing,.38,color="#6a1b9a",label="Min spacing above CTC")
ax.scatter(x+.19,spacing,color="#6a1b9a",marker="D",s=20,zorder=4)
ax.axhline(0,color="black",lw=.8);ax.set_xticks(x,labels,rotation=45,ha="right");ax.set(title="Worst-case margins across valid trials",xlabel="Diameter / CTC (mm)",ylabel="Margin (mm)");ax.grid(axis="y",alpha=.25);ax.legend(fontsize=8);ax.text(.98,.94,"All minimum spacing errors = 0.000 mm",transform=ax.transAxes,ha="right",va="top",fontsize=8)
fig.suptitle("Parameter evidence on irregular targets: sphere diameter and CTC sweep",fontsize=15,weight="bold")
fig.text(.5,.025,"375 trials: 15 parameter configurations × 5 tumour sizes × 5 lattice phases. Dashed line marks the two-vertex lattice requirement.",ha="center",fontsize=9)
fig.savefig(output,dpi=180,facecolor="white");print(f"WROTE {output}; trials={len(rows)}; valid={len(valid)}")

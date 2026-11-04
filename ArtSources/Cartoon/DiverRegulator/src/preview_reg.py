import sys; sys.path.insert(0,'/home/claude/divers')
import numpy as np, pickle
from dload import *
from PIL import Image
def preview(names,views,out_path,size=420,fov=24,target=(0,.3,0)):
    ims=[]
    for name in names:
        o=pickle.load(open('/tmp/reg_%s.pkl'%name,'rb')); at=atlas(name)
        M=dict(P=o['P'],T=o['T'],UV=o['UV'])
        cols=face_colours(M,at)
        for e in views: ims.append(render(M,cols,e,target,size=(size,size),fov=fov))
    W=len(views)*size; H=len(names)*size; out=Image.new('RGB',(W,H))
    for i,im in enumerate(ims): out.paste(im,((i%len(views))*size,(i//len(views))*size))
    out.save(out_path)
if __name__=="__main__":
    preview(('Diver','Diver_Tracker','Diver_Drone'),((0,.35,3.0),(1.9,.4,2.3),(3.0,.35,.3)),'/tmp/reg_preview.png')

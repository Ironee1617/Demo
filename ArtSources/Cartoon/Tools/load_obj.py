import numpy as np
def load_obj(path):
    V=[];VT=[];F=[]
    for l in open(path):
        if l.startswith('v '): V.append(list(map(float,l.split()[1:4])))
        elif l.startswith('vt '): VT.append(list(map(float,l.split()[1:3])))
        elif l.startswith('f '):
            idx=[w.split('/') for w in l.split()[1:]]
            F.append([(int(a[0])-1, int(a[1])-1 if len(a)>1 and a[1] else -1) for a in idx])
    return np.array(V),np.array(VT),F

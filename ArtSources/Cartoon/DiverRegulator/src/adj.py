import numpy as np
from dload import weld
def face_adjacency(P,T):
    w=weld(P); TW=w[T]; edges={}
    for f,t in enumerate(TW):
        for a,b in ((t[0],t[1]),(t[1],t[2]),(t[2],t[0])):
            edges.setdefault((min(a,b),max(a,b)),[]).append(f)
    nb=[[] for _ in range(len(T))]
    for fs in edges.values():
        for i in fs:
            for j in fs:
                if i!=j: nb[i].append(j)
    return nb,w

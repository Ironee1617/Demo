"""A rounded glove grown from the sketch contour, without an extruded rim."""
import bpy,numpy as np
from mathutils import Vector
from mathutils.geometry import interpolate_bezier,delaunay_2d_cdt
def create(outline):
 curve=bpy.data.curves.new('Sketch outline','CURVE');curve.dimensions='2D';spline=curve.splines.new('BEZIER');spline.bezier_points.add(len(outline)-1);spline.use_cyclic_u=True
 for b,(x,y) in zip(spline.bezier_points,outline):b.co=(x,y,0);b.handle_left_type=b.handle_right_type='AUTO'
 obj=bpy.data.objects.new('Contour guide',curve);bpy.context.collection.objects.link(obj);bpy.context.view_layer.update();poly=[];bp=spline.bezier_points
 for i in range(len(bp)):
  a=bp[i];b=bp[(i+1)%len(bp)];poly.extend([list(p[:2]) for p in interpolate_bezier(a.co,a.handle_right,b.handle_left,b.co,10)[:-1]])
 bpy.data.objects.remove(obj,do_unlink=True);poly=np.array(poly)
 area=np.sum(poly[:,0]*np.roll(poly[:,1],-1)-poly[:,1]*np.roll(poly[:,0],-1))
 if area<0:poly=poly[::-1]
 x,y=np.meshgrid(np.arange(poly[:,0].min(),poly[:,0].max(),.003),np.arange(poly[:,1].min(),poly[:,1].max(),.003));points=np.stack([x.ravel(),y.ravel()],1);inside=np.zeros(len(points),bool)
 for a,b in zip(poly,np.roll(poly,-1,axis=0)):
  cross=((a[1]>points[:,1])!=(b[1]>points[:,1]))&(points[:,0]<(b[0]-a[0])*(points[:,1]-a[1])/(b[1]-a[1]+1e-30)+a[0]);inside^=cross
 coords=np.r_[poly,points[inside]];out=delaunay_2d_cdt([Vector(p) for p in coords],[],[list(range(len(poly)))],1,1e-7);xy=np.array(out[0]);tri=np.array(out[2]);dist=np.full(len(xy),1.)
 for a,b in zip(poly,np.roll(poly,-1,axis=0)):
  delta=b-a;t=np.clip((xy-a)@delta/(delta@delta),0,1);dist=np.minimum(dist,np.linalg.norm(xy-a-t[:,None]*delta,axis=1))
 height=.031*np.sqrt(np.maximum(0,1-np.exp(-2*dist/.031)));height[dist<1e-6]=0
 vertices=np.c_[xy,height].tolist();lower=[]
 for i,h in enumerate(height):
  if h==0:lower.append(i)
  else:lower.append(len(vertices));vertices.append([*xy[i],-h])
 faces=tri.tolist()+[[lower[int(k)] for k in face[::-1]] for face in tri]
 mesh=bpy.data.meshes.new('Continuous rounded sketch surface');mesh.from_pydata(vertices,[],faces);mesh.update();obj=bpy.data.objects.new('One-piece sketch glove',mesh);bpy.context.collection.objects.link(obj)
 return obj

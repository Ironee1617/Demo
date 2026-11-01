using System;
namespace PrototypeLab.Aquarium
{
    // A signed distance is 1-Lipschitz. A cell's centre +/- half its diagonal
    // bounds every query in that cell; uncertain cells still use exact geometry.
    public sealed class AquariumClearanceGrid
    {
        // R48: .5 m cells (was 2 m) so swimmers near glass/decks settle on the cached bound far more often.
        const float Cell=.5f, Margin=Cell*.70711f+.001f;
        readonly float[,] distance;
        readonly float originX,originZ;
        public AquariumClearanceGrid(float minX,float minZ,float maxX,float maxZ,Func<Point,float> exact)
        {
            originX=(float)Math.Floor(minX/Cell)*Cell;originZ=(float)Math.Floor(minZ/Cell)*Cell;
            int width=Math.Max(1,(int)Math.Ceiling((maxX-originX)/Cell)),depth=Math.Max(1,(int)Math.Ceiling((maxZ-originZ)/Cell));
            distance=new float[width,depth];
            for(int x=0;x<width;x++)for(int z=0;z<depth;z++)distance[x,z]=exact(new Point(originX+(x+.5f)*Cell,originZ+(z+.5f)*Cell));
        }
        // R48: a lower bound of the field at p (false outside the grid).
        public bool TryLowerBound(Point p,out float bound)
        {
            int x=(int)Math.Floor((p.x-originX)/Cell),z=(int)Math.Floor((p.z-originZ)/Cell);bound=float.MinValue;
            if(x<0||z<0||x>=distance.GetLength(0)||z>=distance.GetLength(1))return false;
            bound=distance[x,z]-Margin;return true;
        }
        public bool TryAtLeast(Point p,float threshold,out bool result)
        {
            int x=(int)Math.Floor((p.x-originX)/Cell),z=(int)Math.Floor((p.z-originZ)/Cell);result=false;
            if(x<0||z<0||x>=distance.GetLength(0)||z>=distance.GetLength(1))return false;
            float centre=distance[x,z];
            if(centre-Margin>=threshold){result=true;return true;}
            return centre+Margin<threshold;
        }
    }
}
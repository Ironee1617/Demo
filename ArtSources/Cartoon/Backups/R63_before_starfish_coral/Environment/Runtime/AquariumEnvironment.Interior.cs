using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    public static partial class AquariumEnvironment
    {
        // A dry visitor gallery at tank-floor level, enclosed beneath the upper promenade.
        private static void BuildVisitorInterior(Transform root,Material wall,Material fixture,Material glow,AquariumEnvironmentRoot marker)
        {
            var shell=new Geo();var panels=new Geo();var fittings=new Geo();var lights=new Geo();var exhibits=new Geo();
            var print=new Material(Shader.Find("Aquarium/Exhibit Backlit Print")){name="Marine atlas / backlit interpretation"};
            print.mainTexture=Resources.Load<Texture2D>("Aquarium/Textures/MarineExhibitAtlas");
            const int segments=496;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                Band(shell,a,b,2.12f,2.43f,.10f,Elevated(6.96f),C(0x686f72));
                Band(panels,a,b,2.105f,2.12f,.25f,1.07f,C(0x414f51));
                Band(panels,a,b,2.101f,2.122f,1.07f,1.105f,Brass);
                Band(panels,a,b,2.104f,2.125f,5.55f,Elevated(6.93f),C(0x26333d));
                Band(lights,a,b,1.99f,2.03f,5.50f,5.55f,C(0xffc782));
                Band(lights,a,b,2.075f,2.09f,.32f,.355f,C(0xffc68a));
            }
            for(int bay=0;bay<38;bay++)
            {
                float a=(bay+.35f)*Mathf.PI*2/38;
                var back=At(a,2.02f,0);var front=At(a,0,0);var inward=(front-back).normalized;
                var rotation=Quaternion.LookRotation(inward);var right=rotation*Vector3.right;
                var tangent=(At(a+.002f,2.02f,0)-At(a-.002f,2.02f,0)).normalized;
                var wallRotation=Quaternion.LookRotation(Vector3.Cross(tangent,Vector3.up));
                if(Vector3.Dot(wallRotation*Vector3.forward,inward)<0)wallRotation=wallRotation*Quaternion.Euler(0,180,0);
                float depth=Vector3.Distance(back,front);
                // Human-scale pilasters, joinery, doors, benches and exhibits give the glass a readable interior.
                fittings.Box(back+Vector3.up*6.08f,new Vector3(.18f,11.7f,.22f),C(0x887a61),wallRotation);
                float panelAngle=a+.055f;
                var panelTangent=(At(panelAngle+.002f,2.12f,0)-At(panelAngle-.002f,2.12f,0)).normalized;
                var panelNormal=Vector3.Cross(panelTangent,Vector3.up).normalized;
                var wallPoint=At(panelAngle,2.12f,0);
                if(Vector3.Dot(panelNormal,At(panelAngle,0,0)-wallPoint)<0)panelNormal=-panelNormal;
                var bayCentre=wallPoint+panelNormal*.42f;
                var turn=Quaternion.LookRotation(panelNormal);
                if(bay%6==0)
                {
                    fittings.Box(bayCentre+Vector3.up*1.62f,new Vector3(1.55f,2.75f,.16f),C(0x243b43),turn);
                    fittings.Box(bayCentre+turn*new Vector3(0,2.96f,.10f),new Vector3(1.64f,.09f,.12f),Brass,turn);
                    for(int side=-1;side<=1;side+=2)
                    {
                        fittings.Box(bayCentre+turn*new Vector3(side*.77f,1.62f,.1f),new Vector3(.07f,2.75f,.12f),Brass,turn);
                        fittings.Cylinder(bayCentre+turn*new Vector3(side*.12f,1.35f,.16f),bayCentre+turn*new Vector3(side*.12f,1.72f,.16f),.025f,.025f,Brass,8);
                    }
                    lights.Box(bayCentre+turn*new Vector3(0,3.16f,.13f),new Vector3(.8f,.19f,.025f),C(0xb4d7c3),turn);
                    Label(root,"GALLERY  /  "+(bay/6+1).ToString("00"),bayCentre+turn*new Vector3(0,3.53f,.14f),turn*Quaternion.Euler(0,180,0),.024f,C(0xeadabb));
                }
                else
                {
                    var display=bayCentre+Vector3.up*2.25f;
                    fittings.Box(display,new Vector3(2.05f,1.52f,.16f),C(0x364e55),turn);
                    int plate=bay%4;
                    DisplayPanel(exhibits,display+turn*new Vector3(0,0,.11f),turn,plate);
                    string[] titles={"LIVING REEF","OPEN OCEAN","AFTER DARK","LIFE IN LAYERS"};
                    string[] captions={"CORAL COMMUNITIES  /  01","PELAGIC WANDERERS  /  02","BIOLUMINESCENT LIFE  /  03","MARINE INVERTEBRATES  /  04"};
                    fittings.Box(display+turn*new Vector3(0,.93f,0),new Vector3(2.05f,.30f,.15f),C(0x142630),turn);
                    Label(root,titles[plate],display+turn*new Vector3(0,.94f,.09f),turn*Quaternion.Euler(0,180,0),.027f,C(0xcbd1bb));
                    Label(root,captions[plate],display+turn*new Vector3(0,-.88f,.11f),turn*Quaternion.Euler(0,180,0),.013f,C(0x7f9fa5));
                    for(int edge=-1;edge<=1;edge+=2)
                    {
                        fittings.Box(display+turn*new Vector3(edge*1.015f,0,.12f),new Vector3(.025f,1.5f,.035f),Brass,turn);
                        fittings.Box(display+turn*new Vector3(0,edge*.75f,.12f),new Vector3(2.05f,.025f,.035f),Brass,turn);
                        for(int h=-1;h<=1;h+=2)fittings.Ellipsoid(display+turn*new Vector3(edge*.97f,h*.70f,.14f),Vector3.one*.013f,Brass,8,5,0);
                    }
                    lights.Box(display+turn*new Vector3(0,.79f,.16f),new Vector3(1.85f,.018f,.04f),C(0xccb17b),turn);
                    if(bay%5==2)
                    {
                        var kiosk=bayCentre+turn*new Vector3(1.45f,.92f,1.1f);
                        fittings.Box(kiosk,new Vector3(.42f,1.38f,.40f),C(0x253944),turn);
                        fittings.Box(kiosk+Vector3.up*.73f,new Vector3(.72f,.08f,.57f),C(0x121e26),turn*Quaternion.Euler(-18,0,0));
                        Label(root,"REEF ATLAS",kiosk+Vector3.up*.84f+turn*Vector3.forward*.16f,turn*Quaternion.Euler(58,180,0),.012f,C(0xaac6bc));
                    }
                    if(depth>2.6f)
                    {
                        var seat=bayCentre+turn*new Vector3(0,.69f,.63f);
                        for(int s=0;s<4;s++)fittings.Box(seat+turn*new Vector3(0,0,(s-1.5f)*.12f),new Vector3(1.95f,.085f,.105f),C(0xab7849),turn);
                        foreach(float x in new[]{-.74f,.74f})fittings.Box(seat+turn*new Vector3(x,-.22f,0),new Vector3(.075f,.44f,.47f),Navy,turn);
                    }
                }
                // Pendant tracks and acoustic slats show a real ceiling through the glass.
                var lamp=Vector3.Lerp(front,back,.48f)+Vector3.up*4.8f;
                fittings.Box(lamp,new Vector3(2.25f,.14f,.18f),C(0x243c46),wallRotation);
                lights.Box(lamp-Vector3.up*.078f,new Vector3(2.10f,.018f,.12f),C(0xffdb9e),wallRotation);
                foreach(float side in new[]{-.8f,.8f})
                    fittings.Cylinder(lamp+right*side,lamp+right*side+Vector3.up*7.06f,.011f,.011f,Navy,6);
                for(int s=0;s<5;s++)
                {
                    var slat=At(a+(s-2)*.014f,1.05f,Elevated(6.73f));
                    fittings.Box(slat,new Vector3(.09f,.21f,Mathf.Max(.5f,depth*.72f)),C(0x8a6c4e),rotation);
                }
                marker.visitorGalleryBays++;
            }
            // The central glass galleries are lit from within as well as around their perimeter.
            var field=new DeckField();var loops=field.Contours(field.Water);float travel=0;
            foreach(var loop in loops)for(int i=0;i<loop.Count;i++)
            {
                var a=loop[i];var b=loop[(i+1)%loop.Count];travel+=Vector3.Distance(a,b);
                if(!IsWalkBoundary(a))continue;
                var tangent=(b-a).normalized;var across=Vector3.Cross(tangent,Vector3.up);
                var test=a+across*.2f;
                if(AquariumWalkwayLayout.SignedDistance(new Point(test.x,test.z))>0)across=-across;
                var aa=a+across*.24f;var bb=b+across*.24f;
                lights.Cylinder(Height(aa,Elevated(6.65f)),Height(bb,Elevated(6.65f)),.024f,.024f,C(0xffd39a),6);
                if(travel<4.2f)continue;travel=0;
                var rotation=Quaternion.LookRotation(across);var foot=a+across*.35f;
                fittings.Box(Height(foot,6.0f),new Vector3(.12f,11.45f,.12f),C(0x42545c),rotation);
                lights.Box(Height(foot,3.1f)+across*.07f,new Vector3(.04f,.75f,.035f),C(0xffd29b),rotation);
                var ceiling=foot+across*.65f;
                fittings.Box(Height(ceiling,Elevated(6.71f)),new Vector3(.16f,.12f,1.25f),C(0x34505a),rotation);
                lights.Box(Height(ceiling,Elevated(6.64f)),new Vector3(.075f,.018f,1.15f),C(0xffdcab),rotation);
            }
            shell.Emit("26 • Enclosed visitor gallery / continuous curved rear walls",root,wall);
            panels.Emit("27 • Visitor gallery stone wainscot and dark upper frieze",root,wall);
            fittings.Emit("28 • Interior doors, benches, ceiling baffles and bronze framing",root,fixture);
            lights.Emit("29 • Interior warm pendants and continuous cove lighting",root,glow,false);
            exhibits.Emit("30 • Aquarium interpretation panels / marine motifs",root,print,false);
        }

        private static void BuildMonumentalReefs(Transform root,Material rock,Material coralMaterial,Material kelpMaterial,System.Random random,AquariumEnvironmentRoot marker)
        {
            for(int group=0;group<AquariumMonolithLayout.Groups.Length;group++)
            {
                var sculpt=new MonolithMesher(group,group<3?.14f:.17f);
                var go=sculpt.Build().Emit("31 • Monolithic limestone / "+AquariumMonolithLayout.Names[group],root,rock);
                go.GetComponent<MeshFilter>().sharedMesh.SetNormals(sculpt.Normals);
                SurfaceBasis(go.GetComponent<MeshFilter>().sharedMesh);
                marker.monumentalReefs++;
                var coral=new Geo();var kelp=new Geo();
                var min=AquariumMonolithLayout.Minimum[group];var max=AquariumMonolithLayout.Maximum[group];
                for(int n=0;n<110;n++)
                {
                    float x=Range(random,min.x,max.x),z=Range(random,min.z,max.z),y=max.y;
                    for(;y>.25f;y-=.09f)if(AquariumMonolithLayout.Distance(new Point(x,y,z),group)<0)break;
                    if(y<AquariumSeabed.Height(x,z)+.08f||y>9.3f)continue;
                    var p=new Point(x,y+.04f,z);var normal=AquariumMonolithLayout.Normal(p,group);
                    if(normal.y<.58f)continue;
                    var at=new Vector3(x,p.y,z);var colour=Corals[(n+group*2)%Corals.Length];
                    if(n%5==0 && y<4.6f){Kelp(kelp,at,Range(random,.7f,1.6f),n,random,marker);marker.kelpFronds++;}
                    else {CoralColony(coral,at,Range(random,.22f,.49f),n+group*13,random,marker);marker.coralColonies++;}
                }
                if(coral.Count>0)coral.Emit("32 • Cliff ledges / encrusting coral "+group,root,coralMaterial);
                if(kelp.Count>0)kelp.Emit("33 • Cliff ledges / anchored kelp "+group,root,kelpMaterial);
            }
        }

        // Marching tetrahedra extracts the connected fractured solid. Shared edge vertices eliminate cracks.
        private sealed class MonolithMesher
        {
            private readonly int group,nx,ny,nz;private readonly float step;private readonly Point origin;
            private readonly float[] field;private readonly Dictionary<long,int> edges=new Dictionary<long,int>();
            private readonly List<Vector3> positions=new List<Vector3>();public readonly List<Vector3> Normals=new List<Vector3>();
            private readonly Geo mesh=new Geo();
            private Vector3 tetraOutward;
            private static readonly int[,] Tets={{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
            public MonolithMesher(int g,float resolution)
            {
                group=g;step=resolution;origin=AquariumMonolithLayout.Minimum[g];var max=AquariumMonolithLayout.Maximum[g];
                nx=Mathf.CeilToInt((max.x-origin.x)/step)+1;ny=Mathf.CeilToInt((max.y-origin.y)/step)+1;nz=Mathf.CeilToInt((max.z-origin.z)/step)+1;
                field=new float[nx*ny*nz];
                for(int i=0;i<field.Length;i++)field[i]=AquariumMonolithLayout.Distance(Position(i),group);
            }
            private Point Position(int i){int x=i%nx,y=(i/nx)%ny,z=i/(nx*ny);return origin+new Point(x*step,y*step,z*step);}
            public Geo Build()
            {
                var ids=new int[8];var inside=new int[4];var outside=new int[4];
                for(int z=0;z<nz-1;z++)for(int y=0;y<ny-1;y++)for(int x=0;x<nx-1;x++)
                {
                    int a=(z*ny+y)*nx+x;
                    ids[0]=a;ids[1]=a+1;ids[2]=a+1+nx*ny;ids[3]=a+nx*ny;
                    ids[4]=a+nx;ids[5]=a+nx+1;ids[6]=a+nx+1+nx*ny;ids[7]=a+nx+nx*ny;
                    bool positive=false,negative=false;for(int i=0;i<8;i++){positive|=field[ids[i]]>=0;negative|=field[ids[i]]<0;}if(!positive||!negative)continue;
                    for(int t=0;t<6;t++)
                    {
                        int ni=0,no=0;for(int c=0;c<4;c++){int id=ids[Tets[t,c]];if(field[id]<0)inside[ni++]=id;else outside[no++]=id;}
                        if(ni==0||no==0)continue;
                        // Face winding must follow this tetrahedron's linear field.
                        // Averaged analytic normals can point across a sharp union/cut
                        // and reverse only one triangle, opening a back-face culled hole.
                        Point inner=new Point(),outer=new Point();
                        for(int c=0;c<ni;c++)inner+=Position(inside[c]);
                        for(int c=0;c<no;c++)outer+=Position(outside[c]);
                        var direction=outer/no-inner/ni;
                        tetraOutward=new Vector3(direction.x,direction.y,direction.z);
                        if(ni==1)Triangle(Vertex(inside[0],outside[0]),Vertex(inside[0],outside[1]),Vertex(inside[0],outside[2]));
                        else if(ni==3)Triangle(Vertex(outside[0],inside[0]),Vertex(outside[0],inside[1]),Vertex(outside[0],inside[2]));
                        else if(ni==2)
                        {
                            int v0=Vertex(inside[0],outside[0]),v1=Vertex(inside[0],outside[1]),v2=Vertex(inside[1],outside[1]),v3=Vertex(inside[1],outside[0]);
                            Triangle(v0,v1,v2);Triangle(v0,v2,v3);
                        }
                    }
                }
                return mesh;
            }
            private int Vertex(int a,int b)
            {
                if(a>b){int swap=a;a=b;b=swap;}long key=((long)a<<32)|(uint)b;int index;if(edges.TryGetValue(key,out index))return index;
                // Keep crossings off the exact lattice endpoint to prevent collapsed
                // triangles from floating point rounding (maximum offset < 0.06 mm).
                float t=Mathf.Clamp(field[a]/(field[a]-field[b]),.0002f,.9998f);var p=Position(a)+(Position(b)-Position(a))*t;var n=AquariumMonolithLayout.Normal(p,group);
                var v=new Vector3(p.x,p.y,p.z);index=positions.Count;positions.Add(v);Normals.Add(new Vector3(n.x,n.y,n.z));
                mesh.V(v,Color.white);edges[key]=index;return index;
            }
            private void Triangle(int a,int b,int c)
            {
                if(Vector3.Dot(Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]),tetraOutward)<0){int swap=b;b=c;c=swap;}
                mesh.T(a,b,c);
            }
        }
    }
}

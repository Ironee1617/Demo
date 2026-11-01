using System;
using System.Collections.Generic;
using UnityEngine;

namespace PrototypeLab.Aquarium
{
    public static partial class AquariumEnvironment
    {
        // Every flexible tube has shared rings and closed ends. UV2 carries the same
        // bend weight and phase at a joint, including its cap, so current cannot tear it.
        private static void OrganicTube(Geo g,Vector3[] path,float[] radius,Color bottom,Color top,Vector2[] bend,int sides=8,Color[] ringColours=null)
        {
            int first=g.Count;Vector3 u=Vector3.zero;
            for(int row=0;row<path.Length;row++)
            {
                var tangent=(path[Mathf.Min(row+1,path.Length-1)]-path[Mathf.Max(0,row-1)]).normalized;
                u=row==0?Vector3.Cross(tangent,Mathf.Abs(tangent.y)>.9f?Vector3.right:Vector3.up).normalized:
                    (u-tangent*Vector3.Dot(u,tangent)).normalized;
                var v=Vector3.Cross(tangent,u);float t=row/(float)(path.Length-1);
                for(int s=0;s<sides;s++)
                {
                    float a=s*Mathf.PI*2/sides;
                    g.V(path[row]+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*radius[row],ringColours==null?Color.Lerp(bottom,top,t*t):ringColours[row],new Vector2(s/(float)sides,t),bend[row]);
                    if(row>0)g.Q(first+(row-1)*sides+s,first+(row-1)*sides+(s+1)%sides,first+row*sides+(s+1)%sides,first+row*sides+s);
                }
            }
            int lo=g.Count;g.V(path[0],bottom,Vector2.zero,bend[0]);
            int hi=g.Count;g.V(path[path.Length-1],top,Vector2.one,bend[bend.Length-1]);
            int last=first+(path.Length-1)*sides;
            for(int s=0;s<sides;s++){g.T(lo,first+(s+1)%sides,first+s);g.T(hi,last+s,last+(s+1)%sides);}
        }

        // A softly cupped, thin solid blade, with rounded margins and closed ends.
        private static void OrganicBlade(Geo g,Vector3[] path,float width,Color tint,float phase,float rootWeight,float tipWeight,float twist)
        {
            // Smooth the lengthwise silhouette before applying the gentle edge
            // waves. Sparse samples made the old folded section look like boards.
            var controls=path;bool broad=width>.07f;
            int segments=broad?Mathf.Max(20,Mathf.CeilToInt((controls.Length-1)*1.25f)):Mathf.Max(12,controls.Length-1);
            path=new Vector3[segments+1];
            for(int n=0;n<=segments;n++)
            {
                float sample=n/(float)segments*(controls.Length-1);
                int index=Mathf.Min(controls.Length-2,Mathf.FloorToInt(sample));float f=sample-index;
                var a=controls[Mathf.Max(0,index-1)];var b=controls[index];
                var c=controls[index+1];var d=controls[Mathf.Min(controls.Length-1,index+2)];
                path[n]=.5f*((2*b)+(-a+c)*f+(2*a-5*b+4*c-d)*f*f+(-a+3*b-3*c+d)*f*f*f);
            }
            int first=g.Count,ring=broad?12:8;
            Vector3 previousSide=Vector3.zero;
            for(int row=0;row<path.Length;row++)
            {
                float t=row/(float)(path.Length-1);
                var tangent=(path[Mathf.Min(row+1,path.Length-1)]-path[Mathf.Max(0,row-1)]).normalized;
                var baseSide=row==0?Vector3.Cross(tangent,Mathf.Abs(tangent.z)>.9f?Vector3.up:Vector3.forward).normalized:
                    (previousSide-tangent*Vector3.Dot(previousSide,tangent)).normalized;
                previousSide=baseSide;
                var side=Quaternion.AngleAxis(twist*t+Mathf.Sin(t*6+phase)*8,tangent)*baseSide;
                var normal=Vector3.Cross(tangent,side);
                float w=Mathf.Max(.0015f,width*Mathf.Pow(Mathf.Max(0,Mathf.Sin(Mathf.PI*t)),.78f)*(1+Mathf.Sin(t*8+phase)*.045f));
                var payload=new Vector2(Mathf.Lerp(rootWeight,tipWeight,t*t),phase);
                for(int j=0;j<ring;j++)
                {
                    float angle=j/(float)ring*Mathf.PI*2,crossX=Mathf.Cos(angle);
                    float camber=(1-crossX*crossX)*w*(.16f+.045f*Mathf.Sin(t*5+phase));
                    float thickness=Mathf.Max(.0005f,w*.035f);
                    var p=path[row]+side*w*crossX+normal*(camber+Mathf.Sin(angle)*thickness);
                    g.V(p,Color.Lerp(tint*.73f,Color.Lerp(tint,C(0xd6d78b),.26f),t),new Vector2((crossX+1)*.5f,t),payload);
                    if(row>0)g.Q(first+(row-1)*ring+j,first+(row-1)*ring+(j+1)%ring,first+row*ring+(j+1)%ring,first+row*ring+j);
                }
            }
            int lo=g.Count;g.V(path[0],tint,Vector2.zero,new Vector2(rootWeight,phase));
            int hi=g.Count;g.V(path[path.Length-1],tint,Vector2.one,new Vector2(tipWeight,phase));
            int last=first+(path.Length-1)*ring;
            for(int j=0;j<ring;j++){g.T(lo,first+(j+1)%ring,first+j);g.T(hi,last+j,last+(j+1)%ring);}
        }

        private static Vector3[] Curve(Vector3 a,Vector3 control,Vector3 b,int segments=10)
        {
            var path=new Vector3[segments+1];
            for(int i=0;i<=segments;i++){float t=i/(float)segments;path[i]=a*((1-t)*(1-t))+control*(2*t*(1-t))+b*(t*t);}
            return path;
        }
        private static void Tendril(Geo g,Vector3[] path,float r0,float r1,Color tint,Color tip,float phase,float w0,float w1,int sides=7)
        {
            var radius=new float[path.Length];var bend=new Vector2[path.Length];
            for(int i=0;i<path.Length;i++){float t=i/(float)(path.Length-1);radius[i]=Mathf.Lerp(r0,r1,t);bend[i]=new Vector2(Mathf.Lerp(w0,w1,t*t),phase);}
            OrganicTube(g,path,radius,tint,tip,bend,sides);
        }

        private static void Seaweed(Geo g,Vector3 at,float height,float phase,System.Random r,AquariumEnvironmentRoot marker)
        {
            int kind=Mathf.Abs(Mathf.RoundToInt(at.x*.71f+at.z*.53f+phase*3.1f))%5;
            marker.plantForms[kind]++;
            if(kind==0) // Broad kelp with continuous stipe and paired, curled blades.
            {
                Color tint=Color.Lerp(C(0x42754e),C(0x8d943e),Range(r,0,1));
                var stem=Curve(at,at+new Vector3(.12f,height*.55f,0),at+new Vector3(Mathf.Sin(phase)*.34f,height,.21f),14);
                Tendril(g,stem,.025f,.008f,tint*.72f,tint,phase,0,1);
                for(int n=2;n<14;n++)
                {
                    float f=n/14f,w=f*f;
                    for(int side=-1;side<=1;side+=2)
                    {
                        float a=phase+side*1.6f+n*.27f,length=(.17f+height*.22f)*Mathf.Sin(f*2.4f);
                        var tip=stem[n]+new Vector3(Mathf.Cos(a)*length,length*.4f,Mathf.Sin(a)*length);
                        OrganicBlade(g,Curve(stem[n],Vector3.Lerp(stem[n],tip,.48f)+Vector3.up*.12f,tip,9),length*.22f,tint,phase,w,w+.12f,side*45);
                    }
                }
            }
            else if(kind==1) // Strap kelp: fewer, broad wavy ribbons in an olive rosette.
            {
                Color tint=Color.Lerp(C(0x747f3a),C(0x9ba14c),Range(r,0,1));
                for(int n=0;n<5;n++)
                {
                    float a=phase+n*2.399f,h=height*Range(r,.7f,1.02f);
                    var tip=at+new Vector3(Mathf.Cos(a)*h*.34f,h,Mathf.Sin(a)*h*.34f);
                    OrganicBlade(g,Curve(at,at+new Vector3(-Mathf.Sin(a)*.2f,h*.6f,Mathf.Cos(a)*.2f),tip,18),height*.095f,tint,phase+n*.23f,0,.95f,85);
                }
            }
            else if(kind==2) // Eelgrass: fine tapered blades, shorter dense fans.
            {
                Color tint=Color.Lerp(C(0x3e825f),C(0x83a66c),Range(r,0,1));
                for(int n=0;n<11;n++)
                {
                    float a=phase+n*2.399f,h=height*Range(r,.36f,.7f);
                    var root=at+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*.055f;
                    var tip=root+new Vector3(Mathf.Cos(a)*h*.5f,h,Mathf.Sin(a)*h*.5f);
                    OrganicBlade(g,Curve(root,root+Vector3.up*h*.85f,tip,12),Range(r,.018f,.038f),tint,phase+n*.18f,0,.8f,32);
                }
            }
            else if(kind==3) // Feathery red seaweed with fine pinnae.
            {
                Color tint=Color.Lerp(C(0x7e405a),C(0xab6869),Range(r,0,1));
                for(int shoot=0;shoot<4;shoot++)
                {
                    float a=phase+shoot*1.7f,h=height*Range(r,.4f,.67f);
                    var stem=Curve(at,at+Vector3.up*h*.6f,at+new Vector3(Mathf.Cos(a)*.22f,h,Mathf.Sin(a)*.22f),12);
                    Tendril(g,stem,.018f,.004f,tint*.7f,tint,phase,0,.8f,6);
                    var across=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                    for(int n=2;n<12;n++)for(int side=-1;side<=1;side+=2)
                    {
                        float f=n/12f,w=.8f*f*f,len=h*.25f*Mathf.Sin(f*Mathf.PI);
                        var tip=stem[n]+across*len*side+Vector3.up*len*.35f;
                        OrganicBlade(g,Curve(stem[n],Vector3.Lerp(stem[n],tip,.5f)+Vector3.up*.04f,tip,5),.025f,tint,phase,w,w+.05f,25);
                    }
                }
            }
            else // Sargassum: branching stems, small curved leaves and air vesicles.
            {
                Color tint=Color.Lerp(C(0x756f32),C(0xaa984c),Range(r,0,1));
                var stem=Curve(at,at+Vector3.up*height*.55f,at+new Vector3(.16f,height*.8f,-.18f),12);
                Tendril(g,stem,.023f,.006f,tint*.65f,tint,phase,0,.8f);
                for(int n=2;n<12;n++)
                {
                    float a=phase+n*2.399f,w=.8f*(n/12f)*(n/12f);
                    var tip=stem[n]+new Vector3(Mathf.Cos(a)*height*.19f,height*.13f,Mathf.Sin(a)*height*.19f);
                    var branch=Curve(stem[n],Vector3.Lerp(stem[n],tip,.5f),tip,4);
                    Tendril(g,branch,.012f,.004f,tint,tint,phase,w,w+.1f,6);
                    for(int j=1;j<4;j++)
                    {
                        var end=branch[j]+new Vector3(Mathf.Cos(a+.8f)*.18f,.15f,Mathf.Sin(a+.8f)*.18f);
                        float weight=Mathf.Lerp(w,w+.1f,(j/4f)*(j/4f));
                        OrganicBlade(g,Curve(branch[j],Vector3.Lerp(branch[j],end,.5f)+Vector3.up*.06f,end,6),.043f,tint,phase,weight,weight+.035f,42);
                    }
                    g.Ellipsoid(tip,new Vector3(.033f,.047f,.033f),Color.Lerp(tint,C(0xc1ad68),.35f),8,6,0,new Vector2(w+.1f,phase));
                }
            }
        }

        private static void CoralColony(Geo g,Vector3 at,float scale,int seed,System.Random r,AquariumEnvironmentRoot marker)
        {
            int kind=Math.Abs(seed)%6;marker.coralForms[kind]++;
            Color[] palette={C(0xddab6f),C(0xae818f),C(0xb57e5e),C(0x7eaa94),C(0xc58aab),C(0x7b92b3)};
            Color tint=palette[(seed/3+kind)%palette.Length];
            switch(kind)
            {
                case 0: Staghorn(g,at,Vector3.up*scale*.95f,scale*.075f,3,tint,r);break;
                case 1: ScrollCoral(g,at,scale*1.18f,tint,seed);break;
                case 2: SeaFan(g,at,scale,tint,seed);break;
                case 3: BrainCoral(g,at,scale,tint,seed);break;
                case 4: FingerCoral(g,at,scale,tint,seed);break;
                default: TubeSponge(g,at,scale,tint,seed);break;
            }
        }
        private static void Staghorn(Geo g,Vector3 at,Vector3 branch,float radius,int depth,Color tint,System.Random r)
        {
            var tip=at+branch;float phase=Range(r,0,6.28f);
            var control=Vector3.Lerp(at,tip,.5f)+new Vector3(Mathf.Sin(phase),0,Mathf.Cos(phase))*branch.magnitude*.12f;
            Tendril(g,Curve(at,control,tip,4),radius,radius*.48f,tint,Color.Lerp(tint,C(0xeee3cc),.46f),0,0,0,7);
            if(depth==0)g.Ellipsoid(tip,Vector3.one*radius*.5f,Color.Lerp(tint,C(0xeee3cc),.48f),7,5,0);
            if(depth==0)return;
            int forks=depth==3?4:3;
            for(int n=0;n<forks;n++)
            {
                float a=phase+n*2.399f;
                var direction=(branch.normalized*.45f+new Vector3(Mathf.Cos(a)*.66f,.55f,Mathf.Sin(a)*.66f)).normalized;
                Staghorn(g,Vector3.Lerp(at,tip,.65f+n*.08f),direction*branch.magnitude*.66f,radius*.61f,depth-1,Color.Lerp(tint,C(0xe9d7bd),.06f),r);
            }
        }
        private static void ScrollCoral(Geo g,Vector3 at,float radius,Color tint,int seed)
        {
            const int sides=72,rows=9;
            for(int layer=0;layer<3;layer++)
            {
                int first=g.Count;float scale=1-layer*.24f;
                var centre=at+new Vector3(layer*.04f,layer*radius*.34f,layer*-.03f);
                for(int face=0;face<2;face++)for(int ring=0;ring<=rows;ring++)for(int s=0;s<sides;s++)
                {
                    float t=ring/(float)rows,a=s*Mathf.PI*2/sides;
                    float r=Mathf.Max(.007f,t*radius*scale)*(1+Mathf.Sin(a*5+seed)*.04f+Mathf.Cos(a*7-seed)*.012f);
                    float y=radius*(.30f*t*t+.075f*Mathf.Sin(a*3+seed)*t*t+.025f*Mathf.Sin(a*5+t*3)*t*t);
                    var p=centre+new Vector3(Mathf.Cos(a)*r,y-(face==1?.022f:0),Mathf.Sin(a)*r);
                    g.V(p,Color.Lerp(tint*.68f,Color.Lerp(tint,C(0xe9d9bd),.42f),t*t),new Vector2(t,a),Vector2.zero);
                    if(ring<rows)
                    {
                        int v=first+face*(rows+1)*sides+ring*sides+s,next=first+face*(rows+1)*sides+ring*sides+(s+1)%sides;
                        if(face==0)g.Q(v,v+sides,next+sides,next);else g.Q(v,next,next+sides,v+sides);
                    }
                }
                int bottom=first+(rows+1)*sides,outer=rows*sides;
                for(int s=0;s<sides;s++)
                {
                    int next=(s+1)%sides;
                    g.Q(first+outer+s,bottom+outer+s,bottom+outer+next,first+outer+next);
                    g.Q(first+s,first+next,bottom+next,bottom+s);
                }
                // The tiny central ring closes around a short solid stalk.
                g.Cylinder(centre-Vector3.up*.035f,centre+Vector3.up*.022f,.028f,.025f,tint*.72f,10);
            }
        }
        private static void SeaFan(Geo g,Vector3 at,float scale,Color tint,int seed)
        {
            var rotation=Quaternion.Euler(0,seed*37,0);int branches=13,rows=9;
            var lines=new Vector3[branches][];
            for(int b=0;b<branches;b++)
            {
                float a=Mathf.Lerp(-1.1f,1.1f,b/(float)(branches-1));lines[b]=new Vector3[rows];
                for(int n=0;n<rows;n++)
                {
                    float t=n/(float)(rows-1);var p=new Vector3(Mathf.Sin(a)*scale*1.15f*t,scale*(.14f+1.6f*Mathf.Cos(a*.55f)*t),Mathf.Sin(t*7+a)*scale*.055f*t);
                    lines[b][n]=at+rotation*p;
                }
                Tendril(g,lines[b],.018f,.006f,tint*.72f,Color.Lerp(tint,C(0xe5d3ba),.25f),seed,.0f,.2f,6);
            }
            for(int n=2;n<rows;n++)for(int b=0;b<branches-1;b++)
            {
                if((b+n+seed)%7==0)continue; // Deliberate irregular fan lattice, not a flat filled sheet.
                var a=lines[b][n];var end=lines[b+1][Mathf.Min(rows-1,n+(b%2==0?1:0))];
                Tendril(g,Curve(a,Vector3.Lerp(a,end,.5f)-Vector3.up*.025f,end,3),.007f,.006f,tint,tint,seed,.2f*(n/8f)*(n/8f),.2f,5);
            }
            Tendril(g,new[]{at,at+Vector3.up*scale*.2f},.045f,.025f,tint*.6f,tint,seed,0,0,8);
        }
        private static void BrainCoral(Geo g,Vector3 at,float scale,Color tint,int seed)
        {
            var centre=at+Vector3.up*scale*.23f;
            g.Ellipsoid(centre,new Vector3(scale,scale*.66f,scale*.82f),tint*.68f,28,18,.035f);
            for(int n=0;n<12;n++)
            {
                float theta=.22f+n*Mathf.PI/14;var path=new Vector3[45];
                for(int s=0;s<path.Length;s++)
                {
                    float a=s*Mathf.PI*2/(path.Length-1);
                    float t=theta+Mathf.Sin(a*7+n*.8f+seed)*.055f;
                    path[s]=centre+new Vector3(Mathf.Cos(a)*Mathf.Sin(t)*scale,Mathf.Cos(t)*scale*.66f,Mathf.Sin(a)*Mathf.Sin(t)*scale*.82f)*1.025f;
                }
                Tendril(g,path,scale*.019f,scale*.019f,Color.Lerp(tint,C(0xd6d0a2),.3f),tint,0,0,0,5);
            }
        }
        private static void FingerCoral(Geo g,Vector3 at,float scale,Color tint,int seed)
        {
            g.Ellipsoid(at+Vector3.up*.045f,new Vector3(scale*.68f,.12f,scale*.65f),tint*.72f,16,9,.05f);
            for(int n=0;n<16;n++)
            {
                float a=n*2.399f+seed,rr=scale*.66f*Mathf.Sqrt((n+1)/17f);
                var root=at+new Vector3(Mathf.Cos(a)*rr,.04f,Mathf.Sin(a)*rr);
                float h=scale*(.5f+.6f*(1-rr/scale)+Mathf.Sin(n*4.1f)*.12f);
                var tip=root+new Vector3(Mathf.Cos(a)*.13f,h,Mathf.Sin(a)*.13f);
                Tendril(g,Curve(root,root+Vector3.up*h*.7f,tip,6),scale*.10f,scale*.075f,tint,Color.Lerp(tint,C(0xf0dfd2),.4f),0,0,0,8);
                g.Ellipsoid(tip,new Vector3(scale*.076f,scale*.09f,scale*.076f),Color.Lerp(tint,C(0xf0dfd2),.4f),8,6,.02f);
            }
        }
        private static void TubeSponge(Geo g,Vector3 at,float scale,Color tint,int seed)
        {
            for(int n=0;n<7;n++)
            {
                float a=n*2.399f+seed,spread=n==0?0:scale*.5f;float h=scale*(.8f+.55f*Mathf.Abs(Mathf.Sin(a)));
                var root=at+new Vector3(Mathf.Cos(a)*spread,0,Mathf.Sin(a)*spread);
                const int sides=16,rings=8;int first=g.Count;float r=scale*(.13f+(n%3)*.025f);
                for(int wall=0;wall<2;wall++)for(int row=0;row<=rings;row++)for(int s=0;s<sides;s++)
                {
                    float t=row/(float)rings,angle=s*Mathf.PI*2/sides;
                    var centre=root+new Vector3(Mathf.Cos(a)*t*t*.10f,h*t,Mathf.Sin(a)*t*t*.10f);
                    float radius=r*(.75f+t*.5f)*(wall==0?1:.65f);
                    g.V(centre+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius),tint*(wall==0?(.75f+t*.25f):(.24f+t*.32f)),new Vector2(s/(float)sides,t));
                    if(row<rings)
                    {
                        int v=first+wall*(rings+1)*sides+row*sides+s,next=first+wall*(rings+1)*sides+row*sides+(s+1)%sides;
                        if(wall==0)g.Q(v,next,next+sides,v+sides);else g.Q(v,v+sides,next+sides,next);
                    }
                }
                int inner=first+(rings+1)*sides,outerTop=first+rings*sides,innerTop=inner+rings*sides;
                int baseCentre=g.Count;g.V(root,tint*.35f);
                for(int s=0;s<sides;s++)
                {
                    int next=(s+1)%sides;g.Q(outerTop+s,outerTop+next,innerTop+next,innerTop+s);
                    g.Q(first+s,inner+s,inner+next,first+next);g.T(baseCentre,inner+s,inner+next);
                }
            }
        }

        // Two restrained reference looks: rose column with flowing pink tentacles,
        // and an olive column with short rounded mint/ivory tentacles.
        private static Vector3 AnemoneColumn(Geo g,Vector3 at,float radius,int kind,int seed)
        {
            Color foot=kind==0?C(0x9c464f):C(0x756f50);
            Color middle=kind==0?C(0xd16978):C(0xb1a578);
            Color rim=kind==0?C(0xe18ba9):C(0xc0d0aa);
            const int sides=48,bodyRows=18,domeRows=10;
            int first=g.Count;float phase=seed*1.7f;
            var lean=new Vector3(Mathf.Sin(phase),0,Mathf.Cos(phase))*radius*.055f;
            // Shared rings run continuously from the buried foot into an elliptical
            // crown. There is no horizontal cap, rim flange, or separate base disc.
            for(int row=0;row<=bodyRows+domeRows;row++)
            {
                float t=Mathf.Min(1,row/(float)bodyRows);
                float domeAngle=Mathf.Max(0,row-bodyRows)/(float)(domeRows+1)*Mathf.PI*.5f;
                float radial=row<=bodyRows?
                    .39f+.21f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,1,t))+.045f*Mathf.Exp(-10*t):
                    .60f*Mathf.Cos(domeAngle);
                float height=row<=bodyRows?Mathf.Lerp(-.14f,.65f,t):.65f+.22f*Mathf.Sin(domeAngle);
                Color tint=t<.62f?Color.Lerp(foot,middle,Mathf.SmoothStep(0,1,t/.62f)):
                    Color.Lerp(middle,rim,Mathf.SmoothStep(0,1,(t-.62f)/.38f));
                for(int s=0;s<sides;s++)
                {
                    float a=s*Mathf.PI*2/sides;
                    float asymmetry=1+.035f*Mathf.Sin(a*3+phase)+.012f*Mathf.Cos(a*7-phase);
                    float fold=1+.008f*Mathf.Sin(a*16+phase)*Mathf.Sin(t*Mathf.PI);
                    float edgeLift=Mathf.Sin(a*3+phase)*.012f*t*Mathf.Cos(domeAngle);
                    var p=at+lean*t+new Vector3(Mathf.Cos(a)*radius*radial*asymmetry*fold,
                        radius*(height+edgeLift),Mathf.Sin(a)*radius*radial*asymmetry*fold);
                    var c=tint*(.985f+.018f*Mathf.Cos(a*5+phase));c.a=1;
                    g.V(p,c,new Vector2(s/(float)sides,(height+.14f)/1.01f),Vector2.zero);
                    if(row>0)g.Q(first+(row-1)*sides+s,first+row*sides+s,first+row*sides+(s+1)%sides,first+(row-1)*sides+(s+1)%sides);
                }
            }
            int bottom=g.Count;g.V(at-Vector3.up*.14f*radius,foot);
            var crown=at+lean+Vector3.up*radius*.65f;
            int top=g.Count;g.V(crown+Vector3.up*radius*.22f,rim,new Vector2(.5f,1));
            int last=first+(bodyRows+domeRows)*sides;
            for(int s=0;s<sides;s++){g.T(bottom,first+s,first+(s+1)%sides);g.T(top,last+(s+1)%sides,last+s);}
            return crown;
        }

        private static void LivingAnemone(Geo g,Vector3 at,float radius,int kind,int seed,AquariumEnvironmentRoot marker)
        {
            var crown=AnemoneColumn(g,at,radius,kind,seed);
            Color rootColour=kind==0?C(0xe18ba9):C(0xc0d0aa);
            Color midColour=kind==0?C(0xf596c2):C(0xd1e4b9);
            Color tipColour=kind==0?C(0xffd0e3):C(0xf4ebcf);
            float phase=seed*1.618f;int count=kind==0?64:90;
            for(int n=0;n<count;n++)
            {
                float a=n*2.399963f+phase;
                var radial=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var tangent=new Vector3(-radial.z,0,radial.x);
                float fraction=Mathf.Sqrt((n+.5f)/count),rr=radius*(.06f+.50f*fraction);
                float domeHeight=.22f*Mathf.Sqrt(1-Mathf.Pow(rr/(radius*.60f),2));
                var start=crown+radial*rr+Vector3.up*(radius*(domeHeight-.035f));
                float length=radius*(kind==0?.72f:.31f)*(1+.14f*Mathf.Sin(n*3.7f));
                int segments=kind==0?16:10,roundedTip=kind==0?0:5;
                var path=new Vector3[segments+roundedTip+1];var widths=new float[path.Length];
                var weights=new Vector2[path.Length];var colours=new Color[path.Length];
                float drift=kind==0?.66f:.30f,tipRise=kind==0?Mathf.Lerp(.77f,.32f,fraction):.90f;
                var p1=start+Vector3.up*length*.70f+radial*length*.06f;
                var p2=start+radial*length*.40f+Vector3.up*length*.85f+tangent*Mathf.Sin(n)*length*.09f;
                var end=start+radial*length*drift+Vector3.up*length*tipRise+tangent*Mathf.Sin(n)*length*.09f;
                float variation=.92f+.12f*(.5f+.5f*Mathf.Sin(n*1.73f+seed));
                for(int k=0;k<=segments;k++)
                {
                    float t=k/(float)segments,u=1-t;
                    path[k]=start*(u*u*u)+p1*(3*u*u*t)+p2*(3*u*t*t)+end*(t*t*t);
                    widths[k]=kind==0?radius*Mathf.Lerp(.043f,.0045f,Mathf.Pow(t,1.7f)):
                        radius*Mathf.Lerp(.040f,.052f,Mathf.SmoothStep(0,1,t));
                    weights[k]=new Vector2(t*t*(kind==0?.42f:.15f),phase+n*.32f);
                    Color c=t<.58f?Color.Lerp(rootColour,midColour,Mathf.SmoothStep(0,1,t/.58f)):
                        Color.Lerp(midColour,tipColour,Mathf.SmoothStep(0,1,(t-.58f)/.42f));
                    colours[k]=c*variation;colours[k].a=1;
                }
                if(kind==1)
                {
                    // The rounded tip continues the tube itself, without a
                    // separate ball or a visible junction at the end.
                    var tangentEnd=(end-p2).normalized;float endRadius=widths[segments];
                    for(int k=1;k<=roundedTip;k++)
                    {
                        float angle=k/(float)roundedTip*Mathf.PI*.5f;int row=segments+k;
                        path[row]=end+tangentEnd*endRadius*Mathf.Sin(angle);
                        widths[row]=Mathf.Max(radius*.001f,endRadius*Mathf.Cos(angle));
                        weights[row]=weights[segments];colours[row]=colours[segments];
                    }
                }
                OrganicTube(g,path,widths,colours[0],colours[colours.Length-1],weights,12,colours);
            }
            marker.anemoneColonies++;marker.anemoneForms[kind]++;
        }

        private static bool PlantingClear(Vector3 p,float radius)
        {
            var q=new Point(p.x,p.y,p.z);
            return AquariumMapOutline.Contains(q,radius+.25f)&&!AquariumWalkwayLayout.Contains(q,radius+.25f)&&
                !AquariumEntryLayout.KeepWaterClear(q,radius)&&AquariumMonolithLayout.Distance(q)>.08f;
        }
        private static void BuildAnemoneGardens(Transform root,Material rockMaterial,Material kelpMaterial,Material softMaterial,AquariumEnvironmentRoot marker)
        {
            var r=new System.Random(8129);var soft=new Geo();var plants=new Geo();var rubble=new Geo();
            // A legible foreground garden around existing cover, plus smaller ledge colonies.
            var reef=AquariumModel.Rocks[1];var centre=new Vector3(reef.x,reef.y,reef.z);
            for(int n=0;n<5;n++)
            {
                float a=n*2.399f;var at=centre+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(AquariumModel.RockRadii[1]+.56f);
                float size=.42f+(n%2)*.055f;
                at.y=AquariumSeabed.Height(at.x,at.z)+.02f;if(!PlantingClear(at,.65f))continue;
                for(int stone=0;stone<3;stone++)
                {
                    float angle=a+stone*2.3f;var foot=at+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*size*.27f;
                    foot.y-=.025f;
                    rubble.Ellipsoid(foot,new Vector3(size*.31f,size*.14f,size*.26f),C(0x7f8b7c),10,7,.24f);
                }
                LivingAnemone(soft,at,size,n%2,n+17,marker);
                if(n==0){marker.anemonePreviewPoint=at;marker.biodiversityPreviewPoint=centre;}
                if(n%2==1)marker.mintAnemonePreviewPoint=at;
                var back=at+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*1.1f;back.y=AquariumSeabed.Height(back.x,back.z);
                if(PlantingClear(back,.65f)){Seaweed(plants,back,1.7f,a,r,marker);marker.kelpFronds++;}
            }
            for(int group=0;group<AquariumMonolithLayout.Groups.Length;group++)
            {
                var min=AquariumMonolithLayout.Minimum[group];var max=AquariumMonolithLayout.Maximum[group];int planted=0;
                for(int n=0;n<90&&planted<4;n++)
                {
                    float x=Range(r,min.x,max.x),z=Range(r,min.z,max.z),y=max.y;
                    for(;y>.25f;y-=.07f)if(AquariumMonolithLayout.Distance(new Point(x,y,z),group)<0)break;
                    if(y<AquariumSeabed.Height(x,z)+.1f||y>5.8f)continue;
                    var p=new Point(x,y+.012f,z);if(AquariumMonolithLayout.Normal(p,group).y<.72f)continue;
                    LivingAnemone(soft,new Vector3(x,p.y,z),Range(r,.27f,.42f),(group+n)%2,group*97+n,marker);planted++;
                }
            }
            if(soft.Count>0)soft.Emit("34 • Living anemones / rose flowing and mint rounded crowns",root,softMaterial);
            if(rubble.Count>0)rubble.Emit("36 • Anemone holdfast / irregular crevice stones",root,rockMaterial);
            if(plants.Count>0)plants.Emit("35 • Foreground garden / mixed seaweed silhouettes",root,kelpMaterial);
        }
    }
}

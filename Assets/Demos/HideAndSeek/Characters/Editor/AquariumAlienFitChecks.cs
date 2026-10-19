using System;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    public static class AquariumAlienFitChecks
    {
        public static void ImportReview(){AquariumCartoonImport.Run();Review();}
        public static void ImportComplete(){AquariumCartoonImport.Run();Complete();}
        public static void Complete(){Review();AquariumPolishChecks.Run();AquariumCartoonChecks.DeformationSweep();AquariumBuild.Windows();}
        public static void Review()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_CARTOON_QA")??"Temp/CartoonQA";
            Directory.CreateDirectory(Path.Combine(output,"Animation"));
            var camera=AquariumCartoonChecks.Studio();
            var rig=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("AquariumCharacters/Alien")).GetComponent<AquariumRig>();
            rig.Clip("Idle").SampleAnimation(rig.gameObject,0);
            AquariumCartoonChecks.Capture(camera,"Eye-front",new Vector3(0,.1858f,3),new Vector3(0,.1858f,.15f),.39f);
            AquariumCartoonChecks.Capture(camera,"Eye-side",new Vector3(2,.30f,1.8f),new Vector3(0,.1858f,.19f),.42f);
            AquariumCartoonChecks.Capture(camera,"Mouth-closed",new Vector3(1.2f,-2.8f,1.7f),new Vector3(0,-.22f,0),.42f);
            rig.skin.SetBlendShapeWeight(rig.skin.sharedMesh.GetBlendShapeIndex("BellyOpen"),100);
            AquariumCartoonChecks.Capture(camera,"Mouth-open",new Vector3(1.2f,-2.8f,1.7f),new Vector3(0,-.22f,0),.42f);
            for(int f=0;f<25;f++)
            {
                rig.Clip("Idle").SampleAnimation(rig.gameObject,0);
                float blink=Mathf.Sin(Mathf.PI*f/24f),at=blink*12;
                for(int i=1;i<=12;i++)
                {
                    string key=i%3==0?"Blink_"+(i*100/12):"BlinkStep_"+i.ToString("00");
                    rig.skin.SetBlendShapeWeight(rig.skin.sharedMesh.GetBlendShapeIndex(key),100*Mathf.Max(0,1-Mathf.Abs(at-i)));
                }
                AquariumCartoonChecks.Capture(camera,"Animation/eye-blink-"+f.ToString("000"),new Vector3(.65f,.28f,3),new Vector3(0,.1858f,.15f),.41f);
            }
            for(int f=0;f<48;f++)
            {
                rig.Clip("Swim").SampleAnimation(rig.gameObject,f*2.4f/48);
                AquariumCartoonChecks.Capture(camera,"Animation/ripple-swim-"+f.ToString("000"),new Vector3(3,.10f,-.25f),new Vector3(0,-.10f,-.40f),1.03f);
                if(f%12==0)AquariumCartoonChecks.Capture(camera,"Ripple-pose-"+f.ToString("00"),new Vector3(2,.8f,3),new Vector3(0,-.05f,-.28f),1.06f);
            }
            UnityEngine.Object.DestroyImmediate(rig.gameObject);
            Debug.Log("ALIEN_FIT_REVIEW_OK");
        }
    }
}

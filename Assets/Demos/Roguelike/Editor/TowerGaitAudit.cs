using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PrototypeLab.Roguelike.Editor
{
    public static class TowerGaitAudit
    {
        [Serializable] sealed class ActorResult
        {
            public int artIndex, samples, maximumSoleJumpFrame, maximumSoleJumpSide;
            public Vector2 jumpFrom, jumpTo;
            public float maximumShoeStrain, maximumSoleJump;
        }
        [Serializable] sealed class Report
        {
            public int frames;
            public List<ActorResult> actors = new List<ActorResult>();
        }
        public static string Capture(string directory,int first=-1)
        {
            Directory.CreateDirectory(directory);
            int[] indices = { 106, 105, 118, 102, 129 };
            if(first>=100)
                for(int i=0;i<indices.Length;i++)indices[i]=first+i;
            var actors = new List<TowerActorArt>();
            var rigs = new List<TowerMythRig>();
            var probes = new List<int[][]>();
            var rests = new List<Vector3[]>();
            var previous = new List<Vector2[]>();
            var report = new Report();
            var stage = new GameObject("Temporary moving gait review") { hideFlags = HideFlags.HideAndDontSave };
            var cameraObject = new GameObject("Temporary gait review camera") { hideFlags = HideFlags.HideAndDontSave };
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 1.8f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.075f,.09f,.12f);
            camera.cullingMask = 1 << 31;
            var target = new RenderTexture(1920,600,24); camera.targetTexture = target;
            var pixels = new Texture2D(1920,600,TextureFormat.RGB24,false);
            var savedTarget = RenderTexture.active;
            var floorTexture = new Texture2D(1,1); floorTexture.SetPixel(0,0,Color.white); floorTexture.Apply();
            var floorSprite = Sprite.Create(floorTexture,new Rect(0,0,1,1),Vector2.one*.5f,1);
            try
            {
                for (int x = -8; x < 30; x++)
                {
                    var mark = new GameObject("World floor mark"); mark.transform.SetParent(stage.transform);
                    mark.transform.position = new Vector3(x,-.09f,0); mark.transform.localScale = new Vector3(.015f,.16f,1);
                    var sprite = mark.AddComponent<SpriteRenderer>(); sprite.sprite = floorSprite; sprite.color = new Color(.3f,.4f,.45f);
                }
                for (int i=0;i<indices.Length;i++)
                {
                    var root = new GameObject(indices[i].ToString()); root.transform.SetParent(stage.transform);
                    root.transform.position = new Vector3((i-2)*2.2f,0,0); root.transform.localScale = Vector3.one*.82f;
                    var art = TowerActorArt.Create(root.transform,indices[i]); actors.Add(art);
                    var rig = art.GetComponentInChildren<TowerMythRig>(); rigs.Add(rig);
                    for(int f=0;f<20;f++)art.SetPose(false,false,false,1,1f/60);
                    var rest = rig.CaptureRestVertices(); rests.Add(rest);
                    var parts = rig.CapturePartIndices();
                    var pairs = new int[2][];
                    for(int side=0;side<2;side++)
                    {
                        int lo=-1,hi=-1,part=side==0?9:12;
                        for(int v=0;v<parts.Length;v++)if(parts[v]==part)
                        {
                            if(lo<0||rest[v].x<rest[lo].x)lo=v;
                            if(hi<0||rest[v].x>rest[hi].x)hi=v;
                        }
                        pairs[side]=new[]{lo,hi};
                    }
                    probes.Add(pairs); previous.Add(new[]{rig.LeftFootPosition,rig.RightFootPosition});
                    report.actors.Add(new ActorResult{artIndex=indices[i]});
                }
                foreach(Transform item in stage.GetComponentsInChildren<Transform>())item.gameObject.layer=31;
                float travel=0;
                for(int frame=0;frame<240;frame++)
                {
                    float speed=frame<20||frame>=220?0:frame<80?1.5f:frame<150?3:5;
                    travel+=speed/60;
                    for(int i=0;i<actors.Count;i++)
                    {
                        var art=actors[i]; var rig=rigs[i];
                        art.transform.parent.position=new Vector3((i-2)*2.2f+travel,0,0);
                        art.SetLocomotion(true,0,false,speed,speed); art.SetPose(speed>0,false,false,1,1f/60);
                        var vertices=rig.CaptureVertices(); var rest=rests[i];
                        for(int side=0;side<2;side++)
                        {
                            int a=probes[i][side][0],b=probes[i][side][1];
                            if(a<0||b<0)throw new InvalidOperationException("Missing painted shoe "+indices[i]);
                            float length=Vector3.Distance(rest[a],rest[b]);
                            if(length>.001f)report.actors[i].maximumShoeStrain=Mathf.Max(report.actors[i].maximumShoeStrain,
                                Mathf.Abs(Vector3.Distance(vertices[a],vertices[b])/length-1));
                            Vector2 sole=side==0?rig.LeftFootPosition:rig.RightFootPosition;
                            float jump=Vector2.Distance(sole,previous[i][side]);
                            if(jump>report.actors[i].maximumSoleJump)
                            {
                                report.actors[i].maximumSoleJump=jump;
                                report.actors[i].maximumSoleJumpFrame=frame;
                                report.actors[i].maximumSoleJumpSide=side;
                                report.actors[i].jumpFrom=previous[i][side];report.actors[i].jumpTo=sole;
                            }
                            previous[i][side]=sole;
                        }
                        report.actors[i].samples++;
                    }
                    if(frame%3!=0)continue;
                    camera.transform.position=new Vector3(travel,.85f,-10);
                    camera.Render(); RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1920,600),0,0); pixels.Apply();
                    File.WriteAllBytes(Path.Combine(directory,"frame-"+(frame/3).ToString("000")+".png"),pixels.EncodeToPNG());
                    report.frames++;
                }
                File.WriteAllText(Path.Combine(directory,"gait-report.json"),JsonUtility.ToJson(report,true));
                return JsonUtility.ToJson(report,true);
            }
            finally
            {
                RenderTexture.active=savedTarget; camera.targetTexture=null;
                UnityEngine.Object.DestroyImmediate(stage); UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
                UnityEngine.Object.DestroyImmediate(floorSprite); UnityEngine.Object.DestroyImmediate(floorTexture);
            }
        }
    }
}

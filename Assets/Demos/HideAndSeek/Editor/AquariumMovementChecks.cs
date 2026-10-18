using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace PrototypeLab.Aquarium.Editor
{
    // Exercise the displayed transforms, rather than the already-filtered animation speed.
    public static class AquariumMovementChecks
    {
        public static void RunAndBuild()
        {
            Run();
            AquariumSwimmingChecks.Run();
            AquariumBuild.Windows();
        }
        [UnityEditor.MenuItem("Aquarium/Check Movement Smoothness")]
        public static void Run()
        {
            string output=Environment.GetEnvironmentVariable("AQUARIUM_MOVEMENT_QA");
            if(string.IsNullOrEmpty(output))output="Artifacts/MovementQA/current.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));
            var result=new StringBuilder("deterministic real Draw + PlayerPosition + camera; speed=2 metres/second\n");
            var failures=new List<string>();
            var root=new GameObject("Movement QA");
            Camera existing=Camera.main;
            AquariumView view=null;
            try
            {
                view=new AquariumView(root.transform);
                if(existing==null)view.Camera.transform.SetParent(root.transform);
                int round=1;
                foreach(string form in new[]{"alien","fish","diver"})
                    foreach(int hz in new[]{30,20})foreach(int fps in new[]{60,144})
                        Measure(view,form,hz,fps,round++,result,failures);
                foreach (int fps in new[] { 60, 144 }) Practice(view, fps, result, failures);
                result.AppendLine(failures.Count==0?"OK":"FAILURES="+failures.Count);
                foreach(string failure in failures)result.AppendLine("FAIL "+failure);
                File.WriteAllText(output,result.ToString());
                Debug.Log("AQUARIUM_MOVEMENT_CHECKS "+result);
                if(failures.Count>0)throw new Exception("Aquarium displayed movement checks failed: "+failures.Count+"; "+output);
            }
            finally
            {
                Shader.SetGlobalFloat("_AquariumPreviewTimeEnabled",0);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static AquariumSnapshot Snapshot(string form,int round,float time,Point position,bool moving=true,RoundPhase phase=RoundPhase.Playing)
        {
            return new AquariumSnapshot
            {
                round=round,time=time,phase=phase,finishedAt=phase==RoundPhase.Finished?time:0,
                players=new[]{new Swimmer{id=1,name="Movement QA",team=form=="diver"?Team.Seeker:Team.Hider,
                    species=form=="fish"?0:-1,position=position,velocity=moving?new Point(0,0,2):new Point(),facing=new Point(0,0,1)}},
                fish=new Prey[0],harpoons=new Harpoon[0],effects=new WaterEffect[0],eggs=new RevivalEgg[0]
            };
        }

        private static void Measure(AquariumView view,string form,int hz,int fps,int round,StringBuilder result,List<string> failures)
        {
            float dt=1f/fps;
            string label=form+" snapshot_"+hz+"hz render_"+fps;
            var start=new Point(0,9,-4);
            var state=Snapshot(form,round,0,start);
            var camera=new AquariumThirdPersonCamera(view.Camera);
            view.Draw(state,1,dt);camera.Prepare(state,1);camera.UpdatePose(state,view,dt);
            Vector3 previousPlayer=view.PlayerPosition(state.players[0]),previousCamera=view.Camera.transform.position;
            float minPlayer=float.MaxValue,maxPlayer=float.MinValue,minCamera=float.MaxValue,maxCamera=float.MinValue;
            int sample=0;
            for(int frame=1;frame<=fps*3;frame++)
            {
                // A fresh value snapshot prevents the presenter from seeing a future mutation.
                int next=(int)Math.Floor(frame*(double)hz/fps);
                if(next!=sample){sample=next;float time=sample/(float)hz;state=Snapshot(form,round,time,start+new Point(0,0,time*2));}
                view.Draw(state,1,dt);camera.Prepare(state,1);camera.UpdatePose(state,view,dt);
                Vector3 player=view.PlayerPosition(state.players[0]),pose=view.Camera.transform.position;
                if(frame>fps)
                {
                    float playerSpeed=(player.z-previousPlayer.z)/dt,cameraSpeed=(pose.z-previousCamera.z)/dt;
                    minPlayer=Mathf.Min(minPlayer,playerSpeed);maxPlayer=Mathf.Max(maxPlayer,playerSpeed);
                    minCamera=Mathf.Min(minCamera,cameraSpeed);maxCamera=Mathf.Max(maxCamera,cameraSpeed);
                }
                previousPlayer=player;previousCamera=pose;
            }
            result.AppendLine(label+" player_speed="+Number(minPlayer)+".."+Number(maxPlayer)+" camera_speed="+Number(minCamera)+".."+Number(maxCamera));
            Check(minPlayer>=1.8f && maxPlayer<=2.2f,label+" displayed player speed has snapshot ripple",failures);
            Check(minCamera>=1.8f && maxCamera<=2.2f,label+" camera speed has snapshot ripple",failures);
            if(hz!=30 || fps!=60)return;

            // Continue publishing stopped snapshots, then require the final quarter-second to settle.
            var stop=start+new Point(0,0,6);float stopMotion=0;
            for(int frame=1;frame<=fps*2;frame++)
            {
                if(frame%(fps/hz)==0)state=Snapshot(form,round,3+frame*dt,stop,false);
                view.Draw(state,1,dt);camera.Prepare(state,1);camera.UpdatePose(state,view,dt);
                var player=view.PlayerPosition(state.players[0]);
                if(frame>fps*1.75f)stopMotion=Mathf.Max(stopMotion,Vector3.Distance(previousPlayer,player));
                previousPlayer=player;
            }
            Check(stopMotion<.0001f,label+" keeps drifting after stopping",failures);
            state=Snapshot(form,round,5,stop,false,RoundPhase.Finished);
            view.Draw(state,1,dt);camera.Prepare(state,1);camera.UpdatePose(state,view,dt);
            Vector3 frozen=view.PlayerPosition(state.players[0]);
            state=Snapshot(form,round,6,stop+new Point(3,0,0),false,RoundPhase.Finished);
            for(int frame=0;frame<fps;frame++)view.Draw(state,1,dt);
            Check(Vector3.Distance(frozen,view.PlayerPosition(state.players[0]))<.0001f,label+" finished scene moves",failures);

            state=Snapshot(form,round+100,0,start,false);
            view.Draw(state,1,dt);
            Check(Vector3.Distance(view.PlayerPosition(state.players[0]),new Vector3(start.x,start.y,start.z))<.001f,label+" new round carries old displayed position",failures);
            state=Snapshot(form,round+100,0,start+new Point(2,0,0),false,RoundPhase.Lobby);
            view.Draw(state,1,dt);
            state=Snapshot(form,round+100,0,start,false);
            view.Draw(state,1,dt);
            Check(Vector3.Distance(view.PlayerPosition(state.players[0]),new Vector3(start.x,start.y,start.z))<.001f,label+" lobby-to-play reset carries old displayed position",failures);
            state=Snapshot(form,round+100,.05f,start,false);
            state.players[0].swallowedBy=2;state.players[0].swallowedFrom=start;state.players[0].visualAt=.05f;
            view.Draw(state,1,dt);
            state=Snapshot(form,round+100,.1f,start+new Point(1,0,0),false);
            view.Draw(state,1,dt);
            Check(Vector3.Distance(view.PlayerPosition(state.players[0]),new Vector3(start.x+1,start.y,start.z))<.001f,label+" release reuses swallowed motion history",failures);
            state=Snapshot(form,round+100,.15f,start,false);
            state.players[0].alive=false;state.players[0].deathAt=.15f;
            view.Draw(state,1,dt);
            state=Snapshot(form,round+100,.2f,start+new Point(1,0,0),false);
            view.Draw(state,1,dt);
            Check(Vector3.Distance(view.PlayerPosition(state.players[0]),new Vector3(start.x+1,start.y,start.z))<.001f,label+" revival reuses dead motion history",failures);
            result.AppendLine(label+" stop_max_frame_motion="+Number(stopMotion)+" finish/reset/release/revival checked");
        }

        private static void Practice(AquariumView view, int fps, StringBuilder result, List<string> failures)
        {
            using (var session = new AquariumSession())
            {
                session.StartPractice("Movement QA", Team.Hider);
                foreach (var p in session.Model.Players) { p.bot = false; p.position = new Point(-25, 9, -5 - p.id); }
                session.Model.Fish.Clear();
                session.Model.Fish.Add(new Prey { id = 100, position = new Point(-25, 10, -5), turnAt = float.MaxValue });
                var owner = session.Model.Player(session.LocalId);
                owner.position = new Point(0, 9, -4); owner.facing = new Point(0, 0, 1);
                float dt = 1f / fps, min = float.MaxValue, max = 0, maxLag = 0;
                var camera = new AquariumThirdPersonCamera(view.Camera);
                view.Draw(session.State, session.LocalId, dt, session.PresentationTime);
                var previous = view.PlayerPosition(owner);
                for (int frame = 0; frame < fps * 3; frame++)
                {
                    session.Input(new SwimInput { z = 1, aimZ = 1 });
                    session.Update(dt);
                    view.Draw(session.State, session.LocalId, dt, session.PresentationTime);
                    camera.Prepare(session.State, session.LocalId); camera.UpdatePose(session.State, view, dt);
                    var position = view.PlayerPosition(owner);
                    if (frame >= fps)
                    {
                        float speed = (position.z - previous.z) / dt;
                        min = Mathf.Min(min, speed); max = Mathf.Max(max, speed);
                        float lag = owner.position.z - position.z;
                        maxLag = Mathf.Max(maxLag, lag);
                        Check(lag >= -.0001f, "practice presenter predicts past authoritative movement", failures);
                    }
                    previous = position;
                }
                float expected = AquariumModel.HiderSpeed;
                Check(min >= expected * .97f && max <= expected * 1.03f, "practice " + fps + "fps still ripples with shared host snapshot objects", failures);
                Check(maxLag <= expected / 30 + .001f, "practice presenter adds more than one simulation tick of delay", failures);
                result.AppendLine("practice render_" + fps + " player_speed=" + Number(min) + ".." + Number(max) + " max_lag_metres=" + Number(maxLag));
            }
        }
        private static void Check(bool condition,string failure,List<string> failures){if(!condition)failures.Add(failure);}
        private static string Number(float value)=>value.ToString("F4",CultureInfo.InvariantCulture);
    }
}

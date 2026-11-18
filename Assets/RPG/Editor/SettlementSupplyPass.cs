using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Profiling;
using System.Reflection;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class SettlementSupplyPass
    {
        const string Folder=".code-check/world-production/supplies/";
        const string RootName="Route supplies";
        const string Props="Assets/RPG/Art/FantasyProps/FBX/";
        [Serializable] internal sealed class Placement
        {
            public string name, prefab, prefabHash;
            public Vector3 position;
            public float yaw;
        }
        [Serializable] internal sealed class Plan
        {
            public string sceneHash;
            public Placement[] items;
        }
        [Serializable] sealed class Review
        {
            public string planHash, sceneHash, beforeImage, afterImage;
            public bool agentVisualReview;
        }
        static bool rendering;
        static int viewIndex, startFrame, lastFrame, requestedFrame;
        static string pendingImage;
        static readonly List<double> frameTimes=new List<double>();
        static readonly StringBuilder previewReport=new StringBuilder();
        static ProfilerRecorder batches, triangles;
        static double[] beforeP95=new double[3];
        static string Hash(string path)
        {
            using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","");
        }
        static Placement Item(string type,string name,float x,float z,float yaw)
            =>new Placement {name=name,prefab=Props+(type=="Crate" ? "Crate_Wooden" : type)+".fbx",position=new Vector3(x,0,z),yaw=yaw};

        public static void Survey()
        {
            try
            {
                WorldProductionAudit.Open(); Directory.CreateDirectory(Folder);
                if(GameObject.Find(RootName)) throw new Exception("Supply placement already exists; inspect rather than duplicate.");
                var report=new StringBuilder($"SUPPLY SURVEY UTC={DateTime.UtcNow:O}\nRead-only; temporary instances removed, no scene/assets saved.\n");
                var chosen=new List<Placement>();
                chosen.AddRange(FindGroup(new Rect(-15,-9,6,5),new Vector2(-12,-5),new[]
                {
                    Item("Crate","Emberwatch packed supply crate",0,0,10),
                    Item("Crate","Emberwatch outbound supply crate",1.4f,.15f,90),
                    Item("Barrel","Emberwatch provision barrel",.2f,-1.4f,15)
                },report));
                var rest=FindGroup(new Rect(-295,40,8,6),new Vector2(-291,44),new[]
                {
                    Item("Bench","Willow Rest resting bench",0,0,90),
                    Item("Barrel","Willow Rest provision barrel",1.8f,-.2f,0)
                },report);
                chosen.AddRange(rest);
                if(chosen.Count==0) throw new Exception("No safe flat location; no terrain flattening or fallback placement permitted.\n"+report);
                var plan=new Plan {sceneHash=Hash(WorldRegions.ScenePath),items=chosen.ToArray()};
                File.WriteAllText(Folder+"plan.json",JsonUtility.ToJson(plan,true));
                report.AppendLine("Source scene SHA256="+plan.sceneHash);
                foreach(var item in plan.items) report.AppendLine($"SELECTED {item.name}: {item.position:F3}, yaw={item.yaw}, prefab={item.prefab}, SHA256={item.prefabHash}");
                File.WriteAllText(Folder+"survey.txt",report.ToString()); EditorApplication.Exit(0);
            }
            catch(Exception e) { Fail("survey",e); }
        }

        static Placement[] FindGroup(Rect area,Vector2 preferred,Placement[] template,StringBuilder report)
        {
            var root=new GameObject("Temporary supply survey");
            try
            {
                var objects=template.Select(p=>Spawn(p,root.transform)).ToArray();
                foreach(var go in objects) report.AppendLine($"SOURCE DIMENSIONS {go.name}: {BoundsOf(go).size:F3}");
                float best=float.PositiveInfinity; Placement[] result=Array.Empty<Placement>(); int rejected=0,valid=0;
                string firstReason="";
                for(float z=area.yMin;z<=area.yMax;z+=1)
                    for(float x=area.xMin;x<=area.xMax;x+=1)
                    {
                        for(int i=0;i<objects.Length;i++)
                        {
                            var p=template[i].position; p.x+=x; p.z+=z; p.y=0;
                            objects[i].transform.position=p; Fit(objects[i]);
                        }
                        Physics.SyncTransforms(); bool clear=true; float spreadTotal=0;
                        foreach(var go in objects)
                        {
                            string problem=Check(go,out float spread); spreadTotal+=spread;
                            if(problem==null) continue;
                            if(firstReason.Length==0) firstReason=problem; clear=false; break;
                        }
                        if(!clear) {rejected++;continue;}
                        valid++; float score=Vector2.Distance(new Vector2(x,z),preferred)+spreadTotal*10;
                        if(score>=best) continue;
                        best=score;
                        result=objects.Select((go,i)=>new Placement
                        {
                            name=template[i].name,prefab=template[i].prefab,prefabHash=Hash(template[i].prefab),
                            position=go.transform.position,yaw=template[i].yaw
                        }).ToArray();
                    }
                report.AppendLine($"GROUP {template[0].name}: valid anchors={valid}, rejected={rejected}, chosen objects={result.Length}, example rejection={firstReason}");
                return result;
            }
            finally {Object.DestroyImmediate(root);Physics.SyncTransforms();}
        }

        static GameObject Spawn(Placement p,Transform parent)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(p.prefab);
            if(!prefab || prefab.GetComponentsInChildren<MonoBehaviour>(true).Length>0 || prefab.GetComponentsInChildren<Rigidbody>(true).Length>0
                || prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length>0) throw new Exception("Only existing rigid scenery models permitted: "+p.prefab);
            var go=new GameObject(p.name);go.transform.SetParent(parent,false);
            go.transform.SetPositionAndRotation(p.position,Quaternion.Euler(0,p.yaw,0));
            var model=(GameObject)PrefabUtility.InstantiatePrefab(prefab,go.transform);
            foreach(var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
            foreach(var animation in model.GetComponentsInChildren<Animation>(true)) animation.enabled=false;
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++) if(mats[i]) mats[i]=WorldArchitectureBuilder.ResolveImportedMaterial(mats[i],"FantasyProps/FBX/"+Path.GetFileName(p.prefab));
                renderer.sharedMaterials=mats;
                if(!Application.isPlaying) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
            if(go.GetComponentsInChildren<MeshRenderer>(false).Length!=1) throw new Exception("Unexpected active renderer count: "+p.prefab);
            if(go.GetComponentsInChildren<Collider>(false).Length==0)
            {
                var mesh=go.GetComponentInChildren<MeshFilter>(); var box=mesh.gameObject.AddComponent<BoxCollider>();
                box.center=mesh.sharedMesh.bounds.center; box.size=mesh.sharedMesh.bounds.size;
            }
            foreach(var t in go.GetComponentsInChildren<Transform>(false)) t.gameObject.isStatic=true;
            if(!Application.isPlaying)
            {
                foreach(var t in go.GetComponentsInChildren<Transform>(false))
                    if(PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                    {PrefabUtility.RecordPrefabInstancePropertyModifications(t);PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);}
                foreach(var animator in model.GetComponentsInChildren<Animator>(true)) PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }
            return go;
        }

        static Bounds BoundsOf(GameObject go) =>go.GetComponentInChildren<MeshRenderer>().bounds;
        static Vector3[] Samples(Bounds b)
        {
            float x=b.extents.x*.95f,z=b.extents.z*.95f;
            return new[] {b.center,b.center+new Vector3(x,0,z),b.center+new Vector3(x,0,-z),b.center+new Vector3(-x,0,z),b.center+new Vector3(-x,0,-z)};
        }
        static void Fit(GameObject go)
        {
            var b=BoundsOf(go); float top=Samples(b).Max(p=>WorldRegions.GroundHeight(p));
            go.transform.position+=Vector3.up*(top-b.min.y-.015f);
        }
        static string Check(GameObject go,out float spread)
        {
            var b=BoundsOf(go);var heights=Samples(b).Select(p=>WorldRegions.GroundHeight(p)).ToArray();
            spread=heights.Max()-heights.Min();
            if(spread>.08f) return "footprint height spread "+spread.ToString("F3");
            float radius=new Vector2(b.extents.x,b.extents.z).magnitude;
            var terrain=WorldRegions.TerrainAt(b.center);
            if(terrain)
                foreach(var tree in terrain.terrainData.treeInstances)
                {
                    var point=terrain.transform.position+Vector3.Scale(tree.position,terrain.terrainData.size);
                    float trunkMargin=1.1f*tree.widthScale;
                    if(Vector2.Distance(new Vector2(b.center.x,b.center.z),new Vector2(point.x,point.z))<radius+trunkMargin)
                        return "terrain vegetation footprint";
                }
            foreach(var route in WorldProductionAudit.Routes())
                for(int i=1;i<route.points.Length;i++)
                    if(Distance(b.center,route.points[i-1],route.points[i])-radius<2.2f) return "travel corridor "+route.name;
            foreach(var npc in Object.FindObjectsByType<TownNPC>(FindObjectsSortMode.None))
                if(Vector2.Distance(new Vector2(b.center.x,b.center.z),new Vector2(npc.transform.position.x,npc.transform.position.z))-radius<4)
                    return "service approach "+npc.name;
            foreach(var giver in Object.FindObjectsByType<QuestGiver>(FindObjectsSortMode.None))
                if(Vector2.Distance(new Vector2(b.center.x,b.center.z),new Vector2(giver.transform.position.x,giver.transform.position.z))-radius<4)
                    return "giver approach "+giver.name;
            foreach(var c in Physics.OverlapBox(b.center,Vector3.Max(b.extents-Vector3.one*.025f,Vector3.one*.01f),Quaternion.identity,~(1<<2),QueryTriggerInteraction.Ignore))
            {
                if(c is TerrainCollider || c.transform.IsChildOf(go.transform)) continue;
                if(c.bounds.max.y<=heights.Min()+.08f) continue;
                return "solid overlap "+c.name;
            }
            return null;
        }
        static float Distance(Vector3 p,Vector3 from,Vector3 to)
        {
            var a=new Vector2(from.x,from.z);var line=new Vector2(to.x,to.z)-a;
            float t=line.sqrMagnitude>0 ? Mathf.Clamp01(Vector2.Dot(new Vector2(p.x,p.z)-a,line)/line.sqrMagnitude) : 0;
            return Vector2.Distance(new Vector2(p.x,p.z),a+line*t);
        }

        internal static GameObject SpawnReviewed()
        {
            var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Folder+"plan.json"));
            if(Hash(WorldRegions.ScenePath)!=plan.sceneHash) throw new Exception("Scene inputs changed; regenerate survey/preview first.");
            if(GameObject.Find(RootName)) throw new Exception("Supply root already exists; do not duplicate.");
            foreach(var p in plan.items)
                if(!p.prefab.StartsWith(Props,StringComparison.Ordinal) || Hash(p.prefab)!=p.prefabHash) throw new Exception("Prefab source changed: "+p.prefab);
            var root=new GameObject(RootName);
            try
            {
                var objects=plan.items.Select(p=>Spawn(p,root.transform)).ToArray();Physics.SyncTransforms();
                foreach(var go in objects)
                {
                    string problem=Check(go,out _);if(problem!=null) throw new Exception(go.name+": "+problem);
                }
                return root;
            }
            catch {Object.DestroyImmediate(root);throw;}
        }

        public static void ApplyReviewed()
        {
            try
            {
                WorldProductionAudit.Open();Directory.CreateDirectory(Folder);
                var review=JsonUtility.FromJson<Review>(File.ReadAllText(Folder+"preview-verified.json"));
                if(!review.agentVisualReview || review.planHash!=Hash(Folder+"plan.json") || review.sceneHash!=Hash(WorldRegions.ScenePath)
                    || !File.Exists(review.beforeImage) || !File.Exists(review.afterImage))
                    throw new Exception("Current plan-bound visual review required; receipt is QA, not broader scene authorization.");
                File.Copy(WorldRegions.ScenePath,Folder+"before-apply.unity",false);
                var root=SpawnReviewed();
                var report=new StringBuilder($"SUPPLIES APPLIED UTC={DateTime.UtcNow:O}\n");
                foreach(Transform child in root.transform)
                {
                    string problem=Check(child.gameObject,out float spread);
                    if(problem!=null) throw new Exception(child.name+": "+problem);
                    report.AppendLine($"{child.name}: position={child.position:F3}, spread={spread:F3}, activeRenderers={child.GetComponentsInChildren<Renderer>(false).Length}, colliders={child.GetComponentsInChildren<Collider>(false).Length}");
                }
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                EditorSceneManager.OpenScene(WorldRegions.ScenePath);Physics.SyncTransforms();
                var saved=GameObject.Find(RootName);
                var plan=JsonUtility.FromJson<Plan>(File.ReadAllText(Folder+"plan.json"));
                if(!saved || saved.transform.childCount!=plan.items.Length) throw new Exception("Saved supply root/count mismatch.");
                foreach(var p in plan.items)
                {
                    var child=saved.transform.Find(p.name);
                    if(!child || Vector3.Distance(child.position,p.position)>.002f || PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.GetComponentInChildren<MeshRenderer>().gameObject)!=p.prefab)
                        throw new Exception("Saved prefab reference/transform mismatch: "+p.name);
                    if(Check(child.gameObject,out _)!=null) throw new Exception("Saved placement clearance mismatch: "+p.name);
                }
                report.AppendLine("PASS after-save/reload: root count, exact transforms, source prefab links, grounding and clearance.");
                report.AppendLine("Only new inert prop instances added. No source prefab/material, building, NPC, ID, terrain, paint or runtime logic changed.");
                File.WriteAllText(Folder+"applied.txt",report.ToString());EditorApplication.Exit(0);
            }
            catch(Exception e) {Fail("apply",e);}
        }

        internal static bool RenderPreviewTick(PlayerController player,string output,bool final)
        {
            if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null) throw new Exception("Native supply preview requires graphics.");
            var names=final ? new[] {"Emberwatch-yard-final","Emberwatch-departure-final","Willow-Rest-final"}
                : new[] {"Emberwatch-yard-before","Emberwatch-departure-before","Willow-Rest-before","Emberwatch-yard-after","Emberwatch-departure-after","Willow-Rest-after"};
            var cameras=new[] {new Vector3(-6,6,2),new Vector3(-31,11,-5),new Vector3(-279,12,39)};
            var targets=new[] {new Vector3(-11.5f,.4f,-5.5f),new Vector3(-11,.5f,-2),new Vector3(-288,6.3f,46)};
            if(!rendering)
            {
                rendering=true;viewIndex=0;pendingImage=null;previewReport.Clear();
                previewReport.AppendLine($"SUPPLY NATIVE PREVIEW UTC={DateTime.UtcNow:O} final={final}\n1280x720; simulation paused, UI hidden, same camera/settings. Editor capture-frame timings only, not standalone GPU FPS. Warm20 rendered frames, sample90 per view.");
                if(final && !GameObject.Find(RootName)) throw new Exception("Applied supply root missing.");
                player.orbit.enabled=false;player.enabled=false;
                foreach(var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                    if(b.enabled && b.GetType().GetMethod("OnGUI",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!=null) b.enabled=false;
                Time.timeScale=0;
                batches=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Batches Count",1);
                triangles=ProfilerRecorder.StartNew(ProfilerCategory.Render,"Triangles Count",1);
                BeginView(player,cameras,targets);
            }
            if(pendingImage!=null)
            {
                // Keep camera and scene unchanged until the asynchronous native screenshot has completed.
                if(Time.renderedFrameCount<=requestedFrame+1 || !File.Exists(pendingImage)) return false;
                pendingImage=null;viewIndex++;
                if(viewIndex>=names.Length) return true;
                if(!final && viewIndex==3)
                {
                    var root=SpawnReviewed();
                    previewReport.AppendLine($"Preview objects={root.transform.childCount}, added active renderers={root.GetComponentsInChildren<Renderer>(false).Length}; source scene not saved.");
                }
                BeginView(player,cameras,targets);return false;
            }
            // Fresh imports can render cyan placeholders while visible shader variants compile.
            if(ShaderUtil.anythingCompiling)
            {startFrame=Time.renderedFrameCount;frameTimes.Clear();return false;}
            if(Time.renderedFrameCount==lastFrame) return false;
            lastFrame=Time.renderedFrameCount;
            if(Time.renderedFrameCount-startFrame<=20) return false;
            double ms=Time.unscaledDeltaTime*1000;
            if(ms>0) frameTimes.Add(ms);
            if(frameTimes.Count<90) return false;
            var sorted=frameTimes.OrderBy(t=>t).ToArray();
            double p95=sorted[(int)(sorted.Length*.95)];
            previewReport.AppendLine($"{names[viewIndex]} medianMs={sorted[45]:F3} p95Ms={p95:F3} batches={(batches.Valid ? batches.LastValue : -1)} triangles={(triangles.Valid ? triangles.LastValue : -1)} activeSceneRenderers={Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length}");
            if(!final)
            {
                if(viewIndex<3) beforeP95[viewIndex]=p95;
                else previewReport.AppendLine("Steady editor sample: "+(p95>beforeP95[viewIndex%3]*1.25+2 ? "INVESTIGATE timing increase" : "no increase above 25 percent +2ms threshold in this sample"));
            }
            pendingImage=output+"/"+names[viewIndex]+".png";requestedFrame=Time.renderedFrameCount;
            ScreenCapture.CaptureScreenshot(pendingImage);
            File.WriteAllText(output+"/supply-preview.txt",previewReport.ToString());return false;
        }
        static void BeginView(PlayerController player,Vector3[] cameras,Vector3[] targets)
        {
            int i=viewIndex%3;var camera=player.orbit.GetComponent<Camera>();
            camera.transform.position=cameras[i];camera.transform.LookAt(targets[i]);
            startFrame=Time.renderedFrameCount;lastFrame=-1;frameTimes.Clear();
        }
        internal static void EndPreview()
        {
            batches.Dispose();triangles.Dispose();rendering=false;
        }
        static void Fail(string stage,Exception e)
        {
            Directory.CreateDirectory(Folder);File.WriteAllText(Folder+stage+"-error.txt",e.ToString());Debug.LogException(e);EditorApplication.Exit(1);
        }
    }
}

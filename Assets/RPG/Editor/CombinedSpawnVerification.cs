using System;
using System.IO;
using System.Text;
using Riftbound.Network;
using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    public static class CombinedSpawnVerification
    {
        public static void Run()
        {
            var report=new StringBuilder($"COMBINED SPAWN HEIGHT UTC={DateTime.UtcNow:O}\n");
            int passed=0,failed=0;
            try
            {
                WorldProductionAudit.Open();
                report.AppendLine("Native saved-scene terrain-collider rays versus production spawn helper; not gameplay/input/online acceptance. No scene/assets saved.");
                report.AppendLine("Active terrain="+(Terrain.activeTerrain ? Terrain.activeTerrain.name : "none"));
                var points=new[] {new Vector3(0,0,-6),new Vector3(-210,0,-30),new Vector3(-40,0,400),
                    new Vector3(300,0,0),new Vector3(300,0,400),new Vector3(63,0,223)};
                foreach(var point in points)
                {
                    if(!Ground(point,out float floor)) throw new Exception("Missing independent collider support at "+point);
                    var origin=new Vector3(point.x,floor+.1f,point.z);
                    for(int slot=0;slot<4;slot++)
                    {
                        var actual=RiftboundCoopPlayer.ComputeSpawnPosition(origin,slot);
                        if(!Ground(actual,out float targetFloor)) throw new Exception("Spawn outside collider coverage: "+actual);
                        float expected=slot==0 ? origin.y : Mathf.Max(targetFloor+.05f,origin.y);
                        float separation=new Vector2(actual.x-origin.x,actual.z-origin.z).magnitude;
                        bool valid=Mathf.Abs(actual.y-expected)<.15f && Mathf.Abs(separation-(slot==0 ? 0 : 2.6f))<.001f;
                        Record(valid,$"origin={origin:F3} slot={slot} actual={actual:F3} colliderGround={targetFloor:F3} expectedY={expected:F3}");
                    }
                }
                // Retain a host standing above terrain, and preserve positions with no terrain underneath.
                foreach(var origin in new[] {new Vector3(0,20,-6),new Vector3(-400,-30,200)})
                    for(int slot=0;slot<4;slot++)
                    {
                        var actual=RiftboundCoopPlayer.ComputeSpawnPosition(origin,slot);
                        Record(Mathf.Abs(actual.y-origin.y)<.001f,$"raised/no-terrain origin={origin:F3} slot={slot} actual={actual:F3}");
                    }
                report.AppendLine($"TOTAL PASS={passed} FAIL={failed}");
                Directory.CreateDirectory(".code-check/integration");
                File.WriteAllText(".code-check/integration/spawn-height-result.txt",report.ToString());
                EditorApplication.Exit(failed==0 ? 0 : 1);
            }
            catch(Exception e)
            {
                Directory.CreateDirectory(".code-check/integration");
                File.WriteAllText(".code-check/integration/spawn-height-error.txt",report+"\n"+e);
                Debug.LogException(e);EditorApplication.Exit(1);
            }

            void Record(bool valid,string message)
            {
                if(valid) passed++; else failed++;
                report.AppendLine((valid ? "PASS " : "FAIL ")+message);
            }
        }

        static bool Ground(Vector3 point,out float height)
        {
            var ray=new Ray(new Vector3(point.x,1000,point.z),Vector3.down);
            foreach(var terrain in Terrain.activeTerrains)
            {
                var collider=terrain.GetComponent<TerrainCollider>();
                if(collider && collider.Raycast(ray,out var hit,2000)) {height=hit.point.y;return true;}
            }
            height=0;return false;
        }
    }
}

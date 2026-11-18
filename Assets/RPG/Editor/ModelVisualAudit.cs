using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Riftbound.Editor
{
    public static class ModelVisualAudit
    {
        [MenuItem("Riftbound/🔍 Run Full Model & Visual Audit", priority = 15)]
        public static void RunAudit()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RIFTBOUND MODEL, TEXTURE, RIG & SCALE AUDIT ===");
            sb.AppendLine($"Timestamp: {DateTime.UtcNow:O}");
            var rp = GraphicsSettings.currentRenderPipeline;
            sb.AppendLine($"RenderPipeline: {(rp ? rp.name + " (" + rp.GetType().Name + ")" : "Built-in")}");

            var scene = EditorSceneManager.GetActiveScene();
            sb.AppendLine($"Active Scene: {scene.path} (dirty={scene.isDirty})\n");

            // 1. Audit all Scene Renderers
            sb.AppendLine("--- 1. SCENE RENDERER & MATERIAL ISSUES ---");
            var allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int sceneMatIssues = 0;
            var seenMatIssues = new HashSet<string>();

            foreach (var r in allRenderers)
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (r.GetComponent<TextMesh>() != null) continue; // Legacy 3D TextMesh uses MeshRenderer without MeshFilter by design
                string path = GetHierarchyPath(r.transform);

                if (r is MeshRenderer)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (!mf || !mf.sharedMesh)
                    {
                        sb.AppendLine($"  [MISSING MESH] {path}");
                        sceneMatIssues++;
                    }
                }
                else if (r is SkinnedMeshRenderer smr && !smr.sharedMesh)
                {
                    sb.AppendLine($"  [MISSING SKINNED MESH] {path}");
                    sceneMatIssues++;
                }

                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    sb.AppendLine($"  [NO MATERIALS] {path}");
                    sceneMatIssues++;
                    continue;
                }

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (!m)
                    {
                        sb.AppendLine($"  [NULL MATERIAL slot {i}] {path}");
                        sceneMatIssues++;
                        continue;
                    }
                    string shName = m.shader ? m.shader.name : "NULL_SHADER";
                    bool badShader = !m.shader || shName.Contains("InternalError") || shName == "Standard" || shName.StartsWith("Legacy Shaders");
                    bool isSub = AssetDatabase.IsSubAsset(m);
                    string matAssetPath = AssetDatabase.GetAssetPath(m);
                    Texture baseTex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    Texture mainTex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                    Color col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                    bool untexturedDefault = (baseTex == null && mainTex == null) &&
                        (isSub || m.name.Contains("Default") || m.name == "Lit" || (col.r > 0.78f && col.g > 0.78f && col.b > 0.78f && !m.name.StartsWith("Animal ")));

                    if (badShader || untexturedDefault)
                    {
                        string key = $"{shName}|{m.name}|{matAssetPath}|sub={isSub}|tex={(baseTex != null || mainTex != null)}|col={ColorUtility.ToHtmlStringRGB(col)}";
                        sceneMatIssues++;
                        if (seenMatIssues.Add(key + "|" + GetRootCategory(r.transform)))
                        {
                            sb.AppendLine($"  [MAT ISSUE] {path} -> mat='{m.name}' shader='{shName}' path='{matAssetPath}' subAsset={isSub} hasTex={(baseTex != null || mainTex != null)} color=#{ColorUtility.ToHtmlStringRGB(col)}");
                        }
                    }
                }
            }
            sb.AppendLine($"Total scene renderer/material flags: {sceneMatIssues}\n");

            // 2. Audit Scene Actors (Player, NPCs, Enemies, Wildlife, Bosses, Mounts)
            sb.AppendLine("--- 2. SCENE ACTORS (SCALE, GROUNDING, ANIMATOR, MATERIALS) ---");
            var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var anim in animators)
            {
                string path = GetHierarchyPath(anim.transform);
                var bounds = ComputeBounds(anim.gameObject);
                string ctrlName = anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "NONE";
                bool avatarValid = anim.avatar != null && anim.avatar.isValid;
                bool isHuman = anim.avatar != null && anim.avatar.isHuman;

                string clipMismatch = "";
                if (anim.runtimeAnimatorController is AnimatorController ac && ac.layers.Length > 0)
                {
                    foreach (var st in ac.layers[0].stateMachine.states)
                    {
                        if (st.state.motion is AnimationClip clip)
                        {
                            if (clip.humanMotion != isHuman)
                            {
                                clipMismatch = $"MISMATCH(avatarHuman={isHuman}, clip='{clip.name}' humanMotion={clip.humanMotion})";
                                break;
                            }
                        }
                    }
                }

                // Check ground delta
                float groundDelta = 0f;
                if (Physics.Raycast( new Vector3(bounds.center.x, bounds.max.y + 1f, bounds.center.z), Vector3.down, out var hit, 25f, ~(1 << 2), QueryTriggerInteraction.Ignore))
                {
                    groundDelta = bounds.min.y - hit.point.y;
                }

                bool hasUntex = HasUntexturedFbxMat(anim.gameObject, out string matSummary);
                bool sizeOdd = bounds.size.y < 0.25f || bounds.size.y > 4.2f;
                bool groundOdd = Mathf.Abs(groundDelta) > 0.35f;

                if (!anim.runtimeAnimatorController || !avatarValid || clipMismatch.Length > 0 || hasUntex || sizeOdd || groundOdd)
                {
                    sb.AppendLine($"  [ACTOR FLAG] {path} | size=({bounds.size.x:F2}, {bounds.size.y:F2}, {bounds.size.z:F2}) groundDelta={groundDelta:F2}m | ctrl='{ctrlName}' avatarValid={avatarValid} human={isHuman} {clipMismatch} | mats={matSummary}");
                }
                else
                {
                    sb.AppendLine($"  [ACTOR OK]   {path} | h={bounds.size.y:F2}m groundDelta={groundDelta:F2}m | ctrl='{ctrlName}' human={isHuman}");
                }
            }

            // 3. Audit all 24 Resources/Characters prefabs
            sb.AppendLine("\n--- 3. RESOURCES/CHARACTERS PREFABS (ALL 24 RACE/CLASS) ---");
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/RPG/Resources/Characters" }))
            {
                string pPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                if (!prefab) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.position = new Vector3(0, -300, 0);
                var anim = inst.GetComponentInChildren<Animator>();
                string animInfo = "NO_ANIM";
                if (anim)
                {
                    anim.Rebind();
                    anim.Update(0f);
                    bool isHuman = anim.avatar && anim.avatar.isHuman;
                    string ctrlName = anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "NONE";
                    string statesInfo = "";
                    if (anim.runtimeAnimatorController is AnimatorController ac && ac.layers.Length > 0)
                    {
                        var stNames = ac.layers[0].stateMachine.states.Select(s =>
                        {
                            var c = s.state.motion as AnimationClip;
                            return $"{s.state.name}(hm={(c ? c.humanMotion.ToString() : "null")})";
                        });
                        statesInfo = string.Join(",", stNames);
                    }
                    animInfo = $"ctrl='{ctrlName}' avatarValid={(anim.avatar && anim.avatar.isValid)} avatarHuman={isHuman} states=[{statesInfo}]";
                }
                var b = ComputeBounds(inst);
                HasUntexturedFbxMat(inst, out string matSum);
                sb.AppendLine($"  {prefab.name}: size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) footY={(b.min.y - (-300f)):F3} | {animInfo} | mats={matSum}");
                UnityEngine.Object.DestroyImmediate(inst);
            }

            // 4. Audit Pets, Animals & Mounts
            sb.AppendLine("\n--- 4. PETS, ANIMALS & MOUNTS ---");
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/RPG/Art/Animals" }))
            {
                string aPath = AssetDatabase.GUIDToAssetPath(guid);
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(aPath);
                if (!model) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inst.transform.position = new Vector3(0, -300, 0);
                var b = ComputeBounds(inst);
                HasUntexturedFbxMat(inst, out string matSum);
                sb.AppendLine($"  Animal FBX '{model.name}' ({aPath}): nativeSize=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) | mats={matSum}");
                UnityEngine.Object.DestroyImmediate(inst);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/RPG/Prefabs" }))
            {
                string pPath = AssetDatabase.GUIDToAssetPath(guid);
                if (pPath.Contains("/Characters/")) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(pPath);
                if (!prefab) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                inst.transform.position = new Vector3(0, -300, 0);
                var anim = inst.GetComponentInChildren<Animator>();
                if (anim) { anim.Rebind(); anim.Update(0f); }
                var b = ComputeBounds(inst);
                HasUntexturedFbxMat(inst, out string matSum);
                sb.AppendLine($"  Prefab '{pPath}': size=({b.size.x:F2},{b.size.y:F2},{b.size.z:F2}) footY={(b.min.y - (-300f)):F3} | mats={matSum}");
                UnityEngine.Object.DestroyImmediate(inst);
            }

            // 5. Audit Skeletons & Other Art Folders (Textures & Importers)
            sb.AppendLine("\n--- 5. SKELETONS & ART TEXTURES ---");
            foreach (string folder in new[] { "Assets/RPG/Art/Skeletons", "Assets/RPG/Art/NPCs", "Assets/RPG/Art/NPCs_WoW", "Assets/RPG/Art/Animals", "Assets/RPG/Materials" })
            {
                if (!Directory.Exists(folder)) continue;
                var texGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
                var modGuids = AssetDatabase.FindAssets("t:Model", new[] { folder });
                var matGuids = AssetDatabase.FindAssets("t:Material", new[] { folder });
                sb.AppendLine($"  Folder '{folder}': models={modGuids.Length}, textures={texGuids.Length}, materials={matGuids.Length}");
                if (folder.Contains("Skeletons"))
                {
                    foreach (var tg in texGuids) sb.AppendLine($"    Skeleton Tex: {AssetDatabase.GUIDToAssetPath(tg)}");
                    foreach (var mg in modGuids)
                    {
                        string mp = AssetDatabase.GUIDToAssetPath(mg);
                        var imp = AssetImporter.GetAtPath(mp) as ModelImporter;
                        var mod = AssetDatabase.LoadAssetAtPath<GameObject>(mp);
                        HasUntexturedFbxMat(mod, out string ms);
                        sb.AppendLine($"    Skeleton Model: {mp} animType={(imp ? imp.animationType.ToString() : "?")} avatarValid={(mod && mod.GetComponentInChildren<Animator>() && mod.GetComponentInChildren<Animator>().avatar ? mod.GetComponentInChildren<Animator>().avatar.isValid.ToString() : "noAnim")} mats={ms}");
                    }
                }
            }

            // Check all Material assets in Assets/RPG/Materials for bad shaders or missing textures
            sb.AppendLine("\n--- 6. MATERIAL ASSETS IN Assets/RPG/Materials ---");
            foreach (string mg in AssetDatabase.FindAssets("t:Material", new[] { "Assets/RPG/Materials" }))
            {
                string mp = AssetDatabase.GUIDToAssetPath(mg);
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (!m) continue;
                string sh = m.shader ? m.shader.name : "NULL";
                Texture bt = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                Texture mt = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null;
                Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : (m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white);
                if (!sh.StartsWith("Universal Render Pipeline") || (bt == null && mt == null && c.r > 0.85f && c.g > 0.85f && c.b > 0.85f))
                {
                    sb.AppendLine($"  [MAT ASSET CHECK] {mp} | shader='{sh}' baseTex={(bt ? bt.name : "NONE")} mainTex={(mt ? mt.name : "NONE")} color=#{ColorUtility.ToHtmlStringRGB(c)}");
                }
            }

            // 7. Animation Clips in UAL1_Standard.fbx & Skeleton Controllers + Village/FantasyProps untextured materials + Overlapping active visuals
            sb.AppendLine("\n--- 7. UAL1 CLIPS, SKELETON STATES, VILLAGE/PROPS MATS & ACTIVE OVERLAPS ---");
            var ualClips = AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/UniversalAnimations/UAL1_Standard.fbx")
                .OfType<AnimationClip>().Where(c => !c.name.StartsWith("__")).Select(c => $"{c.name}(loop={c.isLooping},hm={c.humanMotion})");
            sb.AppendLine("  UAL1_Standard clips: " + string.Join(", ", ualClips));

            foreach (string skCtrl in new[] { "Skeleton Warrior", "Skeleton Mage", "Skeleton Rogue" })
            {
                var ac = AssetDatabase.LoadAssetAtPath<AnimatorController>($"Assets/RPG/Animations/{skCtrl}.controller");
                if (ac && ac.layers.Length > 0)
                {
                    var sts = ac.layers[0].stateMachine.states.Select(s => $"{s.state.name}->{(s.state.motion ? s.state.motion.name : "null")}");
                    sb.AppendLine($"  {skCtrl}.controller states: " + string.Join(", ", sts));
                }
            }

            foreach (string mg in AssetDatabase.FindAssets("t:Material", new[] { "Assets/RPG/Materials" }))
            {
                string mp = AssetDatabase.GUIDToAssetPath(mg);
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (!m) continue;
                if (mp.Contains("Village_") || mp.Contains("FantasyProps_"))
                {
                    Texture bt = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    if (bt == null)
                        sb.AppendLine($"  [UNTEXTURED PACK MAT] {mp} color=#{ColorUtility.ToHtmlStringRGB(c)}");
                }
            }

            // Check enemies & NPCs for multiple ACTIVE visual children or real ground floating (ignoring CharacterController/CapsuleCollider)
            foreach (var ec in UnityEngine.Object.FindObjectsByType<EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!ec.gameObject.activeInHierarchy) continue;
                var activeAnimChildren = ec.GetComponentsInChildren<Animator>(false); // active only
                var activeSkinned = ec.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                if (activeAnimChildren.Length != 1 || activeSkinned.Length == 0)
                    sb.AppendLine($"  [ENEMY VISUAL COUNT] {GetHierarchyPath(ec.transform)} activeAnims={activeAnimChildren.Length} activeSkinned={activeSkinned.Length}");

                var b = ComputeActiveBounds(ec.gameObject);
                float gDelta = MeasureRealGroundDelta(ec.gameObject, b);
                if (Mathf.Abs(gDelta) > 0.35f)
                    sb.AppendLine($"  [ENEMY GROUND DELTA] {GetHierarchyPath(ec.transform)} pos={ec.transform.position} boundsMinY={b.min.y:F2} groundDelta={gDelta:F2}m height={b.size.y:F2}m");
            }
            foreach (var npc in UnityEngine.Object.FindObjectsByType<TownNPC>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!npc.gameObject.activeInHierarchy) continue;
                var activeSkinned = npc.GetComponentsInChildren<SkinnedMeshRenderer>(false);
                if (activeSkinned.Length == 0)
                    sb.AppendLine($"  [NPC NO SKINNED MESH] {GetHierarchyPath(npc.transform)}");
                var b = ComputeActiveBounds(npc.gameObject);
                float gDelta = MeasureRealGroundDelta(npc.gameObject, b);
                if (Mathf.Abs(gDelta) > 0.35f)
                    sb.AppendLine($"  [NPC GROUND DELTA] {GetHierarchyPath(npc.transform)} pos={npc.transform.position} boundsMinY={b.min.y:F2} groundDelta={gDelta:F2}m height={b.size.y:F2}m");
            }

            // 8. Terrain Elevation vs Spawns, Landmarks, Zone Roots, NPCs, Enemies & Nodes
            sb.AppendLine("\n--- 8. TERRAIN ELEVATION & UNDER-MAP AUDIT ---");
            var terrain = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 tPos = terrain.transform.position;
                Vector3 tSize = terrain.terrainData.size;
                sb.AppendLine($"  Terrain '{terrain.name}' pos={tPos} size={tSize} boundsX=[{tPos.x:F1}, {tPos.x + tSize.x:F1}] boundsZ=[{tPos.z:F1}, {tPos.z + tSize.z:F1}]");

                float SampleTerr(Vector3 p) => terrain.SampleHeight(p) + tPos.y;

                var pcAudit = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
                Vector3 EvalRaceSpawn(string race, string faction)
                {
                    if (!pcAudit) return PlayerController.ClampAboveGround(new Vector3(50f, 9.2f, 210f), 0.30f);
                    string oldR = pcAudit.CharacterRace, oldF = pcAudit.CharacterFaction;
                    pcAudit.CharacterRace = race; pcAudit.CharacterFaction = faction;
                    Vector3 s = pcAudit.GetRacialSpawnPosition();
                    pcAudit.CharacterRace = oldR; pcAudit.CharacterFaction = oldF;
                    return s;
                }

                var exitPortal = UnityEngine.Object.FindObjectsByType<DungeonPortal>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .FirstOrDefault(dp => dp.isExitPortal);

                var testSpawns = new (string label, Vector3 pos)[]
                {
                    ("Human Spawn (Emberwatch)", EvalRaceSpawn("Human", "Alliance")),
                    ("Dwarf Spawn (Frostpeak)", EvalRaceSpawn("Dwarf", "Alliance")),
                    ("Elf Spawn (Silverglen)", EvalRaceSpawn("Elf", "Alliance")),
                    ("Undead Spawn (Deathknell)", EvalRaceSpawn("Undead", "Horde")),
                    ("Goblin Spawn (Rustwater)", EvalRaceSpawn("Goblin", "Horde")),
                    ("Orc / Horde Default Spawn (Camp Bloodrock)", EvalRaceSpawn("Brute", "Horde")),
                    ("FlightNode: Gor'Kresh Stronghold (Horde Capital)", PlayerController.ClampAboveGround(FlightMaster.WorldFlightNodes[4].targetPos, 0.35f)),
                    ("FlightNode: Highspire Keep (Alliance Capital)", PlayerController.ClampAboveGround(FlightMaster.WorldFlightNodes[0].targetPos, 0.35f)),
                    ("FlightNode: Duskwood Cathedral Grounds", PlayerController.ClampAboveGround(FlightMaster.WorldFlightNodes[2].targetPos, 0.35f)),
                    ("Cathedral Exit Portal Dest", exitPortal ? PlayerController.ClampAboveGround(exitPortal.destination, 0.35f) : Vector3.zero),
                    ("Battleground Crossroads Arena", PlayerController.ClampAboveGround(new Vector3(0f, 3.5f, 120f), 0.35f)),
                    ("Wyrmcrest Summit Roost", new Vector3(0f, 48.6f, 218f))
                };

                int buriedCount = 0;
                foreach (var (label, p) in testSpawns)
                {
                    float ty = SampleTerr(p);
                    float diff = p.y - ty;
                    bool buried = !(p.y > 40f || p.y < -25f) && diff < -0.25f;
                    if (buried) buriedCount++;
                    string status = (p.y > 40f || p.y < -25f) ? "ELEVATED/INSTANCE" : (buried ? "BURIED UNDER TERRAIN!" : diff > 2.2f ? "FLOATING HIGH!" : "OK");
                    sb.AppendLine($"  [SPAWN CHECK] {label}: codePos={p} | terrainY={ty:F2} | delta={diff:F2}m -> {status}");
                }

                var newWorldRoot = GameObject.Find("11 - New World NPCs");
                if (newWorldRoot != null)
                {
                    foreach (Transform child in newWorldRoot.transform)
                    {
                        Vector3 cp = child.position;
                        if (cp.x < -200f || cp.y < -25f || cp.y >= 40f)
                        {
                            sb.AppendLine($"  [WORLD GROUP] '{child.name}' pos={cp} -> INSTANCE/SUMMIT (children={child.childCount})");
                            continue;
                        }
                        float ty = SampleTerr(cp);
                        bool rootContainer = child.name.StartsWith("14 -") || child.name.StartsWith("15 -");
                        bool groupBuried = !rootContainer && (cp.y - ty) < -0.45f;
                        if (groupBuried) buriedCount++;
                        sb.AppendLine($"  [WORLD GROUP] '{child.name}' pos={cp} | terrainY={ty:F2} | delta={(cp.y - ty):F2}m {(groupBuried ? "-> BURIED!" : "-> OK")} (children={child.childCount})");
                        foreach (Transform sub in child)
                        {
                            Vector3 sp = sub.position;
                            if (sp.x < -200f || sp.y < -25f || sp.y >= 40f) continue;
                            float sty = SampleTerr(sp);
                            // Foundation slabs embed up to 0.55m into terrain; anything deeper than -0.65m is buried!
                            if (sp.y - sty < -0.65f)
                            {
                                buriedCount++;
                                sb.AppendLine($"    [BURIED SUB ITEM] '{child.name}/{sub.name}' worldPos={sp} | terrainY={sty:F2} | delta={(sp.y - sty):F2}m");
                            }
                        }
                    }
                }
                sb.AppendLine($"  Total buried spawns/groups/items: {buriedCount}");
            }

            File.WriteAllText("ModelVisualAuditReport.txt", sb.ToString());
            Debug.Log("[ModelVisualAudit] Wrote ModelVisualAuditReport.txt");
        }

        static Bounds ComputeActiveBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(false);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static float MeasureRealGroundDelta(GameObject actor, Bounds b)
        {
            Vector3 p = actor.transform.position;
            float terrainY = float.NegativeInfinity;
            if (p.x > -200f && p.y > -20f && p.y < 40f)
            {
                var t = Terrain.activeTerrain ?? UnityEngine.Object.FindAnyObjectByType<Terrain>();
                if (t && t.terrainData)
                {
                    Vector3 tp = t.transform.position, ts = t.terrainData.size;
                    if (p.x >= tp.x && p.x <= tp.x + ts.x && p.z >= tp.z && p.z <= tp.z + ts.z)
                        terrainY = t.SampleHeight(p) + tp.y;
                }
            }

            float rayTop = (!float.IsNegativeInfinity(terrainY) ? Mathf.Max(b.max.y, terrainY) : b.max.y) + 2.0f;
            var hits = Physics.RaycastAll(new Vector3(b.center.x, rayTop, b.center.z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore);
            float bestY = terrainY;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(actor.transform)) continue;
                if (h.collider is CharacterController || h.collider is CapsuleCollider) continue;
                float ceiling = !float.IsNegativeInfinity(terrainY) ? Mathf.Max(b.min.y + 0.8f, terrainY + 1.5f) : (b.min.y + 0.8f);
                if (h.point.y <= ceiling && h.point.y > bestY)
                    bestY = h.point.y;
            }
            if (float.IsNegativeInfinity(bestY)) return 0f;
            return b.min.y - bestY;
        }

        static bool HasUntexturedFbxMat(GameObject go, out string summary)
        {
            if (!go) { summary = "NULL_GO"; return true; }
            var list = new List<string>();
            bool flagged = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (!m) { list.Add("NULL_MAT"); flagged = true; continue; }
                    string sh = m.shader ? m.shader.name : "NULL_SH";
                    bool isSub = AssetDatabase.IsSubAsset(m);
                    Texture bt = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    if (!sh.StartsWith("Universal Render Pipeline") || (isSub && bt == null))
                        flagged = true;
                    list.Add($"{m.name}[sh={sh.Replace("Universal Render Pipeline/", "URP/")},sub={isSub},tex={(bt ? bt.name : "none")},c=#{ColorUtility.ToHtmlStringRGB(c)}]");
                }
            }
            summary = string.Join("; ", list.Distinct());
            return flagged;
        }

        static Bounds ComputeBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            var b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }

        static string GetHierarchyPath(Transform t)
        {
            var parts = new List<string>();
            while (t != null) { parts.Add(t.name); t = t.parent; }
            parts.Reverse();
            return string.Join("/", parts);
        }

        static string GetRootCategory(Transform t)
        {
            while (t.parent != null) t = t.parent;
            return t.name;
        }
    }
}

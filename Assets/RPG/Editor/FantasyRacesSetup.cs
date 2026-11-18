using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Riftbound.Editor
{
    public static class FantasyRacesSetup
    {
        const string Source = "Assets/RPG/ThirdParty/QuaterniusFantasyRaces";
        const string Art = "Assets/RPG/Art/FantasyRacesIntegration";
        const string Playable = "Assets/RPG/Resources/Characters/FantasyRaces";
        const string Evidence = ".code-check/fantasy-races";
        const string RegionRoot = "Silverglen and Bloodrock watch";

        public static void Run()
        {
            Directory.CreateDirectory(Evidence);
            try
            {
                Directory.CreateDirectory(Art);
                Directory.CreateDirectory(Playable);
                AssetDatabase.Refresh();
                var report = new StringBuilder("Quaternius Fantasy Races Standard integration\n");
                foreach (var file in Directory.GetFiles(Source + "/Models", "*.fbx"))
                {
                    var meshImporter = (ModelImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                    if (meshImporter.isReadable) continue;
                    meshImporter.isReadable = true;
                    meshImporter.SaveAndReimport();
                }
                foreach (var file in Directory.GetFiles(Source + "/Textures", "*_Normal.png"))
                {
                    var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                    if (importer.textureType == TextureImporterType.NormalMap) continue;
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                var scene = EditorSceneManager.OpenScene(WorldRegions.ScenePath);
                var regional = GameObject.Find(RegionRoot);
                if (regional) Object.DestroyImmediate(regional);
                regional = new GameObject(RegionRoot);

                foreach (string race in new[] { "Elf", "Orc" })
                {
                    foreach (string job in CharacterVisualFactory.AllClasses)
                    {
                        string name = race + "_" + job;
                        var prefab = Build(race, job, false, Playable + "/" + name + ".prefab", report);
                        if (job == "Warrior" || job == "Mage")
                            Pose(prefab, name, job == "Warrior" ? "Run" : "Idle");
                    }

                    var guard = Build(race, "Warrior", true, Art + "/" + race + " Watch.prefab", report);
                    PlaceGuard(guard, regional.transform, race, report);
                }

                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Could not save the regional guards.");
                AssetDatabase.SaveAssets();
                File.WriteAllText(Evidence + "/integration.txt", report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(Evidence + "/error.txt", error.ToString());
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }

        static GameObject Build(string race, string job, bool female, string path, StringBuilder report)
        {
            string sourceKey = race + (female ? "_Female" : "_Male");
            string name = race + "_" + job + (female ? "_Watch" : "");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{Source}/Models/{sourceKey}.fbx");
            if (!source) throw new IOException("Missing imported model " + sourceKey);
            var wrapper = new GameObject(name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                model.transform.SetParent(wrapper.transform, false);
                var rig = model.GetComponentInChildren<Animator>();
                if (!rig || !rig.avatar || !rig.avatar.isValid || !rig.isHuman)
                    throw new InvalidOperationException(sourceKey + " is not ready for Unity Humanoid retargeting.");
                float targetHeight = CharacterVisualFactory.GetProfile(race).height * (female ? .94f : 1f);
                var fore = rig.GetBoneTransform(HumanBodyBones.LeftToes);
                var ankle = rig.GetBoneTransform(HumanBodyBones.LeftFoot);
                if (fore && ankle)
                {
                    var forward = Vector3.ProjectOnPlane(fore.position - ankle.position, Vector3.up);
                    if (forward.sqrMagnitude > .0001f)
                    {
                        var otherToe = rig.GetBoneTransform(HumanBodyBones.RightToes);
                        var otherFoot = rig.GetBoneTransform(HumanBodyBones.RightFoot);
                        if (otherToe && otherFoot) forward += Vector3.ProjectOnPlane(otherToe.position - otherFoot.position, Vector3.up);
                        model.transform.rotation = Quaternion.Euler(0, Vector3.Dot(forward, Vector3.forward) < 0 ? 180 : 0, 0);
                    }
                }

                var body = model.GetComponentsInChildren<SkinnedMeshRenderer>().OrderByDescending(r => r.sharedMesh.vertexCount).First();
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (race == "Elf" && renderer.name.EndsWith("_Horns", StringComparison.OrdinalIgnoreCase)) renderer.enabled = false;
                int muscularity = body.sharedMesh.GetBlendShapeIndex("Size");
                if (race == "Orc" && muscularity >= 0) body.SetBlendShapeWeight(muscularity, 22f);
                var original = RestBounds(model);
                if (original.size.y < .01f) throw new InvalidOperationException("Invalid model bounds for " + sourceKey);
                model.transform.localScale *= targetHeight / original.size.y;
                model.transform.position -= Vector3.up * RestBounds(model).min.y;
                BindSurfaces(model, race, sourceKey);

                var cloth = Styled(name + " dyed cloth", ClothColor(race, job), .16f);
                cloth.SetTexture("_BaseMap", ClothWeave());
                cloth.SetTextureScale("_BaseMap", new Vector2(7, 7));
                cloth.SetFloat("_Cull", 0);
                EditorUtility.SetDirty(cloth);
                var leather = Styled(race + " field leather", race == "Elf" ? "403D35" : "372F2A", .25f);
                var metal = Styled(race + " field plate", race == "Elf" ? "607672" : "594946", .30f, .27f);
                var trim = Styled(race + " field trim", race == "Elf" ? "BAA160" : "B2784A", .32f, .28f);
                TailorBody(body, rig, targetHeight, cloth, leather, name);
                FitGear(rig, targetHeight, job, cloth, leather, metal, trim);

                var spec = StylizedRacialModelSystem.GetPlayableSpec(race, job);
                if (string.IsNullOrEmpty(spec.weaponRightFbx)) spec.weaponRightFbx = "Assets/RPG/Art/NPCs/KnightSword.fbx";
                StylizedRacialModelSystem.AttachWeaponToHand(model, rig, true, spec.weaponRightFbx, spec.palette);
                if (!string.IsNullOrEmpty(spec.weaponLeftFbx))
                    StylizedRacialModelSystem.AttachWeaponToHand(model, rig, false, spec.weaponLeftFbx, spec.palette);

                // Fit gear facing forward in the FBX rest pose. Humanoid retargeting supplies
                // its own forward orientation, so the animated model root stays unrotated.
                model.transform.localRotation = Quaternion.identity;
                var legacy = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/RPG/Resources/Characters/{race}_{job}.prefab");
                rig.runtimeAnimatorController = legacy.GetComponentInChildren<Animator>(true).runtimeAnimatorController;
                rig.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                rig.applyRootMotion = false;
                rig.Rebind();
                Sample(rig, "Idle", .18f);
                model.transform.position -= Vector3.up * RestBounds(model).min.y;
                var motion = new StringBuilder();
                foreach (string state in new[] { "Walk", "Run", "WalkBack", "RunBack", "Jump", "Ride" })
                {
                    Sample(rig, state, .27f);
                    var foot = rig.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var first = foot.position;
                    Sample(rig, state, .71f);
                    motion.Append($" {state}={Vector3.Distance(first, foot.position):F3}m");
                }
                Sample(rig, "Idle", 0);
                var eyes = model.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.name.EndsWith("_Eyes", StringComparison.OrdinalIgnoreCase));
                var eyeMesh = new Mesh();
                eyes.BakeMesh(eyeMesh);
                float faceOffset = eyes.transform.TransformPoint(eyeMesh.bounds.center).z - rig.GetBoneTransform(HumanBodyBones.Head).position.z;
                Object.DestroyImmediate(eyeMesh);
                if (faceOffset < -.035f) throw new InvalidOperationException(name + " rendered backward after animation retargeting.");
                report.AppendLine($"PASS {name}: valid Humanoid avatar and six retargeted states; sample foot displacement:{motion}.");
                return PrefabUtility.SaveAsPrefabAsset(wrapper, path);
            }
            finally { Object.DestroyImmediate(wrapper); }
        }

        static void Sample(Animator animator, string name, float phase)
        {
            int state = Animator.StringToHash(name);
            if (!animator.HasState(0, state)) throw new InvalidOperationException(animator.name + " controller lacks " + name);
            animator.Play(state, 0, phase);
            animator.Update(0);
            if (animator.GetCurrentAnimatorStateInfo(0).shortNameHash != state)
                throw new InvalidOperationException(animator.name + " could not sample " + name);
        }

        static void BindSurfaces(GameObject model, string race, string key)
        {
            var skin = Textured(key + " skin", $"T_{key}_BaseColor_1", $"T_{key}_Normal");
            var hair = Textured("Fantasy race hair", "T_FantasyHair_BaseColor_1", "T_FantasyHair_Normal");
            var eye = Textured(race + " eyes", race == "Elf" ? "T_Eye_Green" : "T_Eye_Orc", "T_Eye_Normal");
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    string slot = (materials[i] ? materials[i].name : renderer.name).ToLowerInvariant();
                    materials[i] = slot.Contains("eye") ? eye : slot.Contains("hair") ? hair : skin;
                }
                renderer.sharedMaterials = materials;
            }
        }

        static Material Textured(string name, string albedo, string normal)
        {
            var material = Styled(name, "FFFFFF", .22f);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Source}/Textures/{albedo}.png");
            if (!tex) throw new IOException("Missing character texture " + albedo);
            material.SetTexture("_BaseMap", tex);
            var normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Source}/Textures/{normal}.png");
            material.SetTexture("_BumpMap", normalMap);
            if (normalMap) material.EnableKeyword("_NORMALMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        static Bounds RestBounds(GameObject model)
        {
            var bounds = new Bounds();
            bool found = false;
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!renderer.enabled) continue;
                var baked = new Mesh();
                renderer.BakeMesh(baked);
                foreach (var v in baked.vertices)
                {
                    var world = renderer.transform.TransformPoint(v);
                    if (!found) { bounds = new Bounds(world, Vector3.zero); found = true; }
                    else bounds.Encapsulate(world);
                }
                Object.DestroyImmediate(baked);
            }
            return bounds;
        }

        static void TailorBody(SkinnedMeshRenderer body, Animator rig, float height, Material cloth, Material leather, string name)
        {
            var sourceMesh = body.sharedMesh;
            var baked = new Mesh(); body.BakeMesh(baked);
            var points = baked.vertices.Select(body.transform.TransformPoint).ToArray();
            Object.DestroyImmediate(baked);
            var mesh = Object.Instantiate(sourceMesh);
            var materials = body.sharedMaterials.ToList();
            var garment = new List<int>(); var boots = new List<int>();
            var waist = rig.GetBoneTransform(HumanBodyBones.Hips).position;
            var leftArm = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            var rightArm = rig.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
            float collar = (leftArm.y + rightArm.y) / 2 + height * .015f;
            float armWidth = Mathf.Abs(leftArm.x - rightArm.x) * .5f + height * .055f;
            for (int sub = 0; sub < sourceMesh.subMeshCount; sub++)
            {
                var triangles = sourceMesh.GetTriangles(sub);
                var skin = new List<int>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var center = (points[triangles[i]] + points[triangles[i + 1]] + points[triangles[i + 2]]) / 3;
                    var target = center.y < height * .205f ? boots
                        : center.y < waist.y + height * .02f || center.y < collar && Mathf.Abs(center.x - waist.x) <= armWidth ? garment : skin;
                    target.Add(triangles[i]); target.Add(triangles[i + 1]); target.Add(triangles[i + 2]);
                }
                mesh.SetTriangles(skin, sub, false);
            }
            if (garment.Count == 0 || boots.Count == 0) throw new InvalidOperationException("Garment cut failed for " + name);
            mesh.subMeshCount = sourceMesh.subMeshCount + 2;
            mesh.SetTriangles(garment, sourceMesh.subMeshCount, false);
            mesh.SetTriangles(boots, sourceMesh.subMeshCount + 1, false);
            mesh.name = name + " tailored body";
            string path = Art + "/" + mesh.name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing) { EditorUtility.CopySerialized(mesh, existing); EditorUtility.SetDirty(existing); Object.DestroyImmediate(mesh); }
            else { AssetDatabase.CreateAsset(mesh, path); existing = mesh; }
            body.sharedMesh = existing;
            materials.Add(cloth);
            materials.Add(leather);
            body.sharedMaterials = materials.ToArray();
            body.updateWhenOffscreen = true;
        }

        static void FitGear(Animator rig, float height, string job, Material cloth, Material leather, Material metal, Material trim)
        {
            float factor = height / 1.9f;
            var hip = rig.GetBoneTransform(HumanBodyBones.Hips);
            var chest = rig.GetBoneTransform(HumanBodyBones.Chest) ?? rig.GetBoneTransform(HumanBodyBones.Spine);
            var leftArm = rig.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var rightArm = rig.GetBoneTransform(HumanBodyBones.RightUpperArm);
            float shoulders = Vector3.Distance(leftArm.position, rightArm.position);
            var belt = hip.position + new Vector3(0, .05f, .005f) * factor;
            Part("Leather war belt", PrimitiveType.Sphere, hip, belt, new Vector3(shoulders * .69f, .096f * factor, .279f * factor), leather);
            Part("Belt clasp", PrimitiveType.Cube, hip, belt + Vector3.forward * .137f * factor, new Vector3(.102f, .078f, .035f) * factor, trim);
            Part("Front tunic panel", PrimitiveType.Cube, hip, belt + new Vector3(0, -.12f, .143f) * factor, new Vector3(.155f, .245f, .036f) * factor, cloth);

            bool heavy = job == "Warrior" || job == "Paladin";
            float depth = .173f * factor;
            var breast = chest.position + new Vector3(0, .134f, depth);
            if (heavy)
                Part("Raised cuirass", PrimitiveType.Sphere, chest, chest.position + new Vector3(0, .128f, .097f) * factor,
                    new Vector3(shoulders * .81f, .295f * factor, .16f * factor), metal);
            else
                Part("Leather chest facing", PrimitiveType.Sphere, chest, chest.position + new Vector3(0, .13f, .105f) * factor,
                    new Vector3(shoulders * .57f, .23f * factor, .09f * factor), leather);
            var emblem = Part("Order insignia", PrimitiveType.Cube, chest, breast, new Vector3(.101f, .101f, .038f) * factor, trim);
            emblem.rotation = Quaternion.Euler(0, 0, 45);
            foreach (var arm in new[] { leftArm, rightArm })
            {
                float side = Mathf.Sign(arm.position.x - hip.position.x);
                if (heavy || job == "Mage")
                {
                    float width = heavy ? .24f : .18f;
                    Part("Forged shoulder plate", PrimitiveType.Sphere, arm, arm.position + new Vector3(side * .024f, .028f, .004f) * factor,
                        new Vector3(width, .145f, .26f) * factor, metal);
                    Part("Trimmed shoulder cap", PrimitiveType.Sphere, arm, arm.position + new Vector3(side * .05f, .06f, .003f) * factor,
                        new Vector3(width * .81f, .098f, .21f) * factor, trim);
                }
            }

            ArmGuard(rig, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, factor, leather, trim);
            ArmGuard(rig, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, factor, leather, trim);
            if (job != "Rogue")
                WearMantle(chest, shoulders, factor, cloth, trim);
        }

        static Texture2D ClothWeave()
        {
            const string path = Art + "/Dyed cloth weave.asset";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture) return texture;
            texture = new Texture2D(64, 64, TextureFormat.RGB24, true);
            texture.name = "Dyed cloth weave";
            texture.wrapMode = TextureWrapMode.Repeat;
            var pixels = new Color[64 * 64];
            var noise = new System.Random(1481);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
            {
                float weave = .76f + .061f * ((x + y) % 2) + .059f * (float)noise.NextDouble();
                pixels[y * 64 + x] = new Color(weave, weave, weave);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        static void WearMantle(Transform chest, float shoulders, float scale, Material cloth, Material trim)
        {
            const string path = Art + "/Folded field mantle.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                var points = new Vector3[27];
                var uv = new Vector2[27];
                var triangles = new List<int>();
                for (int row = 0; row < 3; row++) for (int col = 0; col < 9; col++)
                {
                    float u = col / 8f, side = u * 2 - 1, v = row / 2f;
                    points[row * 9 + col] = new Vector3(side * Mathf.Lerp(.48f, .56f, v), .28f - v * .72f - .036f * Mathf.Abs(side),
                        -.135f - .102f * v + .032f * Mathf.Abs(side) + Mathf.Cos(side * Mathf.PI * 2f) * .014f);
                    uv[row * 9 + col] = new Vector2(u, 1 - v);
                    if (row == 2 || col == 8) continue;
                    int i = row * 9 + col;
                    triangles.AddRange(new[] { i, i + 1, i + 9, i + 1, i + 10, i + 9 });
                }
                mesh = new Mesh { name = "Folded field mantle", vertices = points, uv = uv, triangles = triangles.ToArray() };
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, path);
            }
            var mantle = new GameObject("Folded field mantle");
            mantle.transform.position = chest.position;
            mantle.transform.localScale = new Vector3(shoulders, scale, scale);
            mantle.transform.SetParent(chest, true);
            mantle.AddComponent<MeshFilter>().sharedMesh = mesh;
            mantle.AddComponent<MeshRenderer>().sharedMaterial = cloth;
            var clasp = Part("Mantle clasp", PrimitiveType.Cube, chest, chest.position + new Vector3(0, .21f, -.158f) * scale,
                new Vector3(.086f, .077f, .025f) * scale, trim);
            clasp.rotation = Quaternion.Euler(0, 0, 45);
        }

        static void ArmGuard(Animator rig, HumanBodyBones from, HumanBodyBones to, float scale, Material leather, Material trim)
        {
            var bone = rig.GetBoneTransform(from);
            var hand = rig.GetBoneTransform(to);
            if (!bone || !hand) return;
            var axis = hand.position - bone.position;
            var guard = Part("Reinforced leather bracer", PrimitiveType.Cylinder, bone, bone.position + axis * .64f,
                new Vector3(.136f * scale, axis.magnitude * .28f, .136f * scale), leather);
            guard.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            var rim = Part("Bracer metal cuff", PrimitiveType.Cylinder, bone, bone.position + axis * .84f,
                new Vector3(.141f * scale, .017f * scale, .141f * scale), trim);
            rim.rotation = guard.rotation;
        }

        static Transform Part(string name, PrimitiveType type, Transform parent, Vector3 world, Vector3 scale, Material surface)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<MeshRenderer>().sharedMaterial = surface;
            part.transform.position = world;
            part.transform.localScale = scale;
            part.transform.SetParent(parent, true);
            return part.transform;
        }

        static string ClothColor(string race, string job) => race == "Elf"
            ? job == "Mage" ? "49406B" : job == "Paladin" ? "668182" : job == "Rogue" ? "3B5449" : "315E59"
            : job == "Mage" ? "4D4B69" : job == "Paladin" ? "855448" : job == "Rogue" ? "4B4249" : "673B36";

        static Material Styled(string name, string hex, float smoothness, float metallic = 0)
        {
            string path = $"{Art}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            material.color = color;
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Pose(GameObject prefab, string name, string action)
        {
            var instance = Object.Instantiate(prefab);
            try
            {
                instance.transform.position = Vector3.up * -400;
                instance.transform.rotation = Quaternion.Euler(0, 33, 0);
                Sample(instance.GetComponentInChildren<Animator>(), action, .24f);
                CharacterArtSetup.RenderPose(instance, $"{Evidence}/{name}.png");
            }
            finally { Object.DestroyImmediate(instance); }
        }

        static void PlaceGuard(GameObject prefab, Transform parent, string race, StringBuilder report)
        {
            var center = race == "Elf" ? new Vector2(-150, 75) : new Vector2(280, 210);
            string label = race == "Elf" ? "Silverglen watch sentry" : "Bloodrock watch sentry";
            for (int i = 0; i < 28; i++)
            {
                float angle = (i * 137.5f + (race == "Elf" ? 35 : 152)) * Mathf.Deg2Rad;
                var p = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (13 + i / 7 * 4);
                if (WorldExpansionLayout.RoadDistance(p.x, p.y) < 4.8f) continue;
                var point = new Vector3(p.x, 0, p.y);
                var terrain = WorldRegions.TerrainAt(point);
                if (!terrain) continue;
                point.y = terrain.GetPosition().y + terrain.SampleHeight(point);
                if (Physics.CheckCapsule(point + Vector3.up * .28f, point + Vector3.up * 2f, .235f, ~0, QueryTriggerInteraction.Ignore)) continue;
                var guard = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                guard.name = label;
                guard.transform.SetPositionAndRotation(point, Quaternion.LookRotation(new Vector3(center.x - p.x, 0, center.y - p.y)));
                report.AppendLine($"Placed {label} at {point}.");
                var camera = guard.transform.position + guard.transform.forward * 4.3f + guard.transform.right * 1.6f + Vector3.up * 1.8f;
                DemoPassSetup.RenderWorld(camera, point + Vector3.up * 1.06f, $"{Evidence}/{race}-regional.png");
                return;
            }
            throw new InvalidOperationException("No clear regional sentry site for " + race);
        }
    }
}

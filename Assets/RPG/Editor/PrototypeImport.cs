using UnityEditor;
using UnityEngine;

namespace Riftbound.Editor
{
    public class PrototypeImport : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/RPG/Art/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            if (assetPath.Contains("/Animals/"))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                ConfigurePackClips(importer);
                var clips = importer.clipAnimations;
                foreach (var clip in clips) { string name = clip.name.ToLowerInvariant(); clip.loopTime = name.Contains("idle") || name.Contains("walk") || name.Contains("run"); clip.loopPose = clip.loopTime; }
                importer.clipAnimations = clips; return;
            }
            if (assetPath.Contains("/BruteNative/"))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.motionNodeName = "mixamorig:Hips";
                ConfigureAnimations(importer, assetPath); return;
            }
            if (assetPath.Contains("/Skeletons/"))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                ConfigurePackClips(importer); return;
            }
            if (assetPath.Contains("/UniversalAnimations/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.bakeAxisConversion = true;
                ConfigurePackClips(importer); return;
            }
            if (!assetPath.Contains("/Brute/") && !assetPath.Contains("/Abe/")) return;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            if (assetPath.Contains("/Brute/") && assetPath.ToLowerInvariant().Contains("walk"))
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath("Assets/RPG/Art/Brute/Brute.fbx"))
                    if (asset is Avatar avatar && avatar.isValid)
                    { importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther; importer.sourceAvatar = avatar; break; }
            }
            ConfigureAnimations(importer, assetPath);
        }

        public static void ConfigureAnimations(ModelImporter importer, string path)
        {
            string lowerPath = path.ToLowerInvariant();
            bool locomotion = lowerPath.Contains("walk") || lowerPath.Contains("run");
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = !path.Contains("attack") && !path.Contains("jump") && !path.Contains("react") && !path.Contains("equip") && !path.Contains("disarm") && !path.Contains("to standing") && !path.Contains("turn");
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = !locomotion;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = !locomotion;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = false;
                clip.heightFromFeet = true;
                clip.keepOriginalPositionXZ = !locomotion;
            }
            importer.clipAnimations = clips;
        }

        public static void ConfigurePackClips(ModelImporter importer)
        {
            var clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                string name = clip.name.ToLowerInvariant();
                clip.loopTime = name.Contains("loop") || name.Contains("idle") || name.Contains("walk") || name.Contains("running") || name.Contains("run_a") || name.Contains("run_b");
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true; clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = !importer.assetPath.Contains("_RM.fbx");
                clip.keepOriginalOrientation = false; clip.keepOriginalPositionY = false;
                clip.heightFromFeet = true;
            }
            importer.clipAnimations = clips;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/RPG/Art/")) return;
            var importer = (TextureImporter)assetImporter;
            if (assetPath.Contains("Normal")) importer.textureType = TextureImporterType.NormalMap;
            if (assetPath.Contains("Roughness") || assetPath.Contains("ORM")) importer.sRGBTexture = false;
        }
    }
}

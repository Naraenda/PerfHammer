using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Formats.Fbx.Exporter;
using UnityEditor;
using System.IO;
using UnityEditor.Presets;
using System.Reflection;

namespace PerfHammer
{
    public class Exporter : IModule
    {
        public Exporter(string path, string name) {
            Path = path;
            AssetName = name;

            Directory.CreateDirectory(path);
        }

        public string Path;
        public string AssetName;

        public string Name => "Exporter";

        public GameObject ExportModel(GameObject toExport) {
            var exportedPath = ModelExporter.ExportObject($"{Path}/{AssetName}_optimized.fbx", toExport);

            AssetDatabase.ImportAsset(exportedPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<Object>(exportedPath);

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.transform.SetParent(toExport.transform.parent, worldPositionStays: false);
            instance.transform.SetSiblingIndex(toExport.transform.GetSiblingIndex());

            CopyFromSource(instance, toExport);

            return instance;
        }

        public void ExportMeshes(GameObject obj) {
            foreach (var r in obj.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var exportPath = $"{Path}/{AssetName}_mesh_{r.sharedMesh.name}.asset";
                AssetDatabase.CreateAsset(r.sharedMesh, exportPath);
                var m = AssetDatabase.LoadAssetAtPath<Mesh>(exportPath);
                r.sharedMesh = m;
            }
        }

        public void CopyFromSource(GameObject from, GameObject to) {
            var ass = Assembly.GetAssembly(typeof(ModelExporter));
            var typ = ass.GetType(" UnityEditor.Formats.Fbx.Exporter.ConvertToNestedPrefab");
            var met = typ.GetMethod("UpdateFromSourceRecursive", BindingFlags.Static | BindingFlags.NonPublic);
            met.Invoke(null, new object[] { from, to });
        }

        public Texture2D ExportTexture(Texture2D t, string id) {
            string exportPath = $"{Path}/{AssetName}_{id}.png";

            File.WriteAllBytes(exportPath, t.EncodeToPNG());

            AssetDatabase.ImportAsset(exportPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(exportPath);
        }

        public Material ExportMaterial(Material m, string id) {
            AssetDatabase.CreateAsset(m, $"{Path}/{AssetName}_{id}.mat");
            return m;
        }

        public GameObject Run(Exporter e, GameObject obj, GameObject reference) {
            ExportMeshes(obj);
            return obj;
        }
    }
}

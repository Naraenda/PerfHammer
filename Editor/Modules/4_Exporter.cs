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
    public class Exporter
    {
        public Exporter(string path, string name) {
            Path = path;
            Name = name;

            Directory.CreateDirectory(path);
        }

        public string Path;
        public string Name;
        public GameObject ExportModel(GameObject toExport) {
            var exportedPath = ModelExporter.ExportObject($"{Path}/{Name}_optimized.fbx", toExport);


            AssetDatabase.ImportAsset(exportedPath, ImportAssetOptions.ForceUpdate);
            var prefab = AssetDatabase.LoadAssetAtPath<Object>(exportedPath);

            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            instance.transform.SetParent(toExport.transform.parent, worldPositionStays: false);
            instance.transform.SetSiblingIndex(toExport.transform.GetSiblingIndex());

            CopyFromSource(instance, toExport);

            return instance;
        }

        public void CopyFromSource(GameObject from, GameObject to) {
            var ass = Assembly.GetAssembly(typeof(ModelExporter));
            var typ = ass.GetType(" UnityEditor.Formats.Fbx.Exporter.ConvertToNestedPrefab");
            var met = typ.GetMethod("UpdateFromSourceRecursive", BindingFlags.Static | BindingFlags.NonPublic);
            met.Invoke(null, new object[] { from, to });
        }

        public Texture2D ExportTexture(Texture2D t, string id) {
            string exportPath = $"{Path}/{Name}_{id}.png";

            File.WriteAllBytes(exportPath, t.EncodeToPNG());

            AssetDatabase.ImportAsset(exportPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(exportPath);
        }

        public Material ExportMaterial(Material m, string id) {
            AssetDatabase.CreateAsset(m, $"{Path}/{Name}_{id}.mat");
            return m;
        }
    }
}

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PerfHammer
{
    public class Duplicator : IModule
    {
        public string Name => "Duplicator";

        public void OnGUI() { }

        /// <summary>
        /// Copies the gameobject and duplicates the contained meshes
        /// </summary>
        public GameObject Run(Exporter e, GameObject obj, GameObject reference) {
            var copy = Object.Instantiate(obj);

            var sms = copy.GetComponentsInChildren<SkinnedMeshRenderer>();
            var mfs = copy.GetComponentsInChildren<MeshFilter>();
            var replacements = new Dictionary<Mesh, Mesh>();

            // Skined meshes
            foreach (var r in sms) {
                if (!replacements.ContainsKey(r.sharedMesh))
                    replacements.Add(r.sharedMesh, Object.Instantiate(r.sharedMesh));
                
                r.sharedMesh = replacements[r.sharedMesh];
            }

            // Mesh filters (rigid meshes)
            foreach (var r in mfs) {
                if (!replacements.ContainsKey(r.sharedMesh))
                    replacements.Add(r.sharedMesh, Object.Instantiate(r.sharedMesh));

                r.sharedMesh = replacements[r.sharedMesh];
            }

            foreach (var t in copy.transform.GetComponentsInChildren<Transform>()) {
                while (PrefabUtility.IsPartOfAnyPrefab(t)) {
                    Debug.Log($"Clearing prefab handle {t.name}");
                    var handle = PrefabUtility.GetPrefabInstanceHandle(t);
                    Object.DestroyImmediate(handle);
                }
            }
            return copy;
        }
    }
}

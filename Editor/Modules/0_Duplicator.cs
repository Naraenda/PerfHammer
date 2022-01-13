using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PerfHammer
{
    public static class Duplicator
    {
        /// <summary>
        /// Copies the gameobject and duplicates the contained meshes
        /// </summary>
        public static GameObject Duplicate(GameObject go, string name) {
            var copy = Object.Instantiate(go);
            copy.name = name;

            var sms = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            var mfs = go.GetComponentsInChildren<MeshFilter>();
            var replacements = new Dictionary<Mesh, Mesh>();

            // Skined meshes
            foreach (var r in sms) {
                if (!replacements.ContainsKey(r.sharedMesh))
                    replacements.Add(r.sharedMesh, Mesh.Instantiate(r.sharedMesh));

                r.sharedMesh = replacements[r.sharedMesh];
            }

            // Mesh filters (rigid meshes)
            foreach (var r in mfs) {
                if (!replacements.ContainsKey(r.sharedMesh))
                    replacements.Add(r.sharedMesh, Mesh.Instantiate(r.sharedMesh));

                r.sharedMesh = replacements[r.sharedMesh];
            }

            return copy;
        }
    }
}

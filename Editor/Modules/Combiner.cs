using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PerfHammer
{
    public class Combiner : IModule
    {
        public class Combineable
        {
            public GameObject GameObject;
            public bool Combine = true;

            public override bool Equals(object obj) {
                return GameObject.Equals((obj as Combineable)?.GameObject);
            }

            public override int GetHashCode() {
                return GameObject.GetHashCode();
            }
        }

        public HashSet<Combineable> Combineables;

        public string Name => "Combiner";

        public void Discover(GameObject go) {
            var sms = go.GetComponentsInChildren<SkinnedMeshRenderer>().Select(c => new Combineable() {
                GameObject = c.gameObject,
                Combine    = true,
            });
            var mfs = go.GetComponentsInChildren<MeshFilter>().Select(c => new Combineable() {
                GameObject = c.gameObject,
                Combine    = false,
            });

            var discovered = new HashSet<Combineable>(sms.Union(mfs));

            foreach (var item in Combineables.Except(discovered))
                Combineables.Remove(item);

            foreach (var item in discovered.Except(Combineables))
                Combineables.Add(item);
        }

        GameObject Merge(GameObject obj) {
            var components = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            var targetComponent = components.First();
            var materials = components.SelectMany(r => r.sharedMaterials);
            var merges = components.SelectMany(r => 
                Enumerable.Range(0, r.sharedMesh.subMeshCount).Select(s =>
                    new SkinnedMeshInstance {
                        Mesh = r.sharedMesh,
                        SMR = r,
                        SubMeshIndex = s,
                        Transform = Matrix4x4.identity,
                        Material = r.sharedMaterials[s],
                    }
                )
            ).ToList();
            
            SkinnedMeshCombiner.CombineMeshes(merges, targetComponent);

            foreach (var c in components.Except(new[] { targetComponent }))
                Object.DestroyImmediate(c.gameObject);

            targetComponent.sharedMesh.name = $"{targetComponent.gameObject.name}";

            return obj;
        }

        public GameObject Run(Exporter e, GameObject obj) {
            return Merge(obj);
        }
    }
}

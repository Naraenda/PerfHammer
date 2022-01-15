using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityMeshSimplifier;

namespace PerfHammer
{
    public class Decimator : IModule
    {
        public string Name => "Decimator";

        public enum DecimationMode
        {
            Disabled,
            Losless,
            Lossy,
        }

        public DecimationMode Mode;
        public float Quality = 1;
        public int TargetTris = -1;

        public SimplificationOptions Options = new SimplificationOptions();

        public Mesh Optimize(Mesh m, float quality) {
            if (Mode == DecimationMode.Disabled)
                return m;

            var o = new MeshSimplifier(m);


            if (Mode == DecimationMode.Losless) {
                o.SimplifyMeshLossless();
            } else {
                o.SimplifyMesh(quality);
            }

            return o.ToMesh();
        }

        public GameObject Run(Exporter e, GameObject obj) {
            var rs = obj.GetComponentsInChildren<SkinnedMeshRenderer>();

            var meshes = rs.Select(r => r.sharedMesh);
            var totalTris = meshes.Sum(m => m.triangles.Length) / 3;

            var quality = Quality;
            if (TargetTris > 0)
                quality = (TargetTris * 100 / totalTris) * 0.01f;

            if (quality > 1.0f || TargetTris > totalTris)
                return obj;

            foreach (var r in rs)
                Optimize(r.sharedMesh, quality);

            return obj;
        }
    }
}

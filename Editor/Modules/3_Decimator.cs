using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityMeshSimplifier;

namespace PerfHammer
{
    public static class Decimator
    {
        public enum Mode
        {
            Disabled,
            Losless,
            Lossy,
        }

        public class Config
        {
            public Mode Mode;
            public float Quality;
            public int TargetTris;
        }

        public static Mesh Optimize(Mesh m, Config c) {
            if (c.Mode == Mode.Disabled)
                return m;

            var o = new MeshSimplifier(m);

            var quality = c.Quality;
            if (c.TargetTris > 0) {
                quality = (c.TargetTris * 100 / m.triangles.Length) * 0.01f;

                if (c.TargetTris > m.triangles.Length)
                    return m;
            }

            o.SimplificationOptions = new SimplificationOptions() {

            };

            if (c.Mode == Mode.Losless) {
                o.SimplifyMeshLossless();
            } else {
                o.SimplifyMesh(quality);
            }

            return o.ToMesh();
        }
    }
}

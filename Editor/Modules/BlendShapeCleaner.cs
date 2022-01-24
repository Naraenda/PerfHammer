using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace PerfHammer
{
    public class BlendShapeCleaner : IModule
    {
        public bool RemoveNonAscii = true;
        public bool ApplyNonZero   = true;
        public bool KeepVRCShapes  = true;
        public string Name => "Shape Key Cleaner";

        class BlendShape
        {
            public string Name;
            public float Weight;
            public Vector3[] DeltaVertices;
            public Vector3[] DeltaNormals;
            public Vector3[] DeltaTangents;
        }

        Mesh Optimize(Mesh m, SkinnedMeshRenderer r) {
            // Gather original blend shapes
            var originalBlendShapes = new List<BlendShape>();
            for (int s_i = 0; s_i < m.blendShapeCount; s_i++) {
                var frameCount = m.GetBlendShapeFrameCount(s_i);
                var frame = new BlendShape() {
                    Name   = m.GetBlendShapeName(s_i),
                    Weight = r.GetBlendShapeWeight(s_i),
                };

                if (frameCount > 1) {
                    Debug.LogWarning($"Blendhsape {frame.Name} has more than 1 frame. This is currently unsupported!");
                }

                m.GetBlendShapeFrameVertices(s_i, 0, frame.DeltaVertices, frame.DeltaNormals, frame.DeltaTangents);
                originalBlendShapes.Add(frame);
            }

            // Modify blendshapes
            IEnumerable<BlendShape> blendShapes = originalBlendShapes.AsEnumerable();

            if (ApplyNonZero) {
                var toApply = blendShapes.Where(f => 
                    f.Weight > 0 && 
                    (f.Name.ToLower().Contains("vrc") || !KeepVRCShapes)
                );

                var vertices = m.vertices;
                var normals  = m.normals;
                var tangents = m.tangents;
                foreach (var f in toApply) {
                    var w = f.Weight;
                    for (int v = 0; v < vertices.Length; v++) {
                        vertices[v] += f.DeltaVertices[v] * w;
                        if (f.DeltaNormals != null)
                            normals[v] += f.DeltaNormals[v] * w;
                        if (f.DeltaTangents != null)
                            tangents[v] += (Vector4)f.DeltaTangents[v] * w;
                    }
                }

                blendShapes = blendShapes.Except(toApply);
            }

            if (RemoveNonAscii) {
                blendShapes = blendShapes.Where(f => 
                    !Regex.IsMatch(f.Name, @"[^\x00-\x80]+") || 
                    (KeepVRCShapes && f.Name.ToLower().Contains("vrc")
                ));
            }

            // Replace blendshapes on mesh
            m.ClearBlendShapes();
            foreach (var f in blendShapes) {
                m.AddBlendShapeFrame(f.Name, 1, f.DeltaVertices, f.DeltaNormals, f.DeltaTangents);
            }

            return m;
        }

        public GameObject Run(Exporter e, GameObject obj, GameObject reference) {
            return obj;
            var rs = obj.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var r in rs)
                Optimize(r.sharedMesh, r);

            return obj;
        }
    }
}

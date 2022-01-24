// Modified version of https://github.com/lxteo/UnitySkinnedMeshCombiner
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PerfHammer.Utils
{
    public struct SkinnedMeshInstance
    {
        public Mesh Mesh { get; set; }
        public SkinnedMeshRenderer SMR { get; set; }
        public int SubMeshIndex { get; set; }
        public Matrix4x4 Transform { get; set; }
        public Material Material { get; set; }
        public Transform Bone { get; set; }
    }
    public static class SkinnedMeshCombiner {

        /// <summary>
        /// Combines Unity SkinnedMeshRenderers. Takes a list of SkinnedMeshInstance and outputs a single mesh with working bones and blendshapes.
        /// </summary>
        /// <param name="combine">List of SkinnedMeshInstance</param>
        /// <param name="result">Output, will set sharedMesh and sharedMaterials</param>
        public static void CombineMeshes(List<SkinnedMeshInstance> combine, SkinnedMeshRenderer result) {
            var rInds  = new List<List<int>>();
            var rVerts = new List<Vector3>();
            var tVerts = new List<Vector3>();
            var rUv0s  = new List<Vector2>();
            var tUv0s  = new List<Vector2>();
            var rNorms = new List<Vector3>();
            var tNorms = new List<Vector3>();
            var rTangs = new List<Vector4>();
            var tTangs = new List<Vector4>();
            var rCols  = new List<Color>();
            var tCols  = new List<Color>();
            var rBoneW = new List<BoneWeight>();
            var tBoneW = new List<BoneWeight>();
            var rBone  = new List<Transform>();
            var rBinds = new List<Matrix4x4>();
            var indexMap = new List<int[]>();

            // Convert non-skinned meshes to skinned
            for (var i = 0; i < combine.Count; i++) {
                var comb = combine[i];
                // Generate a skinned mesh
                if (!comb.SMR) {
                    comb.Mesh = UnityEngine.Object.Instantiate(comb.Mesh);
                    comb.Mesh.bindposes = new Matrix4x4[] { comb.Transform };
                    comb.Mesh.boneWeights = Enumerable.Repeat(new BoneWeight {
                        boneIndex0 = 0,
                        weight0 = 1,
                        weight1 = 0,
                        weight2 = 0,
                        weight3 = 0,
                    }, comb.Mesh.vertexCount).ToArray();
                }
                combine[i] = comb;
            }

            var currentIndiceCount = 0;
            var currentSubmeshCount = 0;
            for (var i = 0; i < combine.Count; i++) {
                var comb = combine[i];

                // The mapping of old bone indices to new bone indices
                var boneRemap = new Dictionary<int, int>();
                var boneAdjust = new Dictionary<int, Matrix4x4>();
                var boneCount = comb.SMR ? comb.SMR.bones.Length : 1;
                // For each new bone
                for (int cb_i = 0; cb_i < boneCount; cb_i++) {
                    var bone = comb.SMR ? comb.SMR.bones[cb_i] : comb.Bone;
                    var bind = comb.Mesh.bindposes[cb_i];

                    // Check if the bone is already mapped
                    var rb_i = rBone.FindIndex(t => t == bone);
                    if (rb_i < 0) {
                        rb_i = rBone.Count;
                        rBone.Add(bone);
                        rBinds.Add(bind);
                    } else {
                        // Bone already exists, calculate transforms to fit new bone
                        //boneAdjust[cb_i] = comb.SMR ? rBinds[rb_i].inverse * bind : bind;
                        boneAdjust[cb_i] = rBinds[rb_i].inverse * bind;
                    }

                    // Map the combining bone index to the resulting bone index
                    boneRemap[cb_i] = rb_i;
                }

                if (rInds.Count <= currentSubmeshCount) {
                    rInds.Add(new List<int>());
                    indexMap.Add(new int[comb.Mesh.vertexCount]);
                }

                var rInd = rInds[currentSubmeshCount];
                var tempIndices = comb.Mesh.GetIndices(comb.SubMeshIndex);

                // Store a copy of mesh data
                var m = comb.Mesh;
                m.GetVertices(tVerts);
                m.GetUVs(0, tUv0s);
                m.GetNormals(tNorms);
                m.GetTangents(tTangs);
                m.GetColors(tCols);
                m.GetBoneWeights(tBoneW);

                var tempAssign = indexMap[currentSubmeshCount];
                foreach (var ind in tempIndices) {
                    // if ind not assigned
                    if (tempAssign[ind] == 0) {
                        rInd.Add(currentIndiceCount);
                        rVerts.Add(
                            AdjustForBone(tVerts[ind], tBoneW[ind], boneAdjust));
                        rNorms.Add(
                            AdjustForBone(tNorms[ind], tBoneW[ind], boneAdjust));
                        rTangs.Add(
                            AdjustForBone(tTangs[ind], tBoneW[ind], boneAdjust));
                        rUv0s.Add(tUv0s[ind]);
                        rCols.Add(tCols.Count > 0 ? tCols[ind] : default);
                        rBoneW.Add(RemapBoneWeight(tBoneW[ind], idx => boneRemap[idx]));

                        tempAssign[ind] = currentIndiceCount;
                        currentIndiceCount += 1;
                    } else {
                        rInd.Add(tempAssign[ind]);
                    }
                }
                currentSubmeshCount += 1;
            }

            var resultMesh = new Mesh {
                subMeshCount = currentSubmeshCount,
                vertices     = rVerts.ToArray(),
                uv           = rUv0s  .ToArray(),
                normals      = rNorms.ToArray(),
                tangents     = rTangs.ToArray(),
                colors       = rCols .ToArray(),
                boneWeights  = rBoneW.ToArray(),
                bindposes    = rBinds.ToArray(),
            };

            for (var i = 0; i < currentSubmeshCount; i++)
                resultMesh.SetTriangles(rInds[i], i, false);

            TransferBlendShapes(combine, resultMesh, indexMap);

            resultMesh.RecalculateBounds();

            result.sharedMaterials = combine.Select(c => c.Material).ToArray();
            result.sharedMesh = resultMesh;
            result.bones = rBone.ToArray();

            foreach (var ind in rInds)
                ind.Clear();
        }

        private static BoneWeight RemapBoneWeight(BoneWeight w, Func<int, int> map) {
            if (w.boneIndex0 >= 0)
                w.boneIndex0 = map(w.boneIndex0);

            if (w.boneIndex1 >= 0)
                w.boneIndex1 = map(w.boneIndex1);

            if (w.boneIndex2 >= 0)
                w.boneIndex2 = map(w.boneIndex2);

            if (w.boneIndex3 >= 0)
                w.boneIndex3 = map(w.boneIndex3);

            return w;
        }

        private static BoneWeight FixBoneWeight(BoneWeight w) {
            for (int i = 0; i < 3; i++) {
                for (int j = i + 1; j < 3; j++) {
                    if (w.GetIndex(i) == w.GetIndex(j)) {
                        w.SetIndex(j, -1);
                        w.SetWeight(j, 0);
                        w.SetWeight(i, w.GetWeigth(i) + w.GetWeigth(j));
                    }
                }
            }
            return w;
        }

        private static Vector3 AdjustForBone(Vector3 v, BoneWeight w, IDictionary<int, Matrix4x4> m, bool asVector = false) {
            var res = Vector3.zero;

            bool hasAdjusted = false;
            for (int i = 0; i < 4; i++) {
                var idx = w.GetIndex(i);
                var weight = w.GetWeigth(i);
                if (idx <  0 || weight <= 0)
                    continue;

                if (m.TryGetValue(idx, out Matrix4x4 m_idx)) {
                    res += weight * (asVector ? 
                        m_idx.MultiplyVector(v) : 
                        m_idx.MultiplyPoint(v));
                    hasAdjusted = true;
                } else {
                    res += weight * v;
                }
            }
            // if not adjusted return original to perserve accuracy
            return hasAdjusted ? res : v;
        }

        class BlendShape
        {
            public Dictionary<float, BlendShapeFrame> Frames = new Dictionary<float, BlendShapeFrame>();
        }

        class BlendShapeFrame
        {
            public BlendShapeFrame(int size) {
                dVert = new Vector3[size];
                dNorm = new Vector3[size];
                dTang = new Vector3[size];
            }

            public Vector3[] dVert;
            public Vector3[] dNorm;
            public Vector3[] dTang;
        }

        private static void TransferBlendShapes(IList<SkinnedMeshInstance> input, Mesh output, IList<int[]> indexMap) {
            var blendShapes = new Dictionary<string, BlendShape>();

            // For each combine instance
            for (int i = 0; i < indexMap.Count; i++) {
                var map  = indexMap[i];
                var mesh = input[i].Mesh;

                var dVert = new Vector3[mesh.vertexCount];
                var dNorm = new Vector3[mesh.vertexCount];
                var dTang = new Vector3[mesh.vertexCount];

                // for each blendshape
                for (int s_i = 0; s_i < mesh.blendShapeCount; s_i++) {
                    var name = mesh.GetBlendShapeName(s_i);
                    var frameCount = mesh.GetBlendShapeFrameCount(s_i);

                    if (!blendShapes.ContainsKey(name)) {
                        blendShapes[name] = new BlendShape();
                    }
                    var blendShape = blendShapes[name];

                    // For each frame
                    for (int f_i = 0; f_i < frameCount; f_i++) {
                        mesh.GetBlendShapeFrameVertices(s_i, f_i, dVert, dNorm, dTang);

                        var weight = mesh.GetBlendShapeFrameWeight(s_i, f_i);
                        if (!blendShape.Frames.ContainsKey(weight)) {
                            blendShape.Frames[weight] = new BlendShapeFrame(output.vertexCount);
                        }
                        var frame = blendShape.Frames[weight];

                        // For each delta in frame, copy to resulting delta
                        for (int j = 0; j < indexMap[i].Length; j++) {
                            frame.dVert[indexMap[i][j]] = dVert[j];
                            frame.dNorm[indexMap[i][j]] = dNorm[j];
                            frame.dTang[indexMap[i][j]] = dTang[j];
                        }

                    }
                }
            }

            // Apply frames to mesh
            foreach (var bs in blendShapes) {
                var name  = bs.Key;
                var shape = bs.Value;

                foreach (var f in shape.Frames.OrderBy(kvp => kvp.Key)) {
                    var weight = f.Key;
                    var frame  = f.Value;

                    output.AddBlendShapeFrame(name, weight, frame.dVert, frame.dNorm, frame.dTang);
                }
            }
        }

        public class BoneMergeBuilder
        {
            public BoneMergeBuilder(SkinnedMeshRenderer SMR) {
                bones = SMR.bones.ToList();
                weights = SMR.sharedMesh.boneWeights;
                binds = SMR.sharedMesh.bindposes.ToList();
                smr = SMR;
            }

            readonly SkinnedMeshRenderer smr;
            readonly List<Transform> bones;
            readonly List<Matrix4x4> binds;
            readonly BoneWeight[] weights;

            public BoneMergeBuilder Merge(Transform from, Transform to) {

                var toIdx = bones.FindIndex(b => b == to);

                if (toIdx < 0) {
                    Debug.LogWarning($"Could not find bone {to.name}");
                    return this;
                }

                var fromIdx = bones.FindIndex(b => b == from);

                if (fromIdx < 0) {
                    Debug.LogWarning($"Could not find bone {from.name}");
                    return this;
                }

                for (int i = 0; i < weights.Length; i++) {
                    weights[i] = FixBoneWeight(
                        RemapBoneWeight(weights[i], idx 
                            => MergeBoneWeightIndex(idx, fromIdx, toIdx)
                        )
                    );
                }

                bones.RemoveAt(fromIdx);
                binds.RemoveAt(fromIdx);

                return this;
            }

            public void Apply() {
                smr.sharedMesh.boneWeights = weights;
                smr.sharedMesh.bindposes = binds.ToArray();
                smr.bones = bones.ToArray();
            }
        }

        private static int MergeBoneWeightIndex(int idx, int from, int to)
            => idx == from ? to : (idx > from ? idx - 1 : idx);
    }
}
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

            var currentIndiceCount = 0;
            var currentSubmeshCount = 0;
            for (var i = 0; i < combine.Count; i++) {
                var comb = combine[i];

                // The mapping of old bone indices to new bone indices
                var boneRemap = new Dictionary<int, int>();

                // For each new bone
                for (int cb_i = 0; cb_i < comb.SMR.bones.Length; cb_i++) {
                    var bone = comb.SMR.bones[cb_i];
                    var bind = comb.Mesh.bindposes[cb_i];

                    // Check if the bone is already mapped
                    var rb_i = rBone.FindIndex(t => t == bone);
                    if (rb_i < 0) {
                        rb_i = rBone.Count;
                        rBone.Add(bone);
                        rBinds.Add(bind);
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
                        rVerts.Add(comb.Transform * tVerts[ind]);
                        rUv0s.Add(tUv0s[ind]);
                        rNorms.Add(tNorms[ind]);
                        rTangs.Add(tTangs[ind]);
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

            CopyBlendShapes(combine.First().Mesh, resultMesh, indexMap);
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

        private static void CopyBlendShapes(Mesh input, Mesh output, IList<int[]> indexMap) {
            if (input.blendShapeCount <= 0)
                return;

            var inputDeltaVertices = new Vector3[input.vertexCount];
            var inputDeltaNormals  = new Vector3[input.vertexCount];
            var inputDeltaTangents = new Vector3[input.vertexCount];
            var deltaVertices = new Vector3[output.vertexCount];
            var deltaNormals  = new Vector3[output.vertexCount];
            var deltaTangents = new Vector3[output.vertexCount];

            for (var i = 0; i < input.blendShapeCount; i++) {
                var frameCount = input.GetBlendShapeFrameCount(i);
                for (var frameIndex = 0; frameIndex < frameCount; frameIndex++) {
                    var name = input.GetBlendShapeName(i);
                    var weight = input.GetBlendShapeFrameWeight(i, frameIndex);

                    input.GetBlendShapeFrameVertices(i, frameIndex, inputDeltaVertices, inputDeltaNormals, inputDeltaTangents);

                    var inputLength = Math.Min(inputDeltaVertices.Length, deltaVertices.Length);
                    for (var j = 0; j < inputLength; j += 1) {
                        deltaVertices[indexMap[0][j]] = inputDeltaVertices[j];
                        deltaNormals[indexMap[0][j]] = inputDeltaNormals[j];
                        deltaTangents[indexMap[0][j]] = inputDeltaTangents[j];
                    }

                    output.AddBlendShapeFrame(name, weight, deltaVertices, deltaNormals, deltaTangents);
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
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Extensions
{
    public static Transform AsProxy(this Transform o, Transform proxyRoot, Transform targetRoot) {
        var path = AnimationUtility.CalculateTransformPath(o, proxyRoot);


        var source = targetRoot.Find(path);
        if (!source)
            Debug.LogWarning($"Could not find similar path {path} in {targetRoot.name} (derived from {proxyRoot.name})");
        return source;
    }

    public static GameObject AsProxy(this GameObject o, GameObject proxyRoot, GameObject targetRoot) {
        var result = o.transform.AsProxy(proxyRoot.transform, targetRoot.transform);
        return result != null ? result.gameObject : null;
    }

    public static IEnumerable<GameObject> InThisAndParents(this GameObject o) {
        while (o != null) {
            yield return o;
            o = o.transform.parent.gameObject;
        }
    }

    public static int GetIndex(this BoneWeight w, int n) {
        switch (n) {
            case 0:
                return w.boneIndex0;
            case 1:
                return w.boneIndex1;
            case 2:
                return w.boneIndex2;
            default:
                return w.boneIndex3;
        }
    }

    public static void SetIndex(this BoneWeight w, int n, int idx) {
        switch (n) {
            case 0:
                w.boneIndex0 = idx;
                return;
            case 1:
                w.boneIndex1 = idx;
                return;
            case 2:
                w.boneIndex2 = idx;
                return;
            default:
                w.boneIndex3 = idx;
                return;
        }
    }

    public static float GetWeigth(this BoneWeight w, int n) {
        switch (n) {
            case 0:
                return w.weight0;
            case 1:
                return w.weight1;
            case 2:
                return w.weight2;
            default:
                return w.weight3;
        }
    }
    public static void SetWeight(this BoneWeight w, int n, float weight) {
        switch (n) {
            case 0:
                w.weight0 = weight;
                return;
            case 1:
                w.weight1 = weight;
                return;
            case 2:
                w.weight2 = weight;
                return;
            default:
                w.weight3 = weight;
                return;
        }
    }
}

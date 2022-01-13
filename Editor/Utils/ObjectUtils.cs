using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ObjectUtils
{
    public static IEnumerable<GameObject> InThisAndParents(this GameObject o) {
        while (o != null) {
            yield return o;
            o = o.transform.parent.gameObject;
        }
    }
}

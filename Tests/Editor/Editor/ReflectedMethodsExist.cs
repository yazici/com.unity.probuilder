using UnityEngine;
using UnityEditor;
using NUnit.Framework;
using System;
using System.Reflection;
using UnityEditor.ProBuilder;

static class ReflectedMethodsExist
{
    const BindingFlags k_BindingFlagsAll = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

#if !UNITY_2019_1_OR_NEWER
    [Test]
    public static void OnPreSceneGUIDelegate()
    {
        var fi = typeof(SceneView).GetField("onPreSceneGUIDelegate", k_BindingFlagsAll);
        Assert.IsNotNull(fi);
    }

#endif

#if !UNITY_2018_2_OR_NEWER
    [Test]
    public static void ResetOnSceneGUIState()
    {
        // no longer necessary as of 2018.2
        var mi = typeof(SceneView).GetMethod("ResetOnSceneGUIState", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(mi);
    }

#endif

#if !UNITY_2019_1_OR_NEWER
    [Test]
    public static void ShowWindowPopupWithMode()
    {
        var mi = typeof(EditorWindow).GetMethod(
            "ShowPopupWithMode",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(mi);
    }

#endif

    [Test]
    public static void ApplyWireMaterial()
    {
        var m_ApplyWireMaterial = typeof(UnityEditor.HandleUtility).GetMethod(
                "ApplyWireMaterial",
                BindingFlags.Static | BindingFlags.NonPublic,
                null,
                new System.Type[] { typeof(UnityEngine.Rendering.CompareFunction) },
                null);
        Assert.IsNotNull(m_ApplyWireMaterial);
    }

#if UNITY_2018_2_OR_NEWER
    [Test]
    public static void GetDefaultMaterial()
    {
        var mi = typeof(Material).GetMethod("GetDefaultMaterial", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(mi);
    }
#endif

    [Test]
    public static void SelectionRenderState_MatchesUnitySettings()
    {
        Assert.AreEqual((int)SelectionRenderState.None, (int)EditorSelectedRenderState.Hidden);
        Assert.AreEqual((int)SelectionRenderState.Wireframe, (int)EditorSelectedRenderState.Wireframe);
        Assert.AreEqual((int)SelectionRenderState.Outline, (int)EditorSelectedRenderState.Highlight);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using VirtualVoid.Networking.Steam;

[CustomEditor(typeof(NetworkTransform))]
public class NetworkTransformEditor : Editor
{
    private NetworkTransform networkTransform;
    private Editor settingsEditor;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (networkTransform == null)
            networkTransform = (NetworkTransform)target;

        //EditorGUILayout.LabelField("NetTransform stuff ", "data here");

        if (networkTransform.settings != null)
            DrawEditor(networkTransform.settings, ref networkTransform.settings.foldout, ref settingsEditor);
    }

    private void OnSceneGUI()
    {
        if (networkTransform == null) networkTransform = (NetworkTransform)target;

        if (networkTransform.settings == null) return;

        NetworkTransformSettings settings = networkTransform.settings;

        // Position
        if (settings.position.sync && settings.position.quantize && settings.position.visualizeQuantization)
        {
            Handles.color = Color.green;

            Vector3 center = Vector3.Lerp(settings.position.quantizationRangeMin, settings.position.quantizationRangeMax, 0.5f);
            Bounds bounds = new Bounds();
            bounds.SetMinMax(settings.position.quantizationRangeMin, settings.position.quantizationRangeMax);
            Handles.DrawWireCube(center, bounds.size);
        }

        // Scale
        if (settings.scale.sync && settings.scale.quantize && settings.scale.visualizeQuantization)
        {
            Vector3 center = networkTransform.transform.position;
            Vector3 smallestSize = settings.scale.quantizationSizeMin;
            Vector3 largestSize = settings.scale.quantizationSizeMax;
            Vector3 currentSize = networkTransform.transform.localScale;

            Handles.color = Color.red;
            Handles.DrawWireCube(center, smallestSize);

            Handles.color = Color.green;
            Handles.DrawWireCube(center, largestSize);

            Handles.color = Color.yellow;
            Handles.DrawWireCube(center, currentSize);
        }
    }

    private void DrawEditor(Object targetObj, ref bool foldout, ref Editor editor)
    {
        //foldout = EditorGUILayout.Foldout(foldout, "Settings");
        foldout = EditorGUILayout.InspectorTitlebar(foldout, targetObj);

        if (foldout)
        {
            CreateCachedEditor(targetObj, null, ref editor);
            //editor = CreateEditor(targetObj);
            editor.OnInspectorGUI();
        }
    }

    private void OnEnable()
    {
        networkTransform = (NetworkTransform)target;
    }
}

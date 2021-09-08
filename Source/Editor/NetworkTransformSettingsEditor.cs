using UnityEngine;
using UnityEditor;
using VirtualVoid.Networking.Steam;

[CustomEditor(typeof(NetworkTransformSettings))]
public class NetworkTransformSettingsEditor : Editor
{
    NetworkTransformSettings settings;

    public override void OnInspectorGUI()
    {
        //DrawDefaultInspector();
        //DrawEditor(settings.position, ref settings.posFold, ref posEditor, "Position", DrawPositionEditor);
        DrawPositionEditor();
        EditorGUILayout.Space();

        DrawRotationEditor();
        EditorGUILayout.Space();

        DrawScaleEditor();

        //EditorGUILayout.LabelField("Quantization data blah blah blah: ", "data here");
    }

    private void DrawEditor(Object targetObj, ref bool foldout, ref Editor editor, string foldoutTitle, System.Action<Editor> drawEditorAction)
    {
        foldout = EditorGUILayout.Foldout(foldout, foldoutTitle);
        EditorGUILayout.Foldout(true, "Title");
        //foldout = EditorGUILayout.InspectorTitlebar(foldout, targetObj);

        if (foldout)
        {
            CreateCachedEditor(targetObj, null, ref editor);
            //editor = CreateEditor(targetObj);
            editor.OnInspectorGUI();
        }
    }

    private void DrawPositionEditor()//Editor editor)
    {
        EditorGUI.indentLevel = 2;
        settings.posFold = EditorGUILayout.Foldout(settings.posFold, "Position");

        if (settings.posFold)
        {
            settings.position.sync = EditorGUILayout.Toggle("Sync", settings.position.sync);

            if (settings.position.sync)
            {
                settings.position.sensitivity = EditorGUILayout.FloatField("Sensitivity", settings.position.sensitivity);
                if (settings.position.sensitivity < 0.01f) settings.position.sensitivity = 0.01f;

                settings.position.interpolate = EditorGUILayout.Toggle("Interpolate", settings.position.interpolate);
                settings.position.useGlobal = EditorGUILayout.Toggle("Use Global", settings.position.useGlobal);

                settings.position.quantize = EditorGUILayout.Toggle("Quantize", settings.position.quantize);

                if (settings.position.quantize)
                {
                    EditorGUI.indentLevel = 3;
                    settings.position.quantizationPrecision = (NetworkTransformSettings.BitPrecision)EditorGUILayout.EnumPopup("Precision (bits)", settings.position.quantizationPrecision);

                    int steps = (int)Mathf.Pow(2, (int)settings.position.quantizationPrecision);
                    EditorGUILayout.LabelField("Steps: ", $"{steps}");
                    EditorGUILayout.Space();

                    settings.position.quantizationRangeMin = EditorGUILayout.Vector3IntField("Lower Bound", settings.position.quantizationRangeMin);
                    settings.position.quantizationRangeMax = EditorGUILayout.Vector3IntField("Upper Bound", settings.position.quantizationRangeMax);
                    settings.position.visualizeQuantization = EditorGUILayout.Toggle("Visualize", settings.position.visualizeQuantization);

                    Vector3 max = settings.position.quantizationRangeMax;
                    Vector3 min = settings.position.quantizationRangeMin;

                    float xPrecision = (max.x - min.x) / steps;
                    float yPrecision = (max.y - min.y) / steps;
                    float zPrecision = (max.z - min.z) / steps;
                    
                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Precision", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"- X: {xPrecision} units");
                    EditorGUILayout.LabelField($"- Y: {yPrecision} units");
                    EditorGUILayout.LabelField($"- Z: {zPrecision} units");
                    //EditorGUILayout.LabelField("Max X: ", $"{max.x - min.x} units");
                    // Max steps for bytes: 2 ^ numBits
                }
            }
        }
    }

    private void DrawRotationEditor()
    {
        EditorGUI.indentLevel = 2;
        settings.rotFold = EditorGUILayout.Foldout(settings.rotFold, "Rotation");

        if (settings.rotFold)
        {
            settings.rotation.sync = EditorGUILayout.Toggle("Sync", settings.rotation.sync);

            if (settings.rotation.sync)
            {
                settings.rotation.sensitivity = EditorGUILayout.FloatField("Sensitivity", settings.rotation.sensitivity);
                if (settings.rotation.sensitivity < 0.01f) settings.rotation.sensitivity = 0.01f;

                settings.rotation.interpolate = EditorGUILayout.Toggle("Interpolate", settings.rotation.interpolate);
                settings.rotation.useGlobal = EditorGUILayout.Toggle("Use Global", settings.rotation.useGlobal);
            }
        }
    }

    private void DrawScaleEditor()
    {
        EditorGUI.indentLevel = 2;
        settings.scaleFold = EditorGUILayout.Foldout(settings.scaleFold, "Scale");

        if (settings.scaleFold)
        {
            settings.scale.sync = EditorGUILayout.Toggle("Sync", settings.scale.sync);

            if (settings.scale.sync)
            {
                settings.scale.sensitivity = EditorGUILayout.FloatField("Sensitivity", settings.scale.sensitivity);
                if (settings.scale.sensitivity < 0.01f) settings.scale.sensitivity = 0.01f;

                settings.scale.interpolate = EditorGUILayout.Toggle("Interpolate", settings.scale.interpolate);

                settings.scale.quantize = EditorGUILayout.Toggle("Quantize", settings.scale.quantize);

                if (settings.scale.quantize)
                {
                    EditorGUI.indentLevel = 3;
                    settings.scale.quantizationPrecision = (NetworkTransformSettings.BitPrecision)EditorGUILayout.EnumPopup("Precision", settings.scale.quantizationPrecision);

                    int steps = (int)Mathf.Pow(2, (int)settings.scale.quantizationPrecision);
                    EditorGUILayout.LabelField("Steps: ", $"{steps}");
                    EditorGUILayout.Space();

                    settings.scale.quantizationSizeMin = EditorGUILayout.Vector3Field("Smallest scale", settings.scale.quantizationSizeMin);
                    settings.scale.quantizationSizeMax = EditorGUILayout.Vector3Field("Largest scale", settings.scale.quantizationSizeMax);
                    settings.scale.visualizeQuantization = EditorGUILayout.Toggle("Visualize", settings.scale.visualizeQuantization);

                    Vector3 max = settings.scale.quantizationSizeMax;
                    Vector3 min = settings.scale.quantizationSizeMin;

                    float xPrecision = (max.x - min.x) / steps;
                    float yPrecision = (max.y - min.y) / steps;
                    float zPrecision = (max.z - min.z) / steps;

                    EditorGUILayout.Space();

                    EditorGUILayout.LabelField("Precision", EditorStyles.boldLabel);
                    EditorGUILayout.LabelField($"- X: {xPrecision} units");
                    EditorGUILayout.LabelField($"- Y: {yPrecision} units");
                    EditorGUILayout.LabelField($"- Z: {zPrecision} units");
                }
            }
        }
    }

    //private void OnSceneGUI()
    //{
    //    if (this.settings == null)
    //    {
    //        if (target is NetworkTransformSettings settings)
    //            this.settings = settings;
    //        else
    //            return;
    //    }
    //
    //    if (this.settings.position.sync && this.settings.position.quantize && this.settings.position.visualizeQuantization)
    //    {
    //        Handles.color = Color.green;
    //
    //        Vector3 center = Vector3.Lerp(this.settings.position.quantizationRangeMin, this.settings.position.quantizationRangeMax, 0.5f);
    //        Bounds bounds = new Bounds();
    //        bounds.SetMinMax(this.settings.position.quantizationRangeMin, this.settings.position.quantizationRangeMax);
    //        Handles.DrawWireCube(center, bounds.size);
    //    }
    //}

    private void OnEnable()
    {
        if (target is NetworkTransformSettings settings)
            this.settings = settings;
    }
}

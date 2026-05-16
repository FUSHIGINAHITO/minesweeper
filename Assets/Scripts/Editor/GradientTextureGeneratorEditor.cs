using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GradientTextureGenerator))]
public class GradientTextureGeneratorEditor : Editor
{
    private const int PreviewSize = 256;
    private Texture2D previewTexture;
    private bool pendingPreviewRefresh = true;

    private void OnDisable()
    {
        ReleasePreview();
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        DrawDefaultInspector();
        bool changed = EditorGUI.EndChangeCheck();

        GUILayout.Space(8f);

        if (GUILayout.Button("刷新预览"))
        {
            pendingPreviewRefresh = true;
        }

        if (changed || previewTexture == null)
        {
            pendingPreviewRefresh = true;
        }

        if (pendingPreviewRefresh && Event.current.type == EventType.Repaint)
        {
            RegeneratePreview();
            pendingPreviewRefresh = false;
        }

        DrawPreviewArea();

        GUILayout.Space(8f);
        if (GUILayout.Button("生成渐变 PNG"))
        {
            ((GradientTextureGenerator)target).GenerateGradientPng();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void RegeneratePreview()
    {
        ReleasePreview();

        GradientTextureGenerator generator = (GradientTextureGenerator)target;
        previewTexture = generator.BuildGradientTexture(PreviewSize, true);
        previewTexture.hideFlags = HideFlags.HideAndDontSave;
        Repaint();
    }

    private void DrawPreviewArea()
    {
        EditorGUILayout.LabelField("预览", EditorStyles.boldLabel);

        Rect rect = GUILayoutUtility.GetAspectRect(1f, GUILayout.MaxWidth(256f));
        if (previewTexture != null)
        {
            EditorGUI.DrawPreviewTexture(rect, previewTexture, null, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUI.DrawRect(rect, new Color(0.15f, 0.15f, 0.15f, 1f));
            EditorGUI.DropShadowLabel(rect, "无预览");
        }
    }

    private void ReleasePreview()
    {
        if (previewTexture == null)
        {
            return;
        }

        DestroyImmediate(previewTexture);
        previewTexture = null;
    }
}
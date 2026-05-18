using UnityEngine;

[ExecuteAlways]
public class SetGlobalLightDir : MonoBehaviour
{
    public Vector2 dir;

    private void Update()
    {
        Vector2 dir2 = new Vector2(dir.x, dir.y).normalized;
        Shader.SetGlobalVector("_LightDirWS", new Vector4(dir2.x, dir2.y, 0f, 0f));
    }
}

public class TextPool : Pool<TextHandler>
{
    protected override void OnRequire(TextHandler item)
    {
        item.text.enabled = true;
    }

    protected override void OnReturn(TextHandler item)
    {
        item.text.enabled = false;
    }
}
public class ImagePool : Pool<ImageHandler>
{
    protected override void OnRequire(ImageHandler item)
    {
        item.image.enabled = true;
    }

    protected override void OnReturn(ImageHandler item)
    {
        item.image.enabled = false;
    }
}
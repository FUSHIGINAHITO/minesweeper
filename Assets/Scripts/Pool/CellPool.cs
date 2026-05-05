public class CellPool : Pool<Cell>
{
    protected override void OnRequire(Cell item)
    {
        item.image.enabled = true;
    }

    protected override void OnReturn(Cell item)
    {
        item.image.enabled = false;
    }
}
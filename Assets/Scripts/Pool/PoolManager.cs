using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager instance => _instance;
    private static PoolManager _instance;

    public TextPool textPool;
    public CellPool cellPool;

    [SerializeField] private MainDataSO mainDataSO;

    private void Awake()
    {
        _instance = this;
        Cell.mainDataSO = mainDataSO;
    }

    public Cell RequireCell(CellShapeType cellShapeType, Vector3 pos, Quaternion rot, float scale, bool isBorder = false, int typeId = -1)
    {
        var cell = cellPool.Require();
        cell.Init(cellShapeType, pos, rot, scale, isBorder, typeId);
        return cell;
    }
}
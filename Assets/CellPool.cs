using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 简单的 Cell 对象池（针对 Unity GameObject），不进行过多资源释放，仅重用实例。
/// 使用方式：在 Inspector 中把 Cell 预制体拖到 `cellPrefab`，调用 `Rent()` 获取一个实例，用完后调用 `Return(obj)` 归还。
/// Inspector 中会显示运行时可用数量（可编辑，但由代码同步）。
/// </summary>
public class CellPool : MonoBehaviour
{
    [Header("Pool Settings")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private int initialSize = 100;
    [SerializeField] private bool expandIfNeeded = true;

    [Header("Debug")]
    [SerializeField] private int availableCount;
    [SerializeField] private int totalCount;

    private readonly Queue<Cell> pool = new();

    private void Awake()
    {
        Preload(initialSize);
    }

    /// <summary>
    /// 预先创建若干实例并置为 inactive。
    /// </summary>
    public void Preload(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var cell = CreateInstance();
            cell.gameObject.SetActive(false);
            pool.Enqueue(cell);
        }

        SyncAvailableCount();
    }

    /// <summary>
    /// 从池中获取一个实例（激活）。当池为空且允许扩展时会新建实例。
    /// 返回 null 表示无法提供（未设置 prefab 且无法扩展）。
    /// </summary>
    public Cell Require()
    {
        if (cellPrefab == null)
        {
            Debug.LogError($"{nameof(CellPool)}: 无法 Rent，因为 cellPrefab 未设置。");
            return null;
        }

        Cell cell;
        if (pool.Count > 0)
        {
            cell = pool.Dequeue();
            cell.gameObject.SetActive(true);
        }
        else if (expandIfNeeded)
        {
            cell = CreateInstance();
        }
        else
        {
            return null;
        }

        SyncAvailableCount();
        return cell;
    }

    private Cell CreateInstance()
    {
        var go = Instantiate(cellPrefab, transform);
        var cell = go.GetComponent<Cell>();
        cell.pool = this;
        totalCount++;
        return cell;
    }

    /// <summary>
    /// 将实例归还到池中：置为 inactive 并移动到指定父物体下以便组织场景层级。
    /// 不执行 Destroy，从而避免频繁分配/释放开销。
    /// </summary>
    public void Return(Cell cell)
    {
        cell.gameObject.SetActive(false);
        cell.transform.SetParent(transform, false);
        pool.Enqueue(cell);
        SyncAvailableCount();
    }

    /// <summary>
    /// 池当前可用数量（不含已 rent 出的实例）
    /// </summary>
    public int AvailableCount => pool.Count;

    private void SyncAvailableCount()
    {
        availableCount = pool.Count;
    }
}
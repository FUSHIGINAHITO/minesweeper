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
    [SerializeField] private Transform parentWhenInactive;

    [Header("Debug")]
    [Tooltip("池中当前可用实例数量（运行时由代码更新）")]
    [SerializeField] private int availableCount;

    private readonly Queue<GameObject> pool = new Queue<GameObject>();

    private void Awake()
    {
        if (parentWhenInactive == null)
            parentWhenInactive = transform;

        if (cellPrefab == null)
        {
            Debug.LogError($"{nameof(CellPool)}: cellPrefab 未设置。");
            availableCount = 0;
            return;
        }

        Preload(initialSize);
    }

    /// <summary>
    /// 预先创建若干实例并置为 inactive。
    /// </summary>
    public void Preload(int count)
    {
        for (int i = 0; i < count; i++)
        {
            var go = Instantiate(cellPrefab, parentWhenInactive);
            go.SetActive(false);
            pool.Enqueue(go);
        }

        SyncAvailableCount();
    }

    /// <summary>
    /// 从池中获取一个实例（激活）。当池为空且允许扩展时会新建实例。
    /// 返回 null 表示无法提供（未设置 prefab 且无法扩展）。
    /// </summary>
    public GameObject Require()
    {
        if (cellPrefab == null)
        {
            Debug.LogError($"{nameof(CellPool)}: 无法 Rent，因为 cellPrefab 未设置。");
            return null;
        }

        GameObject obj;
        if (pool.Count > 0)
        {
            obj = pool.Dequeue();
            obj.SetActive(true);
        }
        else if (expandIfNeeded)
        {
            obj = Instantiate(cellPrefab, parentWhenInactive);
            obj.SetActive(true);
        }
        else
        {
            return null;
        }

        SyncAvailableCount();
        return obj;
    }

    /// <summary>
    /// 将实例归还到池中：置为 inactive 并移动到指定父物体下以便组织场景层级。
    /// 不执行 Destroy，从而避免频繁分配/释放开销。
    /// </summary>
    public void Return(GameObject obj)
    {
        if (obj == null)
            return;

        obj.SetActive(false);
        obj.transform.SetParent(parentWhenInactive, false);
        pool.Enqueue(obj);
        SyncAvailableCount();
    }

    /// <summary>
    /// 可选：清空池（不销毁实例，仅清空队列并将现有子对象置为 inactive 并归位）
    /// 若需要真正释放资源，可在此处调用 Destroy，但当前实现保持实例以便重用。
    /// </summary>
    public void ReturnAllActiveChildren()
    {
        // 将 parent 下的所有子对象（除未归还的池内对象）归入池，避免额外销毁
        for (int i = parentWhenInactive.childCount - 1; i >= 0; i--)
        {
            var child = parentWhenInactive.GetChild(i).gameObject;
            if (child.activeSelf)
            {
                child.SetActive(false);
                pool.Enqueue(child);
            }
        }

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
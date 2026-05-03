using System;
using System.Collections.Generic;
using UnityEngine;

public class Pool<T> : MonoBehaviour where T : Pool<T>.PoolObj
{
    public class PoolObj : MonoBehaviour
    {
        [NonSerialized, HideInInspector]
        public Pool<T> pool;

        public void Return()
        {
            pool.Return((T)this);
        }
    }

    [Header("Debug")]
    [SerializeField] private int availableCount;
    [SerializeField] private int totalCount;

    [Header("Pool Settings")]
    [SerializeField] private GameObject prefab;
    [SerializeField] private int initialSize = 100;

    private readonly Queue<T> pool = new();

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
    public T Require()
    {
        T item;
        if (pool.Count > 0)
        {
            item = pool.Dequeue();
            item.gameObject.SetActive(true);
        }
        else
        {
            item = CreateInstance();
        }

        SyncAvailableCount();
        return item;
    }

    private T CreateInstance()
    {
        var go = Instantiate(prefab, transform);
        var item = go.GetComponent<T>();
        item.pool = this;
        OnCreate(item);
        totalCount++;
        return item;
    }

    protected virtual void OnCreate(T item)
    {

    }

    /// <summary>
    /// 将实例归还到池中：置为 inactive 并移动到指定父物体下以便组织场景层级。
    /// 不执行 Destroy，从而避免频繁分配/释放开销。
    /// </summary>
    public void Return(T item)
    {
        item.gameObject.SetActive(false);
        pool.Enqueue(item);
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
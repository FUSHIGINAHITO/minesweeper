using System;
using System.Collections.Generic;
using UnityEngine;

public class Pool<T> : MonoBehaviour where T : Pool<T>.PoolObj
{
    public class PoolObj : MonoBehaviour
    {
        [NonSerialized, HideInInspector] public Pool<T> pool;

        public void Return()
        {
            pool.Return((T)this);
        }
    }

    [Header("Debug")]
    [SerializeField] private int availableCount;
    [SerializeField] private int totalCount;

    [Header("Pool Settings")]
    [SerializeField] private T prefab;
    [SerializeField] private int initialSize = 100;

    private readonly Queue<T> pool = new();
    private Transform trans;

    private void Awake()
    {
        trans = transform;
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
        }
        else
        {
            item = CreateInstance();
        }

        SyncAvailableCount();
        OnRequire(item);
        return item;
    }

    private T CreateInstance()
    {
        var item = Instantiate(prefab, trans);
        item.pool = this;
        OnReturn(item);

        totalCount++;
        return item;
    }

    protected virtual void OnRequire(T item)
    {

    }

    protected virtual void OnReturn(T item)
    {

    }

    /// <summary>
    /// 将实例归还到池中：置为 inactive 并移动到指定父物体下以便组织场景层级。
    /// 不执行 Destroy，从而避免频繁分配/释放开销。
    /// </summary>
    public void Return(T item)
    {
        pool.Enqueue(item);
        SyncAvailableCount();
        OnReturn(item);
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
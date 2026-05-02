using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager instance => _instance;
    private static PoolManager _instance;

    public CellPool triangle;
    public CellPool square;
    public CellPool hex;

    private void Awake()
    {
        _instance = this;
    }
}
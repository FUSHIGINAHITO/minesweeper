using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public int i;
    public int j;
    public int value;
    public List<Cell> neighbours = new();
    public bool isShown;
    public bool isMine;

    public bool isFlagged;

    public TMP_Text text;
    public SpriteRenderer image;

    private void Awake()
    {
        text.transform.rotation = Quaternion.identity;
    }
}
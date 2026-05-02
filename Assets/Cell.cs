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

    public void Init()
    {
        i = 0;
        j = 0;
        value = 0;
        neighbours.Clear();
        isShown = false;
        isMine = false;
        isFlagged = false;

        text.transform.rotation = Quaternion.identity;
        text.gameObject.SetActive(false);
    }
}
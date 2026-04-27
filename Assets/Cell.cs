using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
}
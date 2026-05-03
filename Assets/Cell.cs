using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Cell : CellPool.PoolObj
{
    [NonSerialized, HideInInspector]
    public int value;
    [NonSerialized, HideInInspector]
    public List<Cell> neighbours = new();
    [NonSerialized, HideInInspector]
    public bool isRevealed;
    [NonSerialized, HideInInspector]
    public bool isMine;
    [NonSerialized, HideInInspector]
    public bool isFlagged;

    public SpriteRenderer image;
    [NonSerialized, HideInInspector]
    public TextHandler text;

    public void Init(Vector3 pos, Quaternion rot, float scale)
    {
        value = 0;
        neighbours.Clear();
        isRevealed = false;
        isMine = false;
        isFlagged = false;
        image.color = Game.instance.so.defaultColor;

        transform.SetPositionAndRotation(pos, rot);
        transform.localScale = scale * Vector3.one;
    }

    // 返回所有未扫开的邻居
    public List<Cell> GetUnshownNeighbors()
    {
        var list = new List<Cell>();

        foreach (var n in neighbours)
        {
            if (!n.isRevealed)
            {
                list.Add(n);
            }
        }

        return list;
    }

    public void Reveal()
    {
        if (!isRevealed)
        {
            var so = Game.instance.so;
            isRevealed = true;
            image.color = isMine ? so.mineColor : so.revealedColor;

            if (!isMine && value > 0)
            {
                text = PoolManager.instance.text.Require();
                text.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
                text.text.text = value.ToString();
                text.text.color = so.colors[Mathf.Clamp(value, 0, so.colors.Length - 1)];
            }
        }
    }

    public void ToggleFlag()
    {
        isFlagged = !isFlagged;

        if (isFlagged)
        {
            Flag();
        }
        else
        {
            Unflag();
        }
    }

    public void Flag()
    {
        isFlagged = true;
        image.color = Game.instance.so.flagColor;
    }

    public void Unflag()
    {
        isFlagged = false;
        image.color = Game.instance.so.defaultColor;
    }

    public void Pressed()
    {
        image.color = Game.instance.so.pressedColor;
    }

    public void Restore()
    {
        var so = Game.instance.so;
        if (!isRevealed)
        {
            image.color = isFlagged ? so.flagColor : so.defaultColor;
        }
        else
        {
            image.color = so.revealedColor;
        }
    }

    public void Chord()
    {
        var so = Game.instance.so;
        if (!isRevealed)
        {
            image.color = isFlagged ? so.chordColorFlag : so.chordColor;
        }
        else
        {
            image.color = so.chordColorRevealed;
        }
    }
}
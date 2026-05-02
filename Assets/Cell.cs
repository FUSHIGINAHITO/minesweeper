using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public int value;
    public List<Cell> neighbours = new();
    public bool isRevealed;
    public bool isMine;
    public bool isFlagged;

    public TMP_Text text;
    public SpriteRenderer image;
    public CellPool pool;

    public void Init()
    {
        value = 0;
        neighbours.Clear();
        isRevealed = false;
        isMine = false;
        isFlagged = false;
        image.color = Game.instance.so.defaultColor;

        text.transform.rotation = Quaternion.identity;
        text.gameObject.SetActive(false);
    }

    public void Return()
    {
        pool.Return(this);
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
                text.gameObject.SetActive(true);
                text.text = value.ToString();
                text.color = so.colors[Mathf.Clamp(value, 0, so.colors.Length - 1)];
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
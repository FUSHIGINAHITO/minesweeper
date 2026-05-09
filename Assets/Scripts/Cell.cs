using System;
using System.Collections.Generic;
using UnityEngine;

public class Cell : CellPool.PoolObj
{
    [NonSerialized, HideInInspector] public int value;
    [NonSerialized, HideInInspector] public List<Cell> neighbours = new();
    [NonSerialized, HideInInspector] public bool isBorder;
    [NonSerialized, HideInInspector] public bool isRevealed;
    [NonSerialized, HideInInspector] public bool isMine;
    [NonSerialized, HideInInspector] public bool isFlagged;

    public SpriteRenderer image;
    [NonSerialized, HideInInspector] public TextHandler text;

    [NonSerialized, HideInInspector] public CellShapeType shapeType;
    [NonSerialized, HideInInspector] public Vector2[] cachedWorldVertices;
    [NonSerialized, HideInInspector] public Bounds cachedAabb;
    [NonSerialized, HideInInspector] public bool geometryDirty = true;

    // BuildNeighbours 临时使用，避免字典查索引
    [NonSerialized, HideInInspector] public int tempBuildIndex = -1;
    [NonSerialized, HideInInspector] public int typeId = -1;
    private Transform trans;

    [NonSerialized, HideInInspector] public Vector3 position;
    [NonSerialized, HideInInspector] public Quaternion rotation;
    [NonSerialized, HideInInspector] public float scale;

    public static MainDataSO mainDataSO;
    [NonSerialized, HideInInspector] public TileSO so;

    private void Awake()
    {
        trans = transform;
    }

    public void Init(CellShapeType cellShapeType, Vector3 pos, Quaternion rot, float s, bool _isBorder, int _typeId = -1)
    {
        shapeType = cellShapeType;
        typeId = _typeId;
        value = 0;
        neighbours.Clear();
        isRevealed = false;
        isMine = false;
        isFlagged = false;
        isBorder = _isBorder;

        geometryDirty = true;
        tempBuildIndex = -1;

        position = pos;
        rotation = rot;
        scale = s;

        trans.SetPositionAndRotation(position, rotation);
        trans.localScale = scale * Vector3.one;
        so = mainDataSO.GetTileSO(shapeType);
    }

    public void InitShowArt()
    {
        ShowRevealArt(false);
        image.color = isBorder ? mainDataSO.borderColor : mainDataSO.defaultColor;
    }

    public void ShowEditArt(Color color, int v)
    {
        ShowRevealArt(true);
        image.color = color;

        /*if (v > 0)
        {
            if (text is null)
            {
                text = PoolManager.instance.textPool.Require();
            }

            text.transform.SetPositionAndRotation(trans.position, Quaternion.identity);
            text.transform.localScale = 0.3f * Vector3.one;
            text.text.text = v.ToString();
            text.text.color = 0.2f * Color.black;
        }*/
    }

    public void GetUnshownNeighbors(List<Cell> res)
    {
        res.Clear();

        foreach (var n in neighbours)
        {
            if (!n.isRevealed)
            {
                res.Add(n);
            }
        }
    }

    public void Reveal(bool force = false)
    {
        if (!isRevealed || force)
        {
            isRevealed = true;
            image.color = isMine ? mainDataSO.mineColor : mainDataSO.revealedColor;

            ShowRevealArt(true);
            ShowText();
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
        image.color = mainDataSO.flagColor;
        Restore();
    }

    public void Unflag()
    {
        isFlagged = false;
        image.color = mainDataSO.defaultColor;
        Restore();
    }

    public void Pressed()
    {
        ShowRevealArt(true);
        image.color = mainDataSO.pressedColor;
    }

    public void Restore()
    {
        if (!isRevealed)
        {
            ShowRevealArt(false);
            image.color = isFlagged ? mainDataSO.flagColor : mainDataSO.defaultColor;
        }
        else
        {
            ShowRevealArt(true);
            image.color = mainDataSO.revealedColor;
        }
    }

    public void Chord()
    {
        if (!isRevealed)
        {
            ShowRevealArt(true);
            image.color = isFlagged ? mainDataSO.chordColorFlag : mainDataSO.chordColor;
        }
        else
        {
            ShowRevealArt(true);
            image.color = mainDataSO.chordColorRevealed;
        }
    }

    public void ReturnAll()
    {
        ReturnText();
        Return();
    }

    private void ShowRevealArt(bool v)
    {
        if (v)
        {
            image.sharedMaterial = mainDataSO.polygonRevealedMaterial;
            image.sprite = so.polygonShrinkSprite;
        }
        else
        {
            if (isBorder)
            {
                image.sharedMaterial = so.polygonBorderMaterialOverride;
            }
            else
            {
                image.sharedMaterial = so.polygonMaterialOverride;
            }
            
            image.sprite = so.polygonSprite;
        }
    }

    public void ShowColor()
    {
        image.sharedMaterial = so.polygonMaterialOverride;
        image.sprite = so.polygonSprite;
        image.color = Game.instance.map.cellColorList[typeId];

        ReturnText();
    }

    public void ShowAns()
    {
        image.color = isMine ? mainDataSO.mineColor : mainDataSO.revealedColor;
        Reveal(true);
    }

    private void ReturnText()
    {
        if (text is not null)
        {
            text.Return();
            text = null;
        }
    }

    private void ShowText()
    {
        if (!isMine && value > 0)
        {
            if (text is null)
            {
                text = PoolManager.instance.textPool.Require();
            }

            text.transform.SetPositionAndRotation(trans.position, Quaternion.identity);
            text.transform.localScale = Game.instance.map.textSize * Vector3.one;
            text.text.text = value.ToString();
            text.text.color = mainDataSO.colors[Mathf.Clamp(value, 0, mainDataSO.colors.Length - 1)];
        }
    }
}
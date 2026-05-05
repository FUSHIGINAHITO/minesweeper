using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "PolygonTileCatalog",
    menuName = "Minesweeper/Tiling Editor/Polygon Tile Catalog")]
public class PolygonTileCatalog : ScriptableObject
{
    [Serializable]
    public class PolygonTileDefinition
    {
        public enum GeometrySource
        {
            SharedRegularByShapeType = 0,
            Custom = 1
        }

        [Header("Identity")]
        public string id = "tile";

        [Header("Geometry")]
        public CellShapeType shapeType = CellShapeType.Square;
        public GeometrySource geometrySource = GeometrySource.SharedRegularByShapeType;
        public List<Vector2> customUnitVertices = new();

        public IReadOnlyList<Vector2> GetUnitLocalVertices()
        {
            return geometrySource == GeometrySource.SharedRegularByShapeType
                ? PoolManager.instance.GetSharedLocalVertices(shapeType)
                : customUnitVertices;
        }
    }

    [SerializeField] private List<PolygonTileDefinition> tiles = new();

    public int Count => tiles.Count;

    public PolygonTileDefinition Get(int index)
    {
        return tiles[index];
    }
}
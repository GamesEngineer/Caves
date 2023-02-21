using GameU;
using System;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class CaveMap : MonoBehaviour
{
    [SerializeField]
    protected CaveSystem caves;

    [SerializeField]
    protected Player player;

    [SerializeField, Range(1f, 100f)]
    protected float mapRadius = 10f;

    [SerializeField]
    protected Color playerColor = Color.white;

    public bool bigMap;

    private RawImage mapImage;
    private Texture2D mapTexture;
    private Vector2 smallMapSize;
    private Vector2 smallMapAnchorMin;
    private Vector2 smallMapAnchorMax;
    private Vector2 smallMapPivot;
    private Vector2 smallMapAnchorPos;

    private void Awake()
    {
        mapImage = GetComponent<RawImage>();
        smallMapSize = mapImage.rectTransform.sizeDelta;
        smallMapAnchorMin = mapImage.rectTransform.anchorMin;
        smallMapAnchorMax = mapImage.rectTransform.anchorMax;
        smallMapPivot = mapImage.rectTransform.pivot;
        smallMapAnchorPos = mapImage.rectTransform.anchoredPosition;
        mapImage.enabled = false;
        caves.OnCreated += Caves_OnCreated;
    }

    private void Caves_OnCreated()
    {
        for (int x = 0; x < mapTexture.width; x++)
        {
            for (int y = 0; y < mapTexture.height; y++)
            {
                mapTexture.SetPixel(x, y, Color.black / 2f);
            }
        }
        mapTexture.Apply();
        mapImage.enabled = true;
    }

    void Start()
    {
        mapTexture = new Texture2D(caves.GridSize.x, caves.GridSize.z);
        mapTexture.filterMode = FilterMode.Point;
        mapImage.texture = mapTexture;
    }

    void Update()
    {
        bigMap ^= Input.GetKeyDown(KeyCode.Space);
        
        if (bigMap)
        {
            mapImage.rectTransform.sizeDelta = new Vector2(1000, 1000);
            Vector2 half = new Vector2(0.5f, 0.5f);
            mapImage.rectTransform.anchorMin = half;
            mapImage.rectTransform.anchorMax = half;
            mapImage.rectTransform.pivot = half;
            mapImage.rectTransform.anchoredPosition = Vector2.zero;
        }
        else
        {
            mapImage.rectTransform.sizeDelta = smallMapSize;
            mapImage.rectTransform.anchorMin = smallMapAnchorMin;
            mapImage.rectTransform.anchorMax = smallMapAnchorMax;
            mapImage.rectTransform.pivot = smallMapPivot;
            mapImage.rectTransform.anchoredPosition = smallMapAnchorPos;
        }

        UpdateMapTexture(player.CellCoordinates, Mathf.RoundToInt(mapRadius));
    }

    private void UpdateMapTexture(Vector3Int caveCoordinates, int radius)
    {
        float gridToTexture = mapTexture.width / (float)caves.GridSize.x; // assumes grid and map are square
        Vector2Int mapCoordinates = new(
            Mathf.RoundToInt(caveCoordinates.x * gridToTexture),
            Mathf.RoundToInt(caveCoordinates.z * gridToTexture));
        int r = Mathf.CeilToInt(radius * gridToTexture);
        int r2 = r + 1;

        // Dim old values that were previously visible
        for (int dx = -r2; dx <= r2; dx++)
        {
            for (int dy = -r2; dy <= r2; dy++)
            {
                Vector2Int d = new(dx, dy);
                if (d.sqrMagnitude > r2 * r2) continue; // outside the circle
                d += mapCoordinates;
                Color c = mapTexture.GetPixel(d.x, d.y);
                if (c.a < 1f) continue;
                c /= 2f;
                mapTexture.SetPixel(d.x, d.y, c);
            }
        }

        Vector3 forwardVector = player.FaceForwardVector;
        Color pf = Color.Lerp(playerColor, Color.black, 0.25f);
        float flasher = Mathf.PingPong(Time.time * 5f, 1f);

        // Only update map pixels that are within the circle defined by 'caveCoordinates' and 'radius'
        for (int dx = -r; dx <= r; dx++)
        {
            for (int dy = -r; dy <= r; dy++)
            {
                Vector2Int d = new(dx, dy);
                if (d.sqrMagnitude > r * r) continue; // outside the circle
                if (Vector3.Dot(forwardVector, new Vector3(dx, 0, dy).normalized) < 0.7071f) continue; // outside view cone

                d += mapCoordinates;

                if (d.x < 0 || d.x >= mapTexture.width || d.y < 0 || d.y >= mapTexture.height) continue; // outside the map

                Vector3Int sample = new(
                    Mathf.RoundToInt(d.x / gridToTexture),
                    caveCoordinates.y,
                    Mathf.RoundToInt(d.y / gridToTexture));                
                int f = caves.FindFloorHeight(sample.x, sample.z);
                Color c;
                if (f < caves.GridSize.y)
                {
                    float a = Mathf.Ceil(f) / (float)caves.GridSize.y;
                    float b = 1 - a;
                    a *= a;
                    b *= b;
                    c = new(a, a, b, 1f);
                }
                else
                {
                    c = Color.black;
                }

                if (dx == 0 && dy == 0)
                {
                    c = playerColor; 
                }
                else if (Mathf.Approximately(dx, forwardVector.x) && Mathf.Approximately(dy, forwardVector.z))
                {
                    c = Color.Lerp(c, pf, flasher); 
                }

                mapTexture.SetPixel(d.x, d.y, c);
            }
        }
        mapTexture.Apply();
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpriteManager : MonoBehaviour
{
    public static SpriteManager Instance;
    public List<Sprite> sprites;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public int GetSpriteIndex(Sprite sprite)
    {
        return sprites.IndexOf(sprite);
    }

    public Sprite GetSpriteByIndex(int index)
    {
        if (index >= 0 && index < sprites.Count)
            return sprites[index];
        return null;
    }
}


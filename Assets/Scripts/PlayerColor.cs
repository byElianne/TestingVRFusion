using UnityEngine;
using Fusion; 

public class PlayerColor : NetworkBehaviour  
{
    public MeshRenderer MeshRenderer;

  
    [Networked, OnChangedRender(nameof(ColorChanged))]  
    public Color NetworkedColor { get; set; }

    void Update()
    {
        if (HasStateAuthority && Input.GetKeyDown(KeyCode.E))
        {
            NetworkedColor = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f), 1f);  
        }
    }

    void ColorChanged()
    {
        MeshRenderer.material.color = NetworkedColor;  // Use NetworkedColor, not NetworkColor
    }
}
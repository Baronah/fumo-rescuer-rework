using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircularMaskMover : MonoBehaviour
{
    public bool UseMouseMovement = false, UseCustomCenter = false;
    public Vector2 Center;
    public Material maskMaterial;
    public float radius = 0f, bgColor = 1f;

    private void Awake()
    {
        radius = 2f;
        SetMaterialAttributes();
    }

    private void OnEnable()
    {
        SetMaterialAttributes();
    }

    void Update()
    {
        SetMaterialAttributes();
    }

    void SetMaterialAttributes()
    {
        if (UseMouseMovement)
        {
            // Convert mouse position to normalized screen coordinates (0 to 1)
            Vector2 mousePos = Input.mousePosition;
            Vector2 screenPos = new Vector2(mousePos.x / Screen.width, mousePos.y / Screen.height);
            maskMaterial.SetVector("_Center", new Vector4(screenPos.x, screenPos.y, 0, 0));
        }
        else if (UseCustomCenter)
            maskMaterial.SetVector("_Center", new Vector4(Center.x, Center.y, 0, 0));

        // Calculate aspect ratio (width divided by height)
        float aspectRatio = (float)Screen.width / Screen.height;

        if (!maskMaterial.HasProperty("_MainTex"))
        {
            Debug.LogWarning("Material has no _MainTex property. Shader: " + maskMaterial.shader.name);
        }


        // Update the shader's properties
        maskMaterial.SetFloat("_Radius", radius);
        maskMaterial.SetColor("_Color", new Color(0, 0, 0, bgColor));
        maskMaterial.SetFloat("_ScreenAspect", aspectRatio); // Set the aspect ratio to the shader
    }
}

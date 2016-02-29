using System;
using UnityEngine;

[Serializable]
public class MouseLook : MonoBehaviour
{
	private GameSettings settings;

	// Configuration parameters loaded through GameSettings
    private float xSensitivity;
    private float ySensitivity;
    private bool clampVerticalRotation;
    private float minimumX;
    private float maximumX;
    private bool smooth;
    private float smoothTime;

	private Quaternion characterTargetRot;

	void Start()
	{
		settings = GameObject.Find("GameSettings").GetComponent<GameSettings>();
		LoadSettings();
	}

	private void LoadSettings()
	{
		xSensitivity		  = settings.EngineerMouseLookXSensitivity;
		ySensitivity		  = settings.EngineerMouseLookYSensitivity;
		clampVerticalRotation = settings.EngineerMouseLookClampVerticalRotation;
		minimumX              = settings.EngineerMouseLookMinimumX;
		maximumX 			  = settings.EngineerMouseLookMaximumX;
		smooth 				  = settings.EngineerMouseLookSmooth;
		smoothTime 			  = settings.EngineerMouseLookSmoothTime;
	}

    public void Init(Transform character, Transform camera)
    {
        characterTargetRot = character.localRotation;
    }
		
    public void LookRotation(Transform character, Transform camera)
    {
        float yRot = Input.GetAxis("Mouse X") * xSensitivity;
        float xRot = Input.GetAxis("Mouse Y") * ySensitivity;

        characterTargetRot *= Quaternion.Euler (-xRot, yRot, 0f);

        if(smooth)
            character.localRotation = Quaternion.Slerp (character.localRotation, characterTargetRot,
                smoothTime * Time.deltaTime);
        else
            character.localRotation = characterTargetRot;
    }
}

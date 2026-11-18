using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DrillManage : MonoBehaviour
{
    [SerializeField] private InputActionProperty leftTriggerAction;
    [SerializeField] private InputActionProperty rightTriggerAction;

    [SerializeField] private XRGrabInteractable drill;
    [SerializeField] private GameObject obj;

    private bool isRight; //0 -> left 1 -> right

    private float isGripped;
    void FixedUpdate()
    {
        if (!drill.isSelected)
        {
            obj.GetComponent<ShaderTests>().drillRunning = false;
            return;
        }

        handCheck();
        if(isRight) isGripped = rightTriggerAction.action.ReadValue<float>();
        else isGripped = leftTriggerAction.action.ReadValue<float>();

        drillOn();

    }
    
    private void handCheck()
    {
        if (drill.firstInteractorSelecting.transform.name.Contains("Right")) isRight = true;
        else isRight = false;
    }
    private void drillOn()
    {
        if (isGripped > 0)
        {
            //Debug.Log("Trigger is being pulled! " + isRight);
            obj.GetComponent<ShaderTests>().drillRunning = true;
            if (drill.firstInteractorSelecting is XRBaseInputInteractor controllerInteractor)
            {
                //Debug.Log("IS HAPTIC");
                controllerInteractor.SendHapticImpulse(0.5f, 0.2f);
            }
        }
        else obj.GetComponent<ShaderTests>().drillRunning = false;
    }
}

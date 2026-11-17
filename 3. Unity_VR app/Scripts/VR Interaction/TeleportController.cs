/*
----------------------------------------------------------------------------------------------------------
Script: TeleportController.cs
Author: Aaron Perez Herrera
Description:    This script manages smooth, parabolic CR teleportation, safely moving the player 
                (XROrigin) to a focused structural element during semantic filtering operations.
AI Assistance:  The user experience design (parabolic flight path) and teleport logic were conceptualized
                by the author. Claude (Anthropic, 2026) was used as a syntax assistant to correctly 
                implement the Unity XR Locomotions System exclusive operation requests and to formulate 
                the quadratic Bezier curve interpolation inside the Coroutine.
----------------------------------------------------------------------------------------------------------
*/

using UnityEngine;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;

public class TeleportController : MonoBehaviour
{
    private Coroutine teleportCoroutine;

    // Method to Start or interrupt the teleportation in filtering of elements
    public void Teleport(XROrigin player, Vector3 targetPosition, Quaternion targetRotation, float arcHeightFactor, float flightDuration)
    {
        if (teleportCoroutine != null)
        {
            StopCoroutine(teleportCoroutine);
        }

        teleportCoroutine = StartCoroutine(
            TeleportCoroutine(player, targetPosition, targetRotation, arcHeightFactor, flightDuration)
        );
    }
    
    // AI-Assited Implementation: The programmatic request for exclusive access to the XR LocomotionSystem 
    // and the quadratic Bezier curve math for the smooth camera transition were implemented with syntax 
    // support from Claude
    // Method to smoothly teleport the player to the target position and rotation with a parabolic arc for better visual effect
    [System.Obsolete]
    private IEnumerator TeleportCoroutine(XROrigin player, Vector3 targetPosition, Quaternion targetRotation, float arcHeightFactor, float flightDuration)
    {
        // Get the main camera from the XROrigin to control its rotation during teleportation
        LocomotionSystem locomotion = player.GetComponentInChildren<LocomotionSystem>();

        // Access XROrigin to disable character controller and rigidbody
        UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider tp = player.GetComponentInChildren<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationProvider>();
        bool hasExclusiveAccess = false;

        // Request exclusive access to the locomotion system to ensure smooth teleportation without interference from other locomotion actions
        if (locomotion != null && tp != null)
        {
            RequestResult rs = locomotion.RequestExclusiveOperation(tp);
            hasExclusiveAccess = rs == RequestResult.Success;
        }

        // Store the starting position and rotation of the player to interpolate from
        Vector3    startPosition = player.transform.position;
        Quaternion startRotation = player.Camera != null 
            ? player.Camera.transform.rotation 
            : player.transform.rotation;

        // Calculate the arc peak for a smooth parabolic trajectory during teleportation
        float   travelDistance = Vector3.Distance(startPosition, targetPosition);
        Vector3 arcPeak        = (startPosition + targetPosition) * 0.5f + Vector3.up * (travelDistance * arcHeightFactor);

        // Duration of the teleportation based on distance to ensure consistent speed
        float elapsed = 0f;
        
        while (elapsed < flightDuration)
        {
            elapsed += Time.deltaTime;

            float t       = Mathf.Clamp01(elapsed / flightDuration);
            float tSmooth = Mathf.SmoothStep(0f, 1f, t);

            float oneMinusT = 1f - tSmooth;
            Vector3 bezierPos = (oneMinusT * oneMinusT) * startPosition
                            + (2f * oneMinusT * tSmooth) * arcPeak
                            + (tSmooth * tSmooth)        * targetPosition;

            // Move the player to the calculated position along the arc
            player.MoveCameraToWorldLocation(bezierPos);

            // Rotate the player smoothly towards the target rotation during teleportation
            Quaternion currentRotation = Quaternion.Slerp(startRotation, targetRotation, tSmooth);
            Vector3 targetUp = currentRotation * Vector3.up;
            Vector3 targetForward = currentRotation * Vector3.forward;
            player.MatchOriginUpCameraForward(targetUp, targetForward);

            yield return null;
        }

        // Ensure the player ends up exactly at the target position and rotation after teleportation
        player.MoveCameraToWorldLocation(targetPosition);
        player.MatchOriginUpCameraForward(targetRotation * Vector3.up, targetRotation * Vector3.forward);

        // Release exclusive access to the locomotion system after teleportation to allow locomotion
        if(hasExclusiveAccess && locomotion != null && tp != null)
            locomotion.FinishExclusiveOperation(tp);

        // Clear the teleportation coroutine reference after completion
        teleportCoroutine = null;
    }
}

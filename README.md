HTC Vive Teleportation System with Parabolic Pointer
----------------------------------------------------

This is an easy-to-use teleportation system for the HTC Vive and the Unity game engine.  The system is modelled after
Valve's game for the Vive [*The Lab*](http://store.steampowered.com/app/450390/), where the player can traverse 
VR environments that are bigger than the play area.  Below you can see myself demoing the system (click for higher quality):

[![Demo](https://thumbs.gfycat.com/HonorableComplexCutworm-size_restricted.gif)](https://gfycat.com/HonorableComplexCutworm)

The system presented here solves a number of problems:

1. **Calculating Navigable Space**: You obviously don't want the player to be able to teleport out of bounds, or inside
   opaque objects.  To solve this problem, my system uses Unity's generated Navigation Mesh as the boundaries that the
   player can teleport to.  Because this process is piggybacking Unity's work, it is stable and can be used reliably in most
   projects.  In order to preload this data, you must:

   * Change the Navigation settings in Unity to something that makes sense for Vive locomotion (for example, set Max Slope
     to zero because the player can't walk up slopes).
   * Bake the Navigation Mesh in Unity
   * Add a "Vive Nav Mesh" component anywhere in your scene, and click the "Update Navmesh Data" button in the inspector
   * Change your Navigation settings back to their original values and rebake (to be used for other things like AI, etc.)

   You can of course update the Vive Nav Mesh component with new NavMesh bakes whenever you update the scene.  The above process is illustrated below:
   
   [![Updating the NavMesh](https://thumbs.gfycat.com/WelldocumentedForcefulAlaskanmalamute-size_restricted.gif)](https://gfycat.com/WelldocumentedForcefulAlaskanmalamute)

   It's worth mentioning that this whole process could be automated completely if Unity exposed Navigation settings
   to editor scripts (currently it does not do this).  If you want to help this project a bit, **please [vote on this page
   on Unity Feedback](https://feedback.unity3d.com/suggestions/expose-navigation-settings-to-editor-scripts)**!
2. **Selecting a Teleport Destination**: This system uses an intuitive parabolic curve selection mechanism using simple
   kinematic equations.  Once again, this was inspired by Valve's *The Lab*.  As the user raises their controller to a higher
   angle, the selection point grows farther away.  If the user raises the remote past 45 degrees (maximum distance of a parabolic
   curve) the angle stays locked at that distance.
3. **Representing the Play Area**: It is often useful to know where the chaperone boundaries will be after teleporting.  For
   this reason the system draws a box around where the chaperone bounds will be.
4. **Reducing Discomfort**: The screen fades in and fades out upon teleportation (the display "blinks"), reducing fatigue
   and nausea for the user.

Provided in this Unity project (version 5.3.4p4) are two sample scenes: one is integrated directly with SteamVR and one
may be used to demo the system if you don't own or have access to an HTC Vive.  The source code is well documented and
commented and may be used following the MIT Licence (see LICENSE.txt).
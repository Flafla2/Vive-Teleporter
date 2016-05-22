HTC Vive Teleportation System with Parabolic Pointer
====================================================

This is an easy-to-use teleportation system for the HTC Vive and the Unity game engine.  The system is modelled after
Valve's game for the Vive [*The Lab*](http://store.steampowered.com/app/450390/), where the player can traverse 
VR environments that are bigger than the play area.  Below you can see myself demoing the system (click for higher
quality):

[![Demo](https://thumbs.gfycat.com/HonorableComplexCutworm-size_restricted.gif)](https://gfycat.com/HonorableComplexCutworm)

The system presented here solves a number of problems:

1. **Calculating Navigable Space**: You obviously don't want the player to be able to teleport out of bounds, or inside
   opaque objects.  To solve this problem, my system uses Unity's generated Navigation Mesh as the boundaries that the
   player can teleport to.  Because this process is piggybacking Unity's work, it is stable and can be used reliably
   in most projects.  In order to preload this data, you must:

   * Change the Navigation settings in Unity to something that makes sense for Vive locomotion (for example, set Max
     Slope to zero because the player can't walk up slopes).
   * Bake the Navigation Mesh in Unity
   * Add a "Vive Nav Mesh" component anywhere in your scene, and click the "Update Navmesh Data" button in the
     inspector
   * Change your Navigation settings back to their original values and rebake (to be used for other things like AI,
     etc.)

   You can of course update the Vive Nav Mesh component with new NavMesh bakes whenever you update the scene.  The above process is illustrated below:
   
   [![Updating the NavMesh](https://thumbs.gfycat.com/WelldocumentedForcefulAlaskanmalamute-size_restricted.gif)](https://gfycat.com/WelldocumentedForcefulAlaskanmalamute)

   It's worth mentioning that this whole process could be automated completely if Unity exposed Navigation settings
   to editor scripts (currently it does not do this).  If you want to help this project a bit, **please [vote on this
   page on Unity Feedback](https://feedback.unity3d.com/suggestions/expose-navigation-settings-to-editor-scripts)**!
2. **Selecting a Teleport Destination**: This system uses an intuitive parabolic curve selection mechanism using simple
   kinematic equations.  Once again, this was inspired by Valve's *The Lab*.  As the user raises their controller to a
   higher angle, the selection point grows farther away.  If the user raises the remote past 45 degrees (maximum
   distance of a parabolic curve) the angle stays locked at that distance.
3. **Representing the Play Area**: It is often useful to know where the chaperone boundaries will be after
   teleporting.  For this reason the system draws a box around where the chaperone bounds will be.
4. **Reducing Discomfort**: The screen fades in and fades out upon teleportation (the display "blinks"), reducing
   fatigue and nausea for the user.

Provided in this Unity project (version 5.3.4p4) are two sample scenes: one is integrated directly with SteamVR and one
may be used to demo the system if you don't own or have access to an HTC Vive.  The source code is well documented and
commented and may be used following the MIT Licence (see LICENSE.txt).

Getting Started
---------------

To get a basic teleportation setup running you need to use three components: 

- The **Vive Nav Mesh** component handles the conversion between Unity's NavMesh system to a renderable mesh.  It also 
  calculates the borders of the NavMesh so that they can be shown to the player when choosing a place to teleport.
- The **Parabolic Pointer** component generates/displays a pointer mesh and samples points from a *Vive Nav Mesh*.
- The **Vive Teleporter** component handles the actual teleportation mechanic.  It pulls pointer data from a
  **Parabolic Pointer** so that it knows where to teleport.  It also smoothly fades the screen in and out to prevent
  discomfort when the player decides to teleport.  It also interfaces with SteamVR to handle button press events,
  controller management, haptic feedback, and displaying the room boundaries when choosing a place to teleport.

Quick Note: The **Teleport Vive** and **Parabolic Pointer** components both automatically add a **Border Renderer**
component.  **Border Renderer** simply generates and renders a mesh to display the borders of the **Vive Nav Mesh** and
the SteamVR play area.

### Step 1: Configure the Vive Nav Mesh

![Vive Nav Mesh](http://i.imgur.com/20E3qdT.png)

Start by adding a *Vive Nav Mesh* object.  You can find a preconfigured Vive Nav Mesh at the path:
*Vive-Teleporter/Prefabs/Navmesh.prefab* in your Assets folder.  You can put this object anywhere in your scene's
heirarchy and at any position in the scene.

Next you need to bake a Navigation mesh ("Navmesh") in Unity.  This can be done in the Navigation window (Window >
Navigation).  If you already have baked a Navmesh for other purposes (AI for example) you should write down the
Navigation settings you had previously (you can rebake the mesh with your old settings later).  I recommend these
settings:

- Agent Radius: 0.25
- Agent Height: 2
- Max Slope: 0
- Step Height: 0
- Drop Height: 0
- Jump Distance: 0

The above settings all ensure that the generated Navmesh will not have any slopes or off-mesh links (which don't make
any sense in roomscale VR, where the player navigates on a horizontal plane).

After you have baked the Navmesh (using the "Bake" button at the bottom of the Navigation window) go back to the *Vive
Nav Mesh* object you created earlier.  Click on the "Update Navmesh Data" button in the inspector and you should see
your Navigation mesh display in the Scene View.

### Step 2: Configure the Parabolic Pointer

![Parabolic Pointer](http://i.imgur.com/iS8N2yq.png)

Next add a *Parabolic Pointer* object.  You can find a preconfigured Pointer at the path:
*Vive-Teleporter/Prefabs/Pointer.prefab* in your Assets folder.  You can put this object anywhere in your scene's
heirarchy and at any position in the scene.

You can of course tweak any of the settings in the Parabolic Pointer script, but you only *have* to set one of them: 
assign the *Vive Nav Mesh* object from Step 1 to the "Nav Mesh" property of the Pointer.

### Step 3: Configure the Vive Teleporter

![Vive Teleporter](http://i.imgur.com/dYCQaPN.png)

Lastly you need to add a *Vive Teleporter* Component (Component > Vive Teleporter > Vive Teleporter) to the **SteamVR
Camera**.  This is the camera that is used to render to the Vive's display.  If you are using the *[CameraRig]* prefab
from the [SteamVR Unity plugin](https://www.assetstore.unity3d.com/en/#!/content/32647) you should add the *Vive
Teleporter* to the *Camera (eye)* object in that prefab.

Next assign the component properties to these values:

- *Pointer*: Set this to the *Parabolic Pointer* object you created in Step 2
- *Origin Transform*: Set this to the origin of the tracking space.  If you are using the SteamVR Unity Plugin, this
  is the *[CameraRig]* GameObject.  This is the object that is actually moved when the player teleports.
- *Head Transform*: Set this to the transform of the player's head.  This should be a child of the *Origin Transform*.
  If you are using the SteamVR Unity Plugin, this is the *Camera (head)* GameObject.
- *Navmesh Animator*: Set this to the Animator of the *Vive Nav Mesh* object created in Step 1.
- *Fade Material*: Set this to the material found in *Vive-Teleporter/Art/Materials/FadeBlack.mat*
- *Controllers*: Populate this with the SteamVR controller objects.  If you are using the SteamVR *[CameraRig]* prefab,
  you should populate this with the *Controller (left)* and *Controller (right)* objects.
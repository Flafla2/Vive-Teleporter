HTC Vive Teleportation System with Arc Pointer
==============================================

This is an easy-to-use teleportation system for the HTC Vive and the Unity game engine.  The system is modelled after
Valve's game for the Vive [*The Lab*](http://store.steampowered.com/app/450390/), where the player can traverse 
VR environments that are bigger than the play area.  Below you can see myself demoing the system (click for higher
quality):

[![Demo](https://thumbs.gfycat.com/HonorableComplexCutworm-size_restricted.gif)](https://gfycat.com/HonorableComplexCutworm)

The system presented here solves a number of problems:

1. **Calculating Navigable Space**: You obviously don't want the player to be able to teleport out of bounds, or inside
   opaque objects.  To solve this problem, my system uses Unity's generated Navigation Mesh as the boundaries that the
   player can teleport to.  Because this process is piggybacking Unity's work, it is stable and can be used reliably
   in most projects.  In order to preload this data, simply add a "Vive Nav Mesh" component anywhere in your scene, and
   click the "Update Navmesh Data" button in the inspector.  You can of course update the Vive Nav Mesh component with
   new NavMesh bakes whenever you update the scene.  The above process is illustrated below:
   
   [![Updating the NavMesh](https://thumbs.gfycat.com/SorrowfulThriftyAfricanpiedkingfisher-size_restricted.gif)](https://gfycat.com/SorrowfulThriftyAfricanpiedkingfisher)
2. **Selecting a Teleport Destination**: This system uses an intuitive parabolic curve selection mechanism using simple
   kinematic equations.  Once again, this was inspired by Valve's *The Lab*.  As the user raises their controller to a
   higher angle, the selection point grows farther away.  If the user raises the remote past 45 degrees (maximum
   distance of a parabolic curve) the angle stays locked at that distance.
3. **Representing the Play Area**: It is often useful to know where the chaperone boundaries will be after
   teleporting.  For this reason the system draws a box around where the chaperone bounds will be.
4. **Reducing Discomfort**: The screen fades in and fades out upon teleportation (the display "blinks"), reducing
   fatigue and nausea for the user.

Provided in this Unity project (version 5.5.0p3) are two sample scenes: one is integrated directly with SteamVR and one
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

![Vive Nav Mesh](http://i.imgur.com/ZmByfYq.png)

Start by adding a *Vive Nav Mesh* object.  You can find a preconfigured Vive Nav Mesh at the path:
*Vive-Teleporter/Prefabs/Navmesh.prefab* in your Assets folder.  You can put this object anywhere in your scene's
heirarchy and at any position in the scene.

Next you need to bake a Navigation mesh ("Navmesh") in Unity.  This can be done in the Navigation window (Window >
Navigation).

Here are a few more considerations to keep in mind:
- **You must use physics colliders on all teleportable surfaces.**  The parabolic pointer (see step 2 below) uses physics
  raycasts to determine where the player is pointing.  Because of this all teleportable surfaces must have a collider
  (as well as surfaces like walls that aren't teleportable but block the pointer anyway).
- It might also be a good idea to **assign different [Navigation Areas](http://docs.unity3d.com/Manual/nav-AreasAndCosts.html)**
  to areas that are not teleportable.  This is helpful for optimization reasons (so that the system doesn't need to render
  an enormous preview mesh when the player chooses where to teleport) and for game balance reasons (so that the player can't
  teleport outside of the map).

After you have baked the Navmesh (using the "Bake" button at the bottom of the Navigation window) go back to the *Vive
Nav Mesh* object you created earlier.  If you have decided to assign specialized Navigation Areas (see above) you can 
choose which areas are teleportable with the *Area Mask* property.  Then, click on the "Update Navmesh Data" button in
the inspector and you should see your Navigation mesh display in the Scene View.

#### Properties

- *Area Mask*: Defines the [Navmesh area](https://docs.unity3d.com/Manual/nav-AreasAndCosts.html) mask used by the 
  system.  One application of this is for optimization - by setting some objects as "non-teleportable," you can reduce
  the polycount of the preview mesh.

Render Settings
- *Ground Material Source*: The material to be used for previewing teleportable areas.
- *Ground Alpha*: This is an animatable parameter that changes the alpha (transparency) of the ground material.  The 
  Vive Teleporter script (see below) uses this value to animate the preview when the player is selecting a place to 
  teleport.

Raycast Settings
- *Layer Mask*: Used to mask colliders that are recognized by the system.  Note: layers included in this mask are not 
  recognized *at all* by Navmesh queries (by the Parabolic pointer for example).  So, the arc pointer will go through 
  colliders captured by the layer mask.  This is useful for surfaces that you want to be recognized by other systems, 
  such as AI, but not the teleporter.
- *Ignore Layer Mask*: If true, layers included in the *Layer Mask* are considered "valid".  If false, layers included
  in the *Layer Mask* are considered invalid and all others are valid.
- *Query Trigger Interaction*: Determines if trigger colliders are recognized by the system.  "Use Global" uses the 
  [Physics.queriesHitTriggers](https://docs.unity3d.com/ScriptReference/Physics-queriesHitTriggers.html) setting.

Navmesh Settings
- *Sample Radius*: This should be set to the 
  [Navmesh Voxel Size](https://docs.unity3d.com/Manual/nav-AdvancedSettings.html) that you are currently using.  You can
  find this in the Navigation Window (``Navigation > Bake > Advanced > Voxel Size``).  If this value is too small, you
  may experience issues where teleportable surfaces are not recognized correctly.
- *Ignore Sloped Surfaces*: If true, the system will ignore sloped surfaces when querying the Navmesh.  This is highly
  recommended, as players can't actually walk up sloped surfaces in VR!
- *Dewarping Method*: In some cases (especially in larger scenes with lots of detailed geometry), Unity's Navmesh will
  not give an aesthetically pleasing output.  For example, in some cases flat surfaces will appear as non-flat in the
  Navmesh output.  You can use a so-called Dewarping method to filter Unity's navmesh in the Navmesh preview.
  - *None*: Use no dewarping.  This is usually OK for smaller scenes.
  - *Round to Voxel Size*: Rounds the Y-position of each vertex in the preview mesh to the *Sample Radius* defined
    above.  This has no additional overhead when Processing the Navmesh, but the preview mesh may appear to be floating
    above the ground.
  - *Raycast Downward*: This is the most accurate dewarping method, but comes with additional overhead when processing
    Navmesh data (that is, when Clicking the "Update Navmesh Data" button).  For each vertex in the preview mesh, the
    system shoots a raycast downward to find the exact position of each vertex.  This ensures the accuracy of the mesh.

### Step 2: Configure the Parabolic Pointer

![Parabolic Pointer](http://i.imgur.com/1IYIAiE.png)

Next add a *Parabolic Pointer* object.  You can find a preconfigured Pointer at the path:
*Vive-Teleporter/Prefabs/Pointer.prefab* in your Assets folder.  You can put this object anywhere in your scene's
heirarchy and at any position in the scene.

#### Properties

- *Nav Mesh*: [Required] The *Vive Navmesh* you are using (see above).
- *Parabola Trajectory*: Use these options to configure the shape of the pointer's arc.  Increasing the Z parameter of
  the *Initial Velocity* OR increasing the Y parameter of the *Acceleration* will make the pointer arc travel further.

Parabola Mesh Properties
- *Point Count*: The maximum number of points in the parabola arc mesh.  Increasing this allows the arc to cover larger
  distances, but has a performance / rendering cost.
- *Point Spacing*: The distance (in meters) between each point in the parabola arc mesh.  Decreasing this brings the arc
  mesh closer to a perfect parabola, but the arc covers smaller distances with the same Point Count (see above).
- *Graphic Thickness*: The thickness (in meters) of the arc mesh.
- *Graphic Material*: The material used to render the parabola mesh.  The UVs of the arc mesh are automatically
  configured so that the given texture is scrolled smoothly along the arc (``U`` = 0 on left side of arc, 1 on right
  side.  ``V`` is repeated and scrolled along the length of the parabola).
  
Selection Pad Properties
- *Selection Pad Prefab*: Prefab to use as the "selection pad."  This is placed at a tentative teleport destination when
  the player is pointing at a valid teleportable surface.  By default, I have included an orange selection pad mesh
  (``Vive-Teleporter/Art/Prefabs/Selection Pad``).
- *Invalid Pad Prefab*: Prefab to use as the "selection pad" when pointing at an invalid / non-teleportable surface.  By
  default, I have included a red X mesh (``Vive-Teleporter/Art/Prefabs/Invalid Selection Pad``)

### Step 3: Configure the Vive Teleporter

![Vive Teleporter](http://i.imgur.com/dYCQaPN.png)

Lastly you need to add a *Vive Teleporter* Component (Component > Vive Teleporter > Vive Teleporter) to the **SteamVR
Camera**.  This is the camera that is used to render to the Vive's display.  If you are using the *[CameraRig]* prefab
from the [SteamVR Unity plugin](https://www.assetstore.unity3d.com/en/#!/content/32647) you should add the *Vive
Teleporter* to the *Camera (eye)* object in that prefab.

#### Properties

- *Pointer*: [Required] Set this to the *Parabolic Pointer* object you created in Step 2
- *Origin Transform*: Set this to the origin of the tracking space.  If you are using the SteamVR Unity Plugin, this
  is the *[CameraRig]* GameObject.  This is the object that is actually moved when the player teleports.
- *Head Transform*: Set this to the transform of the player's head.  This should be a child of the *Origin Transform*.
  If you are using the SteamVR Unity Plugin, this is the *Camera (head)* GameObject.
- *Navmesh Animator*: Set this to the Animator of the *Vive Nav Mesh* object created in Step 1.
- *Fade Material*: Set this to the material found in *Vive-Teleporter/Art/Materials/FadeBlack.mat*
- *Controllers*: Populate this with the SteamVR controller objects.  If you are using the SteamVR *[CameraRig]* prefab,
  you should populate this with the *Controller (left)* and *Controller (right)* objects.
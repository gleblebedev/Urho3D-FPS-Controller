// Moving character example.
// This sample demonstrates:
//     - Controlling a humanoid character through physics
//     - Driving animations using the AnimationController component
//     - Manual control of a bone scene node
//     - Implementing 1st and 3rd person cameras, using raycasts to avoid the 3rd person camera clipping into scenery
//     - Saving and loading the variables of a script object
//     - Using touch inputs/gyroscope for iOS/Android (implemented through an external file)

#include "Scripts/Utilities/Sample.as"
#include "Scripts/Utilities/Touch.as"

const int CTRL_FORWARD = 1;
const int CTRL_BACK = 2;
const int CTRL_LEFT = 4;
const int CTRL_RIGHT = 8;
const int CTRL_JUMP = 16;
const int CTRL_SHIFT = 32;
const int CTRL_CROUCH = 64;

const float YAW_SENSITIVITY = 0.1f;

bool firstPerson = false; // First person camera flag

Node@ characterNode;

void Start()
{
    cache.autoReloadResources = true;

    // Execute the common startup for samples
    SampleStart();

    // Create static scene content
    CreateScene();

    // Create the controllable character
    CreateCharacter();

    // Create the UI content
    CreateInstructions();

    // Set the mouse mode to use in the sample
    SampleInitMouseMode(MM_RELATIVE);

    // Subscribe to necessary events
    SubscribeToEvents();
}

void CreateScene()
{
    scene_ = Scene();
    scene_.LoadXML(cache.GetFile("Scenes/LevelFPS.xml"));

    // Create camera and define viewport. Camera does not necessarily have to belong to the scene
    cameraNode = Node();
    Camera@ camera = cameraNode.CreateComponent("Camera");
    camera.farClip = 300.0f;
    float desiredVFov = 44.0;
    camera.fov = 2.0 * Atan(Tan((desiredVFov * 3.14 / 180.0) / 2.0) * graphics.width / graphics.height) * 180.0 / 3.14;

    renderer.viewports[0] = Viewport(scene_, camera);
    renderer.shadowQuality = SHADOWQUALITY_BLUR_VSM;
    renderer.shadowSoftness = 3.0;
    renderer.SetVSMShadowParameters(0.000001, 0.2);
}

void CreateCharacter()
{
    characterNode = scene_.InstantiateXML(cache.GetFile("Objects/Character.xml"), Vector3(0.0, 1.0, 0.0), Quaternion(0,0,0));
    characterNode.CreateComponent("Character");
}

void CreateInstructions()
{
    // Construct new Text object, set string to display and font to use
    Text@ instructionText = ui.root.CreateChild("Text");
    instructionText.text =
        "Use WASD keys and mouse to move\n"
        "Space to jump, F to toggle 1st/3rd person\n"
        "F5 to save scene, F7 to load";
    instructionText.SetFont(cache.GetResource("Font", "Fonts/Anonymous Pro.ttf"), 15);
    // The text has multiple rows. Center them in relation to each other
    instructionText.textAlignment = HA_CENTER;

    // Position the text relative to the screen center
    instructionText.horizontalAlignment = HA_CENTER;
    instructionText.verticalAlignment = VA_CENTER;
    instructionText.SetPosition(0, ui.root.height / 4);
}

void SubscribeToEvents()
{
    // Subscribe to Update event for setting the character controls before physics simulation
    SubscribeToEvent("Update", "HandleUpdate");

    // Subscribe to PostUpdate event for updating the camera position after physics simulation
    SubscribeToEvent("PostUpdate", "HandlePostUpdate");

    // Subscribe HandlePostRenderUpdate() function for processing the post-render update event, during which we request debug geometry
    SubscribeToEvent("PostRenderUpdate", "HandlePostRenderUpdate");

    // Unsubscribe the SceneUpdate event from base class as the camera node is being controlled in HandlePostUpdate() in this sample
    UnsubscribeFromEvent("SceneUpdate");
}

void HandleUpdate(StringHash eventType, VariantMap& eventData)
{
    Camera@ camera = cameraNode.GetComponent("Camera");
    if (camera !is null)
    {
        renderer.defaultRenderPath.shaderParameters["ViewProjPS"] = Variant(camera.view);
        renderer.defaultRenderPath.shaderParameters["ViewInvPS"] = Variant(camera.effectiveWorldTransform);
    }

    if (characterNode is null)
        return;

    Character@ character = characterNode.GetComponent("Character");
    if (character is null)
        return;

    // Clear previous controls
    character.controls.Set(CTRL_FORWARD | CTRL_BACK | CTRL_LEFT | CTRL_RIGHT | CTRL_JUMP | CTRL_SHIFT | CTRL_CROUCH, false);

    // Update controls using touch utility
    if (touchEnabled)
        UpdateTouches(character.controls);

    // Update controls using keys (desktop)
    if (ui.focusElement is null)
    {
        if (touchEnabled || !useGyroscope)
        {
            character.controls.Set(CTRL_FORWARD, input.keyDown[KEY_W]);
            character.controls.Set(CTRL_BACK, input.keyDown[KEY_S]);
            character.controls.Set(CTRL_LEFT, input.keyDown[KEY_A]);
            character.controls.Set(CTRL_RIGHT, input.keyDown[KEY_D]);
            character.controls.Set(CTRL_SHIFT, input.keyDown[KEY_SHIFT]);
            character.controls.Set(CTRL_CROUCH, input.keyDown[KEY_CTRL]);
        }
        character.controls.Set(CTRL_JUMP, input.keyDown[KEY_SPACE]);

        // Add character yaw & pitch from the mouse motion or touch input
        if (touchEnabled)
        {
            for (uint i = 0; i < input.numTouches; ++i)
            {
                TouchState@ state = input.touches[i];
                if (state.touchedElement is null) // Touch on empty space
                {
                    Camera@ camera = cameraNode.GetComponent("Camera");
                    if (camera is null)
                        return;

                    character.controls.yaw += TOUCH_SENSITIVITY * camera.fov / graphics.height * state.delta.x;
                    character.controls.pitch += TOUCH_SENSITIVITY * camera.fov / graphics.height * state.delta.y;
                }
            }
        }
        else
        {
            character.controls.yaw += input.mouseMoveX * YAW_SENSITIVITY;
            character.controls.pitch += input.mouseMoveY * YAW_SENSITIVITY;
        }
        // Limit pitch
        character.controls.pitch = Clamp(character.controls.pitch, -80.0f, 80.0f);
        // Set rotation already here so that it's updated every rendering frame instead of every physics frame
        characterNode.rotation = Quaternion(character.controls.yaw, Vector3(0.0f, 1.0f, 0.0f));

        // Switch between 1st and 3rd person
        if (input.keyPress['F'])
            firstPerson = !firstPerson;

        // Turn on/off gyroscope on mobile platform
        if (input.keyPress['G'])
            useGyroscope = !useGyroscope;

        // Toggle debug geometry with space
        if (input.keyPress[KEY_TAB])
            drawDebug = !drawDebug;

        // Check for loading / saving the scene
        if (input.keyPress[KEY_F5])
        {
            File saveFile(fileSystem.programDir + "Data/Scenes/CharacterDemo.xml", FILE_WRITE);
            scene_.SaveXML(saveFile);
        }
        if (input.keyPress[KEY_F7])
        {
            File loadFile(fileSystem.programDir + "Data/Scenes/CharacterDemo.xml", FILE_READ);
            scene_.LoadXML(loadFile);
            // After loading we have to reacquire the character scene node, as it has been recreated
            // Simply find by name as there's only one of them
            characterNode = scene_.GetChild("Jack", true);
            if (characterNode is null)
                return;
        }
    }
}

void HandlePostUpdate(StringHash eventType, VariantMap& eventData)
{
    if (characterNode is null)
        return;

    Character@ character = characterNode.GetComponent("Character");
    if (character is null)
        return;

    // Get camera lookat dir from character yaw + pitch
    Quaternion rot = characterNode.rotation;
    Quaternion dir = rot * Quaternion(character.controls.pitch, Vector3(1.0f, 0.0f, 0.0f));

    // Turn head to camera pitch, but limit to avoid unnatural animation
    Node@ characterModel = character.modelNode;
    if (characterModel is null)
        return;
    Node@ headNode = characterModel.GetChild("Bip01_Head", true);
    if (headNode is null)
        return;
    float limitPitch = Clamp(character.controls.pitch, -45.0f, 45.0f);
    Quaternion headDir = rot * Quaternion(limitPitch, Vector3(1.0f, 0.0f, 0.0f));
    // This could be expanded to look at an arbitrary target, now just look at a point in front
    Vector3 headWorldTarget = headNode.worldPosition + headDir * Vector3(0.0f, 0.0f, 1.0f);
    headNode.LookAt(headWorldTarget, Vector3(0.0f, 1.0f, 0.0f));
    // Correct head orientation because LookAt assumes Z = forward, but the bone has been authored differently (Y = forward)
    headNode.Rotate(Quaternion(0.0f, 90.0f, 90.0f));

    if (firstPerson)
    {
        // First person camera: position to the head bone + offset slightly forward & up
        cameraNode.position = headNode.worldPosition + rot * Vector3(0.0f, 0.15f, 0.2f);
        cameraNode.rotation = dir;
    }
    else
    {
        // Third person camera: position behind the character
        Vector3 aimPoint =  headNode.worldPosition - Vector3(0.0f, 2.0f, 0.0f) + rot * Vector3(0.0f, 1.7f, 0.0f); // You can modify x Vector3 value to translate the fixed character position (indicative range[-2;2])

        // Collide camera ray with static physics objects (layer bitmask 2) to ensure we see the character properly
        Vector3 rayDir = dir * Vector3(0.0f, 0.0f, -1.0f); // For indoor scenes you can use dir * Vector3(0.0, 0.0, -0.5) to prevent camera from crossing the walls
        float rayDistance = cameraDistance;
        PhysicsRaycastResult result = scene_.physicsWorld.RaycastSingle(Ray(aimPoint, rayDir), rayDistance, 2);
        if (result.body !is null)
            rayDistance = Min(rayDistance, result.distance);
        rayDistance = Clamp(rayDistance, CAMERA_MIN_DIST, cameraDistance);

        cameraNode.position = aimPoint + rayDir * rayDistance;
        cameraNode.rotation = dir;
    }
}

void HandlePostRenderUpdate(StringHash eventType, VariantMap& eventData)
{
    if (drawDebug)
    {
        // Visualize navigation mesh, obstacles and off-mesh connections
        scene_.GetComponent("PhysicsWorld").DrawDebugGeometry(scene_.debugRenderer, true);
    }
}

// Create XML patch instructions for screen joystick layout specific to this sample app
String patchInstructions =
        "<patch>" +
        "    <add sel=\"/element\">" +
        "        <element type=\"Button\">" +
        "            <attribute name=\"Name\" value=\"Button3\" />" +
        "            <attribute name=\"Position\" value=\"-120 -120\" />" +
        "            <attribute name=\"Size\" value=\"96 96\" />" +
        "            <attribute name=\"Horiz Alignment\" value=\"Right\" />" +
        "            <attribute name=\"Vert Alignment\" value=\"Bottom\" />" +
        "            <attribute name=\"Texture\" value=\"Texture2D;Textures/TouchInput.png\" />" +
        "            <attribute name=\"Image Rect\" value=\"96 0 192 96\" />" +
        "            <attribute name=\"Hover Image Offset\" value=\"0 0\" />" +
        "            <attribute name=\"Pressed Image Offset\" value=\"0 0\" />" +
        "            <element type=\"Text\">" +
        "                <attribute name=\"Name\" value=\"Label\" />" +
        "                <attribute name=\"Horiz Alignment\" value=\"Center\" />" +
        "                <attribute name=\"Vert Alignment\" value=\"Center\" />" +
        "                <attribute name=\"Color\" value=\"0 0 0 1\" />" +
        "                <attribute name=\"Text\" value=\"Gyroscope\" />" +
        "            </element>" +
        "            <element type=\"Text\">" +
        "                <attribute name=\"Name\" value=\"KeyBinding\" />" +
        "                <attribute name=\"Text\" value=\"G\" />" +
        "            </element>" +
        "        </element>" +
        "    </add>" +
        "    <remove sel=\"/element/element[./attribute[@name='Name' and @value='Button0']]/attribute[@name='Is Visible']\" />" +
        "    <replace sel=\"/element/element[./attribute[@name='Name' and @value='Button0']]/element[./attribute[@name='Name' and @value='Label']]/attribute[@name='Text']/@value\">1st/3rd</replace>" +
        "    <add sel=\"/element/element[./attribute[@name='Name' and @value='Button0']]\">" +
        "        <element type=\"Text\">" +
        "            <attribute name=\"Name\" value=\"KeyBinding\" />" +
        "            <attribute name=\"Text\" value=\"F\" />" +
        "        </element>" +
        "    </add>" +
        "    <remove sel=\"/element/element[./attribute[@name='Name' and @value='Button1']]/attribute[@name='Is Visible']\" />" +
        "    <replace sel=\"/element/element[./attribute[@name='Name' and @value='Button1']]/element[./attribute[@name='Name' and @value='Label']]/attribute[@name='Text']/@value\">Jump</replace>" +
        "    <add sel=\"/element/element[./attribute[@name='Name' and @value='Button1']]\">" +
        "        <element type=\"Text\">" +
        "            <attribute name=\"Name\" value=\"KeyBinding\" />" +
        "            <attribute name=\"Text\" value=\"SPACE\" />" +
        "        </element>" +
        "    </add>" +
        "</patch>";

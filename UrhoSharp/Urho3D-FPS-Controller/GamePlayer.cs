﻿using System;
using System.Diagnostics;
using Urho;
using Urho.Physics;

namespace Urho3D_FPS_Controller
{
    public class GamePlayer:Sample
    {
        public const float CameraMinDist = 1.0f;
        public const float CameraInitialDist = 5.0f;
        public const float CameraMaxDist = 20.0f;
        bool firstPerson;
        Touch touch;
        public const float YawSensitivity = 0.1f;

        public GamePlayer(ApplicationOptions options):base(options)
        {
            Instance = this;
        }

        public static GamePlayer Instance { get; private set; }

        protected override void Start()
        {
            base.Start();

            //if (TouchEnabled)
            //    touch = new Touch(TouchSensitivity, Input);
            CreateScene();
            CreateCharacter();
            SubscribeToEvents();
        }

        void SubscribeToEvents()
        {
            Engine.SubscribeToPostUpdate(HandlePostUpdate);
            physicsWorld.SubscribeToPhysicsPreStep(HandlePhysicsPreStep);
        }

        void HandlePostUpdate(PostUpdateEventArgs args)
        {
            if (character == null)
                return;

            Node characterNode = character.Node;

            // Get camera lookat dir from character yaw + pitch
            Quaternion rot = characterNode.Rotation;
            Quaternion dir = rot * Quaternion.FromAxisAngle(Vector3.UnitX, character.Controls.Pitch);

            // Turn head to camera pitch, but limit to avoid unnatural animation
            Node headNode = characterNode.GetChild("Bip01_Head", true);
            if (headNode != null)
            {
                float limitPitch = MathHelper.Clamp(character.Controls.Pitch, -45.0f, 45.0f);
                Quaternion headDir = rot * Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f), limitPitch);
                // This could be expanded to look at an arbitrary target, now just look at a point in front
                Vector3 headWorldTarget = headNode.WorldPosition + headDir * new Vector3(0.0f, 0.0f, 1.0f);
                headNode.LookAt(headWorldTarget, new Vector3(0.0f, 1.0f, 0.0f), TransformSpace.World);
                // Correct head orientation because LookAt assumes Z = forward, but the bone has been authored differently (Y = forward)
                headNode.Rotate(new Quaternion(0.0f, 90.0f, 90.0f), TransformSpace.Local);
            }
            if (firstPerson)
            {
                if (headNode != null)
                {
                    CameraNode.Position = headNode.WorldPosition + rot*new Vector3(0.0f, 0.15f, 0.2f);
                    CameraNode.Rotation = dir;
                }
            }
            else
            {
                // Third person camera: position behind the character
                Vector3 aimPoint = characterNode.Position + rot * new Vector3(0.0f, 1.7f, 0.0f);

                // Collide camera ray with static physics objects (layer bitmask 2) to ensure we see the character properly
                Vector3 rayDir = dir * new Vector3(0f, 0f, -1f);
                float rayDistance = touch != null ? touch.CameraDistance : CameraInitialDist;

                PhysicsRaycastResult result = new PhysicsRaycastResult();
                scene.GetComponent<PhysicsWorld>().RaycastSingle(ref result, new Ray(aimPoint, rayDir), rayDistance, 2);
                if (result.Body != null)
                    rayDistance = Math.Min(rayDistance, result.Distance);
                rayDistance = MathHelper.Clamp(rayDistance, CameraMinDist, CameraMaxDist);

                CameraNode.Position = aimPoint + rayDir * rayDistance;
                CameraNode.Rotation = dir;
            }
        }
        protected override void OnUpdate(float timeStep)
        {
            Input input = Input;

            if (character != null)
            {
                // Clear previous controls
                character.Controls.Set(Character.CTRL_FORWARD | Character.CTRL_BACK | Character.CTRL_LEFT | Character.CTRL_RIGHT | Character.CTRL_JUMP, false);

                // Update controls using touch utility class
                touch?.UpdateTouches(character.Controls);

                // Update controls using keys
                if (UI.FocusElement == null)
                {
                    if (touch == null || !touch.UseGyroscope)
                    {
                        character.Controls.Set(Character.CTRL_FORWARD, input.GetKeyDown(Key.W));
                        character.Controls.Set(Character.CTRL_BACK, input.GetKeyDown(Key.S));
                        character.Controls.Set(Character.CTRL_LEFT, input.GetKeyDown(Key.A));
                        character.Controls.Set(Character.CTRL_RIGHT, input.GetKeyDown(Key.D));
                        character.Controls.Set(Character.CTRL_CROUCH, input.GetKeyDown(Key.Ctrl));
                    }
                    character.Controls.Set(Character.CTRL_JUMP, input.GetKeyDown(Key.Space));
                    //character.Controls.Set(Character.CT, input.GetMouseButtonDown(MouseButton.Left));

                    // Add character yaw & pitch from the mouse motion or touch input
                    if (TouchEnabled)
                    {
                        for (uint i = 0; i < input.NumTouches; ++i)
                        {
                            TouchState state = input.GetTouch(i);
                            if (state.TouchedElement == null)    // Touch on empty space
                            {
                                Camera camera = CameraNode.GetComponent<Camera>();
                                if (camera == null)
                                    return;

                                var graphics = Graphics;
                                character.Controls.Yaw += TouchSensitivity * camera.Fov / graphics.Height * state.Delta.X;
                                character.Controls.Pitch += TouchSensitivity * camera.Fov / graphics.Height * state.Delta.Y;
                            }
                        }
                    }
                    else
                    {
                        character.Controls.Yaw += (float)input.MouseMove.X * YawSensitivity;
                        character.Controls.Pitch += (float)input.MouseMove.Y * YawSensitivity;
                    }
                    // Limit pitch
                    character.Controls.Pitch = MathHelper.Clamp(character.Controls.Pitch, -80.0f, 80.0f);

                    // Switch between 1st and 3rd person
                    if (input.GetKeyPress(Key.F))
                        firstPerson = !firstPerson;

                    // Turn on/off gyroscope on mobile platform
                    if (touch != null && input.GetKeyPress(Key.G))
                        touch.UseGyroscope = !touch.UseGyroscope;

                    //if (input.GetKeyPress(Key.F5))
                    //{
                    //    var path = FileSystem.ProgramDir + "Data/Scenes";
                    //    FileSystem.CreateDir(path);
                    //    scene.SaveXml(path+ "/CharacterDemo.xml", "\t");
                    //}
                    //   if (input.GetKeyPress(Key.F7))
                    //{
                    //                   var path = FileSystem.ProgramDir + "Data/Scenes";
                    //                   FileSystem.CreateDir(path);
                    //                   scene.LoadXml(path + "/CharacterDemo.xml");
                    //	Node characterNode = scene.GetChild("Jack", true);
                    //	if (characterNode != null)
                    //	{
                    //		character = characterNode.GetComponent<Character>();
                    //	}
                    //	physicsWorld = scene.CreateComponent<PhysicsWorld>();
                    //	physicsWorld.SubscribeToPhysicsPreStep(HandlePhysicsPreStep);
                    //}
                }

                // Set rotation already here so that it's updated every rendering frame instead of every physics frame
                if (character != null)
                    character.Node.Rotation = Quaternion.FromAxisAngle(Vector3.UnitY, character.Controls.Yaw);
            }
        }
        PhysicsWorld physicsWorld;
        Character character;

        void HandlePhysicsPreStep(PhysicsPreStepEventArgs args)
        {
            character?.FixedUpdate(args.TimeStep);
        }


        private void CreateCharacter()
        {
            var cache = ResourceCache;

            //Node objectNode = scene.CreateChild("Jack");
            Node objectNode = scene.InstantiateXml(cache.GetFile("Objects/Character.xml", true),
                new Vector3(0f, 0.0f, 0f), Quaternion.Identity, CreateMode.Local);
            character = objectNode.CreateComponent<Character>();
        }

        private Scene scene;
        private void CreateScene()
        {
            var cache = ResourceCache;

            scene = new Scene();
            var path = FileSystem.ProgramDir + "Data/Scenes";
            FileSystem.CreateDir(path);
            scene.LoadXml(path + "/LevelFPS.xml");
            
            physicsWorld = scene.GetComponent<PhysicsWorld>();
            //// Create scene subsystem components
            //scene.CreateComponent<Octree>();
            //physicsWorld = scene.CreateComponent<PhysicsWorld>();

            // Create camera and define viewport. We will be doing load / save, so it's convenient to create the camera outside the scene,
            // so that it won't be destroyed and recreated, and we don't have to redefine the viewport on load
            CameraNode = new Node();
            Camera camera = CameraNode.CreateComponent<Camera>();
            camera.FarClip = 300.0f;
            Renderer.SetViewport(0, new Viewport(Context, scene, camera, null));

            //// Create static scene content. First create a zone for ambient lighting and fog control
            //Node zoneNode = scene.CreateChild("Zone");
            //Zone zone = zoneNode.CreateComponent<Zone>();
            ////zone.AmbientColor = new Color(0.15f, 0.15f, 0.15f);
            //zone.AmbientColor = new Color(1.0f, 1.0f, 1.0f);
            //zone.FogColor = new Color(0.5f, 0.5f, 0.7f);
            //zone.FogStart = 100.0f;
            //zone.FogEnd = 300.0f;
            //zone.SetBoundingBox(new BoundingBox(-1000.0f, 1000.0f));


        }



        private void InitTouchInput()
        {
            
        }

        void SetupScript()
        {
            //const Script* script(GetSubsystem<Script>());
            //asIScriptEngine * const engine(script->GetScriptEngine());

            //RegisterComponent<Character>(engine, "Character", true);
            //engine->RegisterObjectProperty("Character", "Controls controls", offsetof(Character, controls_));
            //engine->RegisterObjectProperty("Character", "Node@ modelNode", offsetof(Character, modelNode_));
        }
    }
}
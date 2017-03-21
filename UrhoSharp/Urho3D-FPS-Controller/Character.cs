//
// Copyright (c) 2008-2015 the Urho3D project.
// Copyright (c) 2015 Xamarin Inc
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Diagnostics;
using Urho;
using Urho.Physics;

namespace Urho3D_FPS_Controller
{
    public class Character : LogicComponent
    {
        public const int CTRL_FORWARD = 1;
        public const int CTRL_BACK = 2;
        public const int CTRL_LEFT = 4;
        public const int CTRL_RIGHT = 8;
        public const int CTRL_JUMP = 16;
        public const int CTRL_SHIFT = 32;
        public const int CTRL_CROUCH = 64;

        public const float MOVE_FORCE = 60.0f;
        public const float MOVE_ACCELERATION = 2.0f;
        public const float MOVE_DECELERATION = 4.0f;
        public const float INAIR_MOVE_FORCE = 60.0f;
        public const float BRAKE_FORCE = 6.0f;
        public const float INAIR_BRAKE_FORCE = 6.0f;
        public const float AGILITY = 155.0f;
        public const float INAIR_AGILITY = 1.5f;
        public const float JUMP_FORCE = 350.0f;
        public const float JUMP_RECOVER = 0.5f;
        public const float CROUCH_SPEED = 15.0f;
        public const float CROUCH_RECOVER = 0.1f;
        public const float YAW_SENSITIVITY = 0.1f;
        public const float INAIR_THRESHOLD_TIME = 0.1f;

        Controls controls_ = new Controls();

        /// Grounded flags for movement.
        bool onGround_;
        bool nearGround_;
        bool dropDetected_;

        /// Jump flags.
        bool okToJump_;
        bool jumpReleased_;

        /// In air timer. Due to possible physics inaccuracy, character can be off ground for max. 1/10 second and still be allowed to move.
        float inAirTimer_;
        float jumpTimer_;
        bool jumped_;

        /// Crouch flags
        bool crouching_;
        bool okToStand_;
        float crouchTimer_;

        /// Movement factors
        float moveMag_;
        Vector3 velocity_;

        /// Bug suppression
        bool highImpulseDetected_;

        /// Components
        RigidBody rigidBody_;
        Node modelNode_;
        public Character()
        {
            dropDetected_ = true;
            okToJump_ = true;
            okToStand_ = true;
        }

        public Controls Controls
        {
            get { return controls_; }
            set { controls_ = value; }
        }

        public override void OnAttachedToNode(Node node)
        {
            // Component has been inserted into its scene node. Subscribe to events now
            node.SubscribeToNodeCollision(HandleNodeCollision);
            node.GetChild("DropDetect",false).SubscribeToNodeCollision(HandleNodeCollision);
            node.GetChild("StandDetect", false).SubscribeToNodeCollision(HandleNodeCollision);
            node.GetChild("GroundDetect", false).SubscribeToNodeCollision(HandleNodeCollision);
            //TODO: Should be BeginRendering event
            //node.SubscribeToNodeCollision(HandleRenderUpdate);

            DelayedStart();
        }

        private void DelayedStart()
        {
            if (Scene != null)
            {
                var xmlFile = GamePlayer.Instance.ResourceCache.GetFile("Objects/CharacterModel.xml", true);
                if (xmlFile != null)
                {
                    modelNode_ = Scene.InstantiateXml(xmlFile, Node.Position, Node.Rotation, CreateMode.Local);
                }
            }
        }

        //TODO: Should be BeginRendering event
        private void HandleRenderUpdate(NodeCollisionEventArgs obj)
        {

        }

        private void HandleNodeCollision(NodeCollisionEventArgs obj)
        {

            RigidBody body = obj.Body;
            Node node = body.Node;
            String name = node.Name;
            RigidBody otherBody = obj.OtherBody;
            Node otherNode = otherBody.Node;
            String otherName = otherNode.Name;

            // Detect if there is ground within stepping range
            if (obj.Trigger)
            {
                if (name == "DropDetect")
                {
                    dropDetected_ = false;
                    return;
                }

                if (name == "StandDetect")
                {
                    okToStand_ = false;
                    return;
                }

                if (name == "GroundDetect")
                {
                    nearGround_ = true;
                    return;
                }
            }

            // Filter out collisions with DropDetect as otherNode, for some reason this can cause a crouch-jump glitch ?
            if (otherName == "DropDetect")
            {
                // dropDetected_ = false;
                return;
            }

            if (otherName == "StandDetect")
                return;

            if (otherName == "GroundDetect")
                return;

            /*if (name != "Character")
                return;*/

            Vector3 vel = body.LinearVelocity;
            float YY = (Node.Position.Y);
            CollisionShape colShape = Node.GetComponent<CollisionShape>();
            float height = colShape.Position.Y;

            foreach (var contacts in obj.Contacts)
            {
                Vector3 contactPosition = contacts.ContactPosition;
                Vector3 contactNormal = contacts.ContactNormal;
                float contactDistance = contacts.ContactDistance;
                float contactImpulse = contacts.ContactImpulse;

                // Prevent bounce from corner impact
                if (contactImpulse > 200.0f)
                    highImpulseDetected_ = true;

                
                // If contact is below node center and mostly vertical, assume it's a ground contact
                if (contactPosition.Y < YY + height)
                {
                    float level = Math.Abs(contactNormal.Y);
                    if (level > 0.75)
                        onGround_ = true;
                }
            }
        }

        public void FixedUpdate(float timeStep)
        {
            //var timeStep = e.TimeStep;

            if (rigidBody_ == null)
                rigidBody_ = GetComponent<RigidBody>();
            var animCtrl = modelNode_.GetComponent<AnimationController>();

            jumpTimer_ += timeStep;
            crouchTimer_ += timeStep;

            // Update the in air timer. Reset if grounded
            if (!onGround_)
                inAirTimer_ += timeStep;
            else
                inAirTimer_ = 0.0f;

            // When character has been in air less than 1/10 second, it's still interpreted as being on ground
            bool softGrounded = (inAirTimer_ < INAIR_THRESHOLD_TIME);

            // Update movement & animation
            var rot = Node.Rotation;

            Vector3 moveDir = Vector3.Zero;
            var velocity = rigidBody_.LinearVelocity;
            // Velocity on the XZ plane
            var planeVelocity = new Vector3(velocity.X, 0, velocity.Z);

            float speed = planeVelocity.Length;

            if (controls_.IsDown(CTRL_FORWARD))
                moveDir += Urho.Vector3.Forward * 1.0f;
            if (controls_.IsDown(CTRL_BACK))
                moveDir += Urho.Vector3.Back * 0.6f;
            if (controls_.IsDown(CTRL_LEFT))
                moveDir += Urho.Vector3.Left* 0.75f;
            if (controls_.IsDown(CTRL_RIGHT))
                moveDir += Urho.Vector3.Right * 0.75f;

            // Normalize move vector so that diagonal strafing is not faster
            if (moveDir.LengthSquared > 1.0f)
                moveDir.Normalize();

            if (controls_.IsDown(CTRL_SHIFT))
                moveDir *= 0.4f;

            // Crouching
            CollisionShape shape = GetComponent<CollisionShape>();

            Node dropDetectNode = Node.GetChild("DropDetect", true);
            CollisionShape dropDetectShape = dropDetectNode.GetComponent<CollisionShape>();

            Node standDetectNode = Node.GetChild("StandDetect", true);
            CollisionShape standDetectShape = standDetectNode.GetComponent<CollisionShape>();

            Node groundDetectNode = Node.GetChild("GroundDetect", true);
            CollisionShape groundDetectShape = groundDetectNode.GetComponent<CollisionShape>();

            bool crouch = ((controls_.IsDown(CTRL_CROUCH) && crouchTimer_ >= CROUCH_RECOVER) || !okToStand_);// || (!softGrounded && crouching_));
            
            rigidBody_.DisableMassUpdate();

            if (crouch)
            {
                if (softGrounded)
                    moveDir *= 0.3f;

                dropDetectShape.Position = Lerp(dropDetectShape.Position, new Vector3(0.0f, 1.0f, 0.0f), timeStep * CROUCH_SPEED);
                standDetectShape.Position = Lerp(standDetectShape.Position, new Vector3(0.0f, 2.1f, 0.0f), timeStep * CROUCH_SPEED);
                groundDetectShape.Position = Lerp(groundDetectShape.Position, new Vector3(0.0f, 1.3f, 0.0f), timeStep * CROUCH_SPEED);

                shape.Position = Lerp(shape.Position, new Vector3(0.0f, 1.5f, 0.0f), timeStep * CROUCH_SPEED);
                shape.Size = Lerp(shape.Size, new Vector3(0.8f, 1.0f, 0.8f), timeStep * CROUCH_SPEED);

                crouching_ = true;
            }
            else
            {
                dropDetectShape.Position = Lerp(dropDetectShape.Position, new Vector3(0.0f, 0.0f, 0.0f), timeStep * CROUCH_SPEED);
                standDetectShape.Position = Lerp(standDetectShape.Position, new Vector3(0.0f, 1.1f, 0.0f), timeStep * CROUCH_SPEED);
                groundDetectShape.Position = Lerp(groundDetectShape.Position, new Vector3(0.0f, 0.3f, 0.0f), timeStep * CROUCH_SPEED);

                Vector3 pos = shape.Position;
                Vector3 newPos = Lerp(pos, new Vector3(0.0f, 1.0f, 0.0f), timeStep * CROUCH_SPEED);

                //  Adjust rigid body position to prevent penetration into the ground when standing
                if (softGrounded && !dropDetected_)
                {
                    rigidBody_.SetPosition((rigidBody_.Position - (newPos - pos) * 2.0f));
                }
                else if (nearGround_)
                {
                    rigidBody_.SetPosition((rigidBody_.Position - (newPos - pos) * 3.0f));
                }

                shape.Position = (newPos);
                shape.Size = Lerp(shape.Size, new Vector3(0.8f, 2.0f, 0.8f), timeStep * CROUCH_SPEED);


                if (crouching_)
                    crouchTimer_ = 0.0f;

                crouching_ = false;
            }

            rigidBody_.EnableMassUpdate();

            // Prevent bounce bugs when colliding against corners, while still allowing movement up ramps
            if (highImpulseDetected_ && nearGround_)
            {
                Vector3 vel = (rigidBody_.LinearVelocity);
                rigidBody_.SetLinearVelocity(new Vector3(vel.X, Math.Min(vel.Y, 0.0f), vel.Z));
            }

            // If in air, allow control, but slower than when on ground
            if (softGrounded && okToJump_)
            {
                float moveMag = (moveDir.Length);
                if (moveMag > 0.0001)
                    moveMag_ = Lerp(moveMag_, 1.0f, timeStep * MOVE_ACCELERATION);
                else
                    moveMag_ = Lerp(moveMag_, 0.0f, timeStep * MOVE_DECELERATION);

                velocity_ = Lerp(velocity_, rot * moveDir * moveMag_ * MOVE_FORCE, timeStep * AGILITY);
                rigidBody_.ApplyImpulse(velocity_);
                Vector3 brakeForce = (planeVelocity * -BRAKE_FORCE);
                rigidBody_.ApplyImpulse(brakeForce);
            }
            else
            {
                if (!(dropDetected_ && nearGround_))
                {
                    velocity_ = Lerp(velocity_, rot * moveDir * moveMag_ * INAIR_MOVE_FORCE, timeStep * INAIR_AGILITY);
                    rigidBody_.ApplyImpulse(velocity_);
                    Vector3 brakeForce = (planeVelocity * -INAIR_BRAKE_FORCE);
                    rigidBody_.ApplyImpulse(brakeForce);
                }
            }

            // Jumping
            if (!controls_.IsDown(CTRL_JUMP))
                jumpReleased_ = true;

            if (!okToJump_ && jumpTimer_ > JUMP_RECOVER && onGround_ && softGrounded && jumpReleased_)
                okToJump_ = true;

            if (softGrounded && !highImpulseDetected_)
            {
                if (controls_.IsDown(CTRL_JUMP) && okToJump_)
                {
                    Vector3 vel = (rigidBody_.LinearVelocity);
                    //URHO3D_LOGINFO("JUMP " + String(vel.y_));
                    rigidBody_.SetLinearVelocity(new Vector3(vel.X, Math.Max(vel.Y, 0.0f), vel.Z));
                    rigidBody_.ApplyImpulse(Urho.Vector3.UnitY * JUMP_FORCE);
                    okToJump_ = false;
                    jumpTimer_ = 0.0f;
                    jumpReleased_ = false;
                }
            }

            // Keep the character close the ground / prevent launching from high speed movement
            if ((okToJump_ && inAirTimer_ > 0.0f && inAirTimer_ <= INAIR_THRESHOLD_TIME && speed > 0.1f && !dropDetected_)
                ||
                (inAirTimer_ > INAIR_THRESHOLD_TIME && !dropDetected_))
            {
                rigidBody_.ApplyImpulse(new Vector3(0.0f, -9.81f * speed, 0.0f));
            }

            // Prevent sliding while on slopes
            rigidBody_.UseGravity = (!onGround_);

            // Add artificial gravity after some time to pull to ground faster
            if (inAirTimer_ > 0.1f && velocity_.Length > 0.1f)
            {
                rigidBody_.ApplyForce(new Vector3(0.0f, -9.81f*2.0f, 0.0f));
            }

            // Play walk animation if moving on ground, otherwise fade it out
            if (softGrounded && !moveDir.Equals(Vector3.Zero))
                animCtrl.PlayExclusive("Models/Jack_Walk.ani", 0, true, 0.2f);
            else
                animCtrl.Stop("Models/Jack_Walk.ani", 0.2f);
            // Set walk animation speed proportional to velocity
            animCtrl.SetSpeed("Models/Jack_Walk.ani", speed / 9.0f);

            // Reset grounded flag for next frame
            onGround_ = false;
            nearGround_ = false;
            dropDetected_ = true;
            okToStand_ = true;
            highImpulseDetected_ = false;

            modelNode_.Position = Lerp(modelNode_.Position,
                rigidBody_.Position + rigidBody_.LinearVelocity * timeStep * 4.0f,
                timeStep * (10.0f + rigidBody_.LinearVelocity.Length / 8.0f
            ));

            modelNode_.Rotation = rot;
        }

        private Vector3 Lerp(Vector3 a, Vector3 b, float blend)
        {
            if (blend <= 0)
                return a;
            if (blend >= 1)
                return b;
            return a + (b - a)*blend;
        }

        private float Lerp(float a, float b, float blend)
        {
            if (blend <= 0)
                return a;
            if (blend >= 1)
                return b;
            return a + (b - a) * blend;
        }


    }
}
